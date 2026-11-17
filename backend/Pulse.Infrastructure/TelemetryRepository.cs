using System.Text.Json;
using Pulse.Application;
using Pulse.Domain;
namespace Pulse.Infrastructure;

public sealed class TelemetryRepository(Database db) : ITelemetryRepository
{
    public async Task<IReadOnlyList<ItemResult>> Ingest(DeviceIdentity device, IReadOnlyList<TelemetryItem> items, CancellationToken ct)
    {
        if (items.Count is < 1 or > 500) throw new ArgumentException("A batch requires 1-500 items.");
        await using var c = await db.App(ct); await using var tx = await Database.Scope(c, device.TenantId, ct);
        await Database.Execute(c, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", ct, ("key", $"event-time:{device.TenantId}"));
        var result = new List<ItemResult>(); var now = DateTimeOffset.UtcNow;
        foreach (var item in items)
        {
            var error = TelemetryPolicy.Validate(item, now);
            if (error is not null) { result.Add(new(item.EventId, "rejected", error)); continue; }
            var count = await Database.Execute(c, "INSERT INTO telemetry_idempotency(id,tenant_id,device_id,event_id) VALUES(@id,@t,@d,@e) ON CONFLICT(tenant_id,device_id,event_id) DO NOTHING", ct,
                ("id", Guid.NewGuid()), ("t", device.TenantId), ("d", device.DeviceId), ("e", item.EventId));
            if (count == 0) { result.Add(new(item.EventId, "duplicate")); continue; }
            var id = Guid.NewGuid();
            await Database.Execute(c, "INSERT INTO telemetry_events(id,tenant_id,device_id,event_id,metric,value,occurred_at) VALUES(@id,@t,@d,@e,@m,@v,@at)", ct,
                ("id", id), ("t", device.TenantId), ("d", device.DeviceId), ("e", item.EventId), ("m", item.Metric), ("v", item.Value), ("at", item.OccurredAt.ToUniversalTime()));
            await Database.Execute(c, "INSERT INTO outbox_messages(id,tenant_id,kind,payload) VALUES(@id,@t,'telemetry',CAST(@payload AS jsonb))", ct,
                ("id", Guid.NewGuid()), ("t", device.TenantId), ("payload", JsonSerializer.Serialize(new { deviceId = device.DeviceId, eventId = item.EventId, item.Metric, item.Value, item.OccurredAt })));
            result.Add(new(item.EventId, "accepted"));
        }
        await tx.CommitAsync(ct); return result;
    }
}
