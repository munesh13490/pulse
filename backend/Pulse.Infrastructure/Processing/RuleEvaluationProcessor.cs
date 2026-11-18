using System.Text.Json;
using Npgsql;

namespace Pulse.Infrastructure.Processing;

public sealed class RuleEvaluationProcessor()
{
    public async Task Process(NpgsqlConnection c, Guid tenant, CancellationToken ct)
    {
        await Database.Execute(c, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", ct, ("key", $"event-time:{tenant}"));
        // Finalize in event-time order once all permitted late arrivals have reached the database.
        var work = await Database.Query(c, "SELECT id,payload FROM outbox_messages WHERE kind='telemetry' AND processed_at IS NULL AND (payload->>'OccurredAt')::timestamptz<=now()-interval '72 hours' ORDER BY (payload->>'OccurredAt')::timestamptz,(payload->>'eventId')::uuid LIMIT 100 FOR UPDATE SKIP LOCKED", ct);
        foreach (var row in work)
        {
            using var doc = JsonDocument.Parse((string)row["payload"]!);
            var e = doc.RootElement;
            var device = e.GetProperty("deviceId").GetGuid();
            var eventId = e.GetProperty("eventId").GetGuid();
            var at = e.GetProperty("OccurredAt").GetDateTimeOffset();
            var metric = e.GetProperty("Metric").GetString()!;
            var value = e.GetProperty("Value").GetDouble();
            var rules = await Database.Query(c, "SELECT r.id AS rule_id,v.id,v.kind,v.threshold,v.window_seconds FROM alert_rules r JOIN LATERAL(SELECT * FROM rule_versions WHERE rule_id=r.id AND created_at<=@at ORDER BY version DESC LIMIT 1) v ON true WHERE r.enabled AND r.device_id=@d AND v.metric=@m ORDER BY r.id", ct, ("at", at), ("d", device), ("m", metric));
            foreach (var rule in rules)
            {
                var values = await Database.Query(c, "SELECT value,occurred_at FROM telemetry_events WHERE device_id=@d AND metric=@m AND occurred_at>=@start AND occurred_at<=@at ORDER BY occurred_at,event_id", ct, ("d", device), ("m", metric), ("start", at.AddSeconds(-(int)rule["window_seconds"]!)), ("at", at));
                var kind = (string)rule["kind"]!;
                var observed = RuleEvaluation.Evaluate(kind, values.Select(v => new MetricSample(new DateTimeOffset((DateTime)v["occurred_at"]!), (double)v["value"]!)).ToArray());
                if (observed is null || observed <= (double)rule["threshold"]!)
                    continue;
                var policies = await Database.Query(c, "UPDATE suppression_policies SET last_alert_at=@at WHERE rule_id=@rule AND (last_alert_at IS NULL OR last_alert_at+make_interval(secs=>seconds)<=@at) RETURNING id", ct, ("at", at), ("rule", rule["rule_id"]));
                if (policies.Count == 0)
                    continue;
                var alert = Guid.NewGuid();
                await Database.Execute(c, "INSERT INTO alerts(id,tenant_id,device_id,rule_version_id,event_id,message) VALUES(@id,@t,@d,@v,@e,@msg) ON CONFLICT(tenant_id,rule_version_id,event_id) DO NOTHING", ct, ("id", alert), ("t", tenant), ("d", device), ("v", rule["id"]), ("e", eventId), ("msg", $"{metric}: {observed} exceeds {rule["threshold"]}"));
                await Database.Execute(c, "INSERT INTO outbox_messages(id,tenant_id,kind,payload) VALUES(@id,@t,'alert',CAST(@p AS jsonb))", ct, ("id", Guid.NewGuid()), ("t", tenant), ("p", JsonSerializer.Serialize(new
                {
                    alertId = alert,
                    state = "OPEN"
                })));
            }

            await Database.Execute(c, "UPDATE outbox_messages SET processed_at=now(),attempts=attempts+1 WHERE id=@id", ct, ("id", row["id"]));
        }
    }
}
