using Npgsql;
using System.Text.Json;

namespace Pulse.Infrastructure.Repositories;

public sealed class RuleRepository(TenantDatabase database) : IRuleRepository
{
    public Task<List<Dictionary<string, object?>>> ListAsync(Guid tenant, CancellationToken ct) => database.ExecuteAsync(tenant, (c, token) => Database.Query(c, "SELECT r.id,r.device_id,r.name,r.enabled,v.id AS version_id,v.version,v.kind,v.metric,v.threshold,v.window_seconds FROM alert_rules r JOIN LATERAL (SELECT * FROM rule_versions WHERE rule_id=r.id ORDER BY version DESC LIMIT 1) v ON true ORDER BY r.created_at DESC LIMIT 500", token), ct);
    public Task<List<Dictionary<string, object?>>> VersionsAsync(Guid tenant, Guid rule, CancellationToken ct) => database.ExecuteAsync(tenant, (c, token) => Database.Query(c, "SELECT * FROM rule_versions WHERE rule_id=@id ORDER BY version DESC", token, ("id", rule)), ct);
    public Task CreateAsync(Guid tenant, Guid id, RuleRequest request, CancellationToken ct) => database.ExecuteAsync(tenant, async (c, token) =>
    {
        await Database.Execute(c, "INSERT INTO alert_rules(id,tenant_id,device_id,name) VALUES(@id,@t,@d,@name)", token, ("id", id), ("t", tenant), ("d", request.DeviceId), ("name", request.Name));
        await AddVersionAsync(c, tenant, id, request, token);
        await Database.Execute(c, "INSERT INTO suppression_policies(id,tenant_id,rule_id,seconds) VALUES(@id,@t,@rule,@s)", token, ("id", Guid.NewGuid()), ("t", tenant), ("rule", id), ("s", request.SuppressionSeconds));
        return true;
    }, ct);
    public Task<bool> AppendVersionAsync(Guid tenant, Guid id, RuleRequest request, CancellationToken ct) => database.ExecuteAsync(tenant, async (c, token) =>
    {
        var rows = await Database.Query(c, "SELECT id FROM alert_rules WHERE id=@id AND device_id=@d FOR UPDATE", token, ("id", id), ("d", request.DeviceId));
        if (rows.Count == 0)
            return false;
        await AddVersionAsync(c, tenant, id, request, token);
        return true;
    }, ct);
    private static Task AddVersionAsync(NpgsqlConnection c, Guid tenant, Guid rule, RuleRequest req, CancellationToken ct) => Database.Execute(c, "INSERT INTO rule_versions(id,tenant_id,rule_id,version,kind,metric,threshold,window_seconds) SELECT @id,@t,@rule,coalesce(max(version),0)+1,@kind,@m,@v,@w FROM rule_versions WHERE rule_id=@rule", ct, ("id", Guid.NewGuid()), ("t", tenant), ("rule", rule), ("kind", req.Kind), ("m", req.Metric), ("v", req.Threshold), ("w", req.WindowSeconds));
}
