using System.Text.Json;
using Npgsql;

namespace Pulse.Infrastructure.Processing;

public sealed class OutboxProcessor(INotificationBus bus)
{
    public async Task Process(NpgsqlConnection c, Guid tenant, CancellationToken ct)
    {
        var rows = await Database.Query(c, "SELECT id,payload FROM outbox_messages WHERE kind='alert' AND processed_at IS NULL AND available_at<=now() ORDER BY created_at LIMIT 100 FOR UPDATE SKIP LOCKED", ct);
        foreach (var row in rows)
        {
            await bus.Publish(tenant, (string)row["payload"]!);
            await Database.Execute(c, "UPDATE outbox_messages SET processed_at=now(),attempts=attempts+1 WHERE id=@id", ct, ("id", row["id"]));
        }
    }
}
