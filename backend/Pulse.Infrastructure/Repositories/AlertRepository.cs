using Npgsql;
using System.Text.Json;

namespace Pulse.Infrastructure.Repositories;

public sealed class AlertRepository(TenantDatabase database) : IAlertRepository
{
    public Task<AlertPage> ListAsync(Guid tenant, string? state, DateTimeOffset? before, Guid? beforeId, CancellationToken ct) => database.ExecuteAsync(tenant, async (c, token) =>
    {
        var rows = await Database.Query(c, "SELECT id,device_id,rule_version_id,state,message,created_at,updated_at FROM alerts WHERE (@state='' OR state=@state) AND (created_at,id)<(@at,@id) ORDER BY created_at DESC,id DESC LIMIT 100", token, ("state", state ?? ""), ("at", before ?? DateTimeOffset.UtcNow.AddDays(1)), ("id", beforeId ?? Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")));
        return new AlertPage(rows, rows.Count == 100 ? new(rows[^1]["created_at"], rows[^1]["id"]) : null);
    }, ct);
    public Task<List<Dictionary<string, object?>>> TransitionsAsync(Guid tenant, Guid alert, CancellationToken ct) => database.ExecuteAsync(tenant, (c, token) => Database.Query(c, "SELECT id,from_state,to_state,user_id,created_at FROM alert_transitions WHERE alert_id=@id ORDER BY created_at,id", token, ("id", alert)), ct);
    public Task<TransitionOutcome> TransitionAsync(Guid tenant, Guid user, Guid alert, string state, CancellationToken ct) => database.ExecuteAsync(tenant, async (c, token) =>
    {
        var rows = await Database.Query(c, "SELECT state FROM alerts WHERE id=@id FOR UPDATE", token, ("id", alert));
        if (rows.Count == 0)
            return TransitionOutcome.NotFound;
        var from = (string)rows[0]["state"]!;
        if (!AlertState.CanTransition(from, state))
            return TransitionOutcome.InvalidTransition;
        await Database.Execute(c, "UPDATE alerts SET state=@state,updated_at=now() WHERE id=@id", token, ("state", state), ("id", alert));
        await Database.Execute(c, "INSERT INTO alert_transitions(id,tenant_id,alert_id,user_id,from_state,to_state) VALUES(@id,@t,@alert,@u,@from,@to)", token, ("id", Guid.NewGuid()), ("t", tenant), ("alert", alert), ("u", user), ("from", from), ("to", state));
        await Database.Execute(c, "INSERT INTO outbox_messages(id,tenant_id,kind,payload) VALUES(@id,@t,'alert',CAST(@p AS jsonb))", token, ("id", Guid.NewGuid()), ("t", tenant), ("p", JsonSerializer.Serialize(new
        {
            alertId = alert,
            state
        })));
        return TransitionOutcome.Updated;
    }, ct);
    public Task<List<Dictionary<string, object?>>> SnapshotAsync(Guid tenant, CancellationToken ct) => database.ExecuteAsync(tenant, (c, token) => Database.Query(c, "SELECT id,state,message,created_at,updated_at FROM alerts ORDER BY updated_at DESC,id DESC LIMIT 100", token), ct);
}
