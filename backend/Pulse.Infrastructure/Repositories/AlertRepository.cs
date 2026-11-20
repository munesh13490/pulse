using Npgsql;
using System.Text.Json;

namespace Pulse.Infrastructure.Repositories;

public sealed class AlertRepository(TenantDatabase database) : IAlertRepository
{
    public Task<AlertPage> ListAsync(Guid tenant, string? state, DateTimeOffset? before, Guid? beforeId, CancellationToken ct) => database.ExecuteAsync(tenant, async (c, token) =>
    {
        var rows = await Database.Query(c, "SELECT alert_id AS id,device_id,rule_version_id,status AS state,message,created_at,updated_at FROM alerts WHERE (@state='' OR status=@state) AND (created_at,alert_id)<(@at,@id) ORDER BY created_at DESC,alert_id DESC LIMIT 100", token, ("state", state ?? ""), ("at", before ?? DateTimeOffset.UtcNow.AddDays(1)), ("id", beforeId ?? Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")));
        return new AlertPage(rows, rows.Count == 100 ? new(rows[^1]["created_at"], rows[^1]["id"]) : null);
    }, ct);
    public Task<List<Dictionary<string, object?>>> TransitionsAsync(Guid tenant, Guid alert, CancellationToken ct) => database.ExecuteAsync(tenant, (c, token) => Database.Query(c, "SELECT transition_id AS id,from_status AS from_state,to_status AS to_state,actor_user_id AS user_id,occurred_at AS created_at FROM alert_transitions WHERE alert_id=@id ORDER BY occurred_at,transition_id", token, ("id", alert)), ct);
    public Task<TransitionOutcome> TransitionAsync(Guid tenant, Guid user, Guid alert, string state, CancellationToken ct) => database.ExecuteAsync(tenant, async (c, token) =>
    {
        var rows = await Database.Query(c, "SELECT status AS state FROM alerts WHERE alert_id=@id FOR UPDATE", token, ("id", alert));
        if (rows.Count == 0)
            return TransitionOutcome.NotFound;
        var from = (string)rows[0]["state"]!;
        if (!AlertState.CanTransition(from, state))
            return TransitionOutcome.InvalidTransition;
        await Database.Execute(c, "UPDATE alerts SET status=CAST(@state AS text),updated_at=now(),row_version=row_version+1,acknowledged_by=CASE WHEN @state='ACKNOWLEDGED' THEN @u ELSE acknowledged_by END,acknowledged_at=CASE WHEN @state='ACKNOWLEDGED' THEN now() ELSE acknowledged_at END,resolved_by=CASE WHEN @state='RESOLVED' THEN @u ELSE resolved_by END,resolved_at=CASE WHEN @state='RESOLVED' THEN now() ELSE resolved_at END WHERE alert_id=@id", token, ("state", state), ("id", alert), ("u", user));
        await Database.Execute(c, "INSERT INTO alert_transitions(transition_id,tenant_id,alert_id,actor_user_id,from_status,to_status) VALUES(@id,@t,@alert,@u,@from,@to)", token, ("id", Guid.NewGuid()), ("t", tenant), ("alert", alert), ("u", user), ("from", from), ("to", state));
        await Database.Execute(c, "INSERT INTO outbox_messages(outbox_id,tenant_id,event_type,payload) VALUES(@id,@t,'alert',CAST(@p AS jsonb))", token, ("id", Guid.NewGuid()), ("t", tenant), ("p", JsonSerializer.Serialize(new
        {
            alertId = alert,
            state
        })));
        return TransitionOutcome.Updated;
    }, ct);
    public Task<List<Dictionary<string, object?>>> SnapshotAsync(Guid tenant, CancellationToken ct) => database.ExecuteAsync(tenant, (c, token) => Database.Query(c, "SELECT alert_id AS id,status AS state,message,created_at,updated_at FROM alerts ORDER BY updated_at DESC,alert_id DESC LIMIT 100", token), ct);
}
