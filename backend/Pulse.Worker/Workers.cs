using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pulse.Application;
using Pulse.Infrastructure;
using Pulse.Domain;
namespace Pulse.Worker;

public abstract class TenantWorker(Database db, ILogger logger, TimeSpan interval) : BackgroundService
{
    protected abstract Task Process(NpgsqlConnection c, Guid tenant, CancellationToken ct);
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var auth = await db.Auth(ct);
                var tenants = await Database.Query(auth, "SELECT id FROM tenants", ct);
                foreach (var t in tenants)
                {
                    var tenant = (Guid)t["id"]!;
                    try
                    {
                        await using var c = await db.App(ct); await using var tx = await Database.Scope(c, tenant, ct);
                        // One replica per worker type and tenant; transaction locks release on crash.
                        var locks = await Database.Query(c, "SELECT pg_try_advisory_xact_lock(hashtextextended(@key,0)) AS acquired", ct, ("key", $"{GetType().Name}:{tenant}"));
                        if (!(bool)locks[0]["acquired"]!) continue;
                        await Process(c, tenant, ct); await tx.CommitAsync(ct);
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogError(ex, "Worker {Worker} failed for tenant {Tenant}", GetType().Name, tenant); }
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogError(ex, "Worker cycle failed"); }
            await Task.Delay(interval, ct);
        }
    }
}
public sealed class RuleEvaluationWorker(Database db, ILogger<RuleEvaluationWorker> logger) : TenantWorker(db, logger, TimeSpan.FromSeconds(5))
{
    protected override async Task Process(NpgsqlConnection c, Guid tenant, CancellationToken ct)
    {
        await Database.Execute(c, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", ct, ("key", $"event-time:{tenant}"));
        // Finalize in event-time order once all permitted late arrivals have reached the database.
        var work = await Database.Query(c, "SELECT id,payload FROM outbox_messages WHERE kind='telemetry' AND processed_at IS NULL AND (payload->>'OccurredAt')::timestamptz<=now()-interval '72 hours' ORDER BY (payload->>'OccurredAt')::timestamptz,(payload->>'eventId')::uuid LIMIT 100 FOR UPDATE SKIP LOCKED", ct);
        foreach (var row in work)
        {
            using var doc = JsonDocument.Parse((string)row["payload"]!); var e = doc.RootElement;
            var device = e.GetProperty("deviceId").GetGuid(); var eventId = e.GetProperty("eventId").GetGuid();
            var at = e.GetProperty("OccurredAt").GetDateTimeOffset(); var metric = e.GetProperty("Metric").GetString()!; var value = e.GetProperty("Value").GetDouble();
            var rules = await Database.Query(c, "SELECT r.id AS rule_id,v.id,v.kind,v.threshold,v.window_seconds FROM alert_rules r JOIN LATERAL(SELECT * FROM rule_versions WHERE rule_id=r.id AND created_at<=@at ORDER BY version DESC LIMIT 1) v ON true WHERE r.enabled AND r.device_id=@d AND v.metric=@m ORDER BY r.id", ct, ("at", at), ("d", device), ("m", metric));
            foreach (var rule in rules)
            {
                var values = await Database.Query(c, "SELECT value,occurred_at FROM telemetry_events WHERE device_id=@d AND metric=@m AND occurred_at>=@start AND occurred_at<=@at ORDER BY occurred_at,event_id", ct,
                    ("d", device), ("m", metric), ("start", at.AddSeconds(-(int)rule["window_seconds"]!)), ("at", at));
                var kind = (string)rule["kind"]!;
                var observed = RuleEvaluation.Evaluate(kind, values.Select(v => new MetricSample(new DateTimeOffset((DateTime)v["occurred_at"]!), (double)v["value"]!)).ToArray());
                if (observed is null || observed <= (double)rule["threshold"]!) continue;
                var policies = await Database.Query(c, "UPDATE suppression_policies SET last_alert_at=@at WHERE rule_id=@rule AND (last_alert_at IS NULL OR last_alert_at+make_interval(secs=>seconds)<=@at) RETURNING id", ct, ("at", at), ("rule", rule["rule_id"]));
                if (policies.Count == 0) continue;
                var alert = Guid.NewGuid();
                await Database.Execute(c, "INSERT INTO alerts(id,tenant_id,device_id,rule_version_id,event_id,message) VALUES(@id,@t,@d,@v,@e,@msg) ON CONFLICT(tenant_id,rule_version_id,event_id) DO NOTHING", ct,
                    ("id", alert), ("t", tenant), ("d", device), ("v", rule["id"]), ("e", eventId), ("msg", $"{metric}: {observed} exceeds {rule["threshold"]}"));
                await Database.Execute(c, "INSERT INTO outbox_messages(id,tenant_id,kind,payload) VALUES(@id,@t,'alert',CAST(@p AS jsonb))", ct,
                    ("id", Guid.NewGuid()), ("t", tenant), ("p", JsonSerializer.Serialize(new { alertId = alert, state = "OPEN" })));
            }
            await Database.Execute(c, "UPDATE outbox_messages SET processed_at=now(),attempts=attempts+1 WHERE id=@id", ct, ("id", row["id"]));
        }
    }
}
public sealed class OutboxWorker(Database db, INotificationBus bus, ILogger<OutboxWorker> logger) : TenantWorker(db, logger, TimeSpan.FromSeconds(1))
{
    protected override async Task Process(NpgsqlConnection c, Guid tenant, CancellationToken ct)
    {
        var rows = await Database.Query(c, "SELECT id,payload FROM outbox_messages WHERE kind='alert' AND processed_at IS NULL AND available_at<=now() ORDER BY created_at LIMIT 100 FOR UPDATE SKIP LOCKED", ct);
        foreach (var row in rows)
        {
            await bus.Publish(tenant, (string)row["payload"]!);
            await Database.Execute(c, "UPDATE outbox_messages SET processed_at=now(),attempts=attempts+1 WHERE id=@id", ct, ("id", row["id"]));
        }
    }
}
public sealed class RollupWorker(Database db, ILogger<RollupWorker> logger) : TenantWorker(db, logger, TimeSpan.FromMinutes(1))
{
    protected override async Task Process(NpgsqlConnection c, Guid tenant, CancellationToken ct)
    {
        await Database.Execute(c, "INSERT INTO hourly_rollups(id,tenant_id,device_id,metric,hour,count,sum,min,max) SELECT gen_random_uuid(),tenant_id,device_id,metric,date_trunc('hour',occurred_at),count(*),sum(value),min(value),max(value) FROM telemetry_events WHERE occurred_at>=date_trunc('hour',now()-interval '96 hours') GROUP BY tenant_id,device_id,metric,date_trunc('hour',occurred_at) ON CONFLICT(tenant_id,device_id,metric,hour) DO UPDATE SET count=excluded.count,sum=excluded.sum,min=excluded.min,max=excluded.max", ct);
    }
}
public sealed class RetentionWorker(IConfiguration config, ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Dedicated local maintenance credentials, never shared with the request-serving API.
                await using var c = new NpgsqlConnection(config.GetConnectionString("Maintenance") ?? throw new InvalidOperationException("Set ConnectionStrings__Maintenance"));
                await c.OpenAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
                await Database.Execute(c, "SELECT pg_advisory_xact_lock(7343193); SELECT maintain_telemetry_partitions()", ct);
                var partitions = await Database.Query(c, "SELECT child.relname FROM pg_inherits JOIN pg_class parent ON parent.oid=inhparent JOIN pg_class child ON child.oid=inhrelid WHERE parent.relname='telemetry_events'", ct);
                foreach (var p in partitions)
                {
                    var name = (string)p["relname"]!;
                    if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^telemetry_events_\d{4}_\d{2}$") &&
                        DateTime.TryParseExact(name["telemetry_events_".Length..], "yyyy_MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var month) &&
                        month.AddMonths(1) < DateTime.UtcNow.AddDays(-90))
                        await Database.Execute(c, $"DROP TABLE \"{name}\"", ct);
                }
                await Database.Execute(c, "DELETE FROM telemetry_idempotency WHERE created_at<now()-interval '90 days'; DELETE FROM outbox_messages WHERE processed_at<now()-interval '7 days'; DELETE FROM user_refresh_tokens WHERE expires_at<now()-interval '7 days'", ct);
                await tx.CommitAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogError(ex, "Retention failed"); }
            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }
}
