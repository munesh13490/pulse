using System.Text.Json;
using Pulse.Application;
using Pulse.Domain;

namespace Pulse.Infrastructure.Repositories;

public sealed class TelemetryRepository(Database db) : ITelemetryRepository
{
    public async Task<IReadOnlyList<ItemResult>> Ingest(DeviceIdentity device, IReadOnlyList<TelemetryItem> items, CancellationToken ct)
    {
        if (items.Count is < 1 or > 500)
            throw new ArgumentException("A batch requires 1-500 items.");
        await using var c = await db.App(ct);
        await using var tx = await Database.Scope(c, device.TenantId, ct);
        await Database.Execute(c, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", ct, ("key", $"event-time:{device.TenantId}"));
        var result = new List<ItemResult>();
        var now = DateTimeOffset.UtcNow;
        foreach (var item in items)
        {
            var error = TelemetryPolicy.Validate(item, now);
            if (error is not null)
            {
                result.Add(new(item.EventId, "rejected", error));
                continue;
            }

            var count = await Database.Execute(c, "INSERT INTO telemetry_idempotency(tenant_id,device_id,event_id) VALUES(@t,@d,@e) ON CONFLICT(tenant_id,device_id,event_id) DO NOTHING", ct, ("t", device.TenantId), ("d", device.DeviceId), ("e", item.EventId));
            if (count == 0)
            {
                result.Add(new(item.EventId, "duplicate"));
                continue;
            }

            var id = Guid.NewGuid();
            await Database.Execute(c, "INSERT INTO telemetry_events(telemetry_id,tenant_id,device_id,event_id,metric_name,metric_value,event_timestamp) VALUES(@id,@t,@d,@e,@m,@v,@at)", ct, ("id", id), ("t", device.TenantId), ("d", device.DeviceId), ("e", item.EventId), ("m", item.Metric), ("v", item.Value), ("at", item.OccurredAt.ToUniversalTime()));
            await Database.Execute(c, "INSERT INTO outbox_messages(outbox_id,tenant_id,event_type,payload) VALUES(@id,@t,'telemetry',CAST(@payload AS jsonb))", ct, ("id", Guid.NewGuid()), ("t", device.TenantId), ("payload", JsonSerializer.Serialize(new
            {
                deviceId = device.DeviceId,
                eventId = item.EventId,
                item.Metric,
                item.Value,
                item.OccurredAt
            })));
            result.Add(new(item.EventId, "accepted"));
        }

        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<List<Dictionary<string, object?>>> ReadAsync(Guid tenant, Guid device, CancellationToken ct)
    {
        await using var c = await db.App(ct);
        await using var tx = await Database.Scope(c, tenant, ct);
        return await Database.Query(c, "SELECT event_id,metric_name AS metric,metric_value AS value,event_timestamp AS occurred_at FROM telemetry_events WHERE device_id=@d ORDER BY event_timestamp DESC LIMIT 500", ct, ("d", device));
    }
}
