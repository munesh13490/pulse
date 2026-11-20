using System.Text.Json;
using Npgsql;

namespace Pulse.Infrastructure.Processing;

public sealed class RuleEvaluationProcessor()
{
    private static bool Matches(double value, double threshold, string comparison) => comparison switch
    {
        ">" => value > threshold,
        ">=" => value >= threshold,
        "<" => value < threshold,
        "<=" => value <= threshold,
        _ => false
    };

    public async Task Process(NpgsqlConnection c, Guid tenant, CancellationToken ct)
    {
        await Database.Execute(c, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", ct, ("key", $"event-time:{tenant}"));
        // Finalize in event-time order once all permitted late arrivals have reached the database.
        var work = await Database.Query(c, "SELECT outbox_id AS id,payload FROM outbox_messages WHERE event_type='telemetry' AND processed_at IS NULL AND (payload->>'OccurredAt')::timestamptz<=now()-interval '72 hours' ORDER BY (payload->>'OccurredAt')::timestamptz,(payload->>'eventId')::uuid LIMIT 100 FOR UPDATE SKIP LOCKED", ct);
        foreach (var row in work)
        {
            using var doc = JsonDocument.Parse((string)row["payload"]!);
            var e = doc.RootElement;
            var device = e.GetProperty("deviceId").GetGuid();
            var eventId = e.GetProperty("eventId").GetGuid();
            var at = e.GetProperty("OccurredAt").GetDateTimeOffset();
            var metric = e.GetProperty("Metric").GetString()!;
            var value = e.GetProperty("Value").GetDouble();
            var rules = await Database.Query(c, "SELECT r.rule_id,v.rule_version_id AS id,COALESCE(v.configuration->>'kind',lower(replace(r.rule_type,'_','-'))) AS kind,v.threshold_value AS threshold,v.comparison_operator,COALESCE(v.window_seconds,1) AS window_seconds FROM alert_rules r JOIN LATERAL(SELECT * FROM rule_versions WHERE tenant_id=r.tenant_id AND rule_id=r.rule_id AND created_at<=@at ORDER BY version_number DESC LIMIT 1) v ON true WHERE r.is_enabled AND (r.device_id=@d OR r.device_id IS NULL) AND COALESCE(v.configuration->>'metric',r.metric_name)=@m ORDER BY r.rule_id", ct, ("at", at), ("d", device), ("m", metric));
            foreach (var rule in rules)
            {
                var values = await Database.Query(c, "SELECT metric_value AS value,event_timestamp AS occurred_at FROM telemetry_events WHERE device_id=@d AND metric_name=@m AND event_timestamp BETWEEN @start AND @at ORDER BY event_timestamp,event_id", ct, ("d", device), ("m", metric), ("start", at.AddSeconds(-(int)rule["window_seconds"]!)), ("at", at));
                var kind = (string)rule["kind"]!;
                var observed = RuleEvaluation.Evaluate(kind, values.Select(v => new MetricSample(new DateTimeOffset((DateTime)v["occurred_at"]!), (double)v["value"]!)).ToArray());
                var threshold = (double)rule["threshold"]!;
                if (observed is null || !Matches(observed.Value, threshold, (string)rule["comparison_operator"]!))
                    continue;
                var policies = await Database.Query(c, "SELECT 1 WHERE NOT EXISTS (SELECT 1 FROM suppression_policies WHERE rule_id=@rule AND is_enabled) UNION ALL SELECT 1 FROM suppression_policies WHERE rule_id=@rule AND is_enabled AND (last_alert_at IS NULL OR last_alert_at+make_interval(secs=>suppress_for_seconds)<=@at)", ct, ("at", at), ("rule", rule["rule_id"]));
                if (policies.Count == 0)
                    continue;
                var alert = Guid.NewGuid();
                var inserted = await Database.Execute(c, "INSERT INTO alerts(alert_id,tenant_id,device_id,rule_version_id,source_event_id,message,trigger_value,triggered_at) VALUES(@id,@t,@d,@v,@e,@msg,@value,@at) ON CONFLICT(tenant_id,device_id,rule_version_id,source_event_id) DO NOTHING", ct, ("id", alert), ("t", tenant), ("d", device), ("v", rule["id"]), ("e", eventId), ("msg", $"{metric}: {observed} {rule["comparison_operator"]} {threshold}"), ("value", observed.Value), ("at", at));
                if (inserted == 0)
                    continue;
                await Database.Execute(c, "UPDATE suppression_policies SET last_alert_at=@at WHERE rule_id=@rule AND is_enabled", ct, ("at", at), ("rule", rule["rule_id"]));
                await Database.Execute(c, "INSERT INTO outbox_messages(outbox_id,tenant_id,event_type,payload) VALUES(@id,@t,'alert',CAST(@p AS jsonb))", ct, ("id", Guid.NewGuid()), ("t", tenant), ("p", JsonSerializer.Serialize(new
                {
                    alertId = alert,
                    state = "OPEN"
                })));
            }

            await Database.Execute(c, "UPDATE outbox_messages SET processed_at=now(),attempts=attempts+1 WHERE outbox_id=@id", ct, ("id", row["id"]));
        }
    }
}
