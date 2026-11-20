using Npgsql;
using System.Text.Json;

namespace Pulse.Infrastructure.Repositories;

public sealed class RuleRepository(TenantDatabase database) : IRuleRepository
{
    public Task<List<Dictionary<string, object?>>> ListAsync(Guid tenant, CancellationToken ct) => database.ExecuteAsync(tenant, (c, token) => Database.Query(c, "SELECT r.rule_id AS id,r.device_id,r.name,r.is_enabled AS enabled,v.rule_version_id AS version_id,v.version_number AS version,COALESCE(v.configuration->>'kind',lower(replace(r.rule_type,'_','-'))) AS kind,COALESCE(v.configuration->>'metric',r.metric_name) AS metric,v.threshold_value AS threshold,v.window_seconds FROM alert_rules r JOIN rule_versions v ON v.tenant_id=r.tenant_id AND v.rule_id=r.rule_id AND v.version_number=r.current_version ORDER BY r.created_at DESC LIMIT 500", token), ct);
    public Task<List<Dictionary<string, object?>>> VersionsAsync(Guid tenant, Guid rule, CancellationToken ct) => database.ExecuteAsync(tenant, (c, token) => Database.Query(c, "SELECT v.rule_version_id AS id,v.tenant_id,v.rule_id,v.version_number AS version,COALESCE(v.configuration->>'kind',lower(replace(r.rule_type,'_','-'))) AS kind,COALESCE(v.configuration->>'metric',r.metric_name) AS metric,v.threshold_value AS threshold,v.window_seconds,v.created_at FROM rule_versions v JOIN alert_rules r USING (tenant_id,rule_id) WHERE v.rule_id=@id ORDER BY v.version_number DESC", token, ("id", rule)), ct);
    public Task CreateAsync(Guid tenant, Guid id, RuleRequest request, CancellationToken ct) => database.ExecuteAsync(tenant, async (c, token) =>
    {
        await Database.Execute(c, "INSERT INTO alert_rules(rule_id,tenant_id,device_id,name,metric_name,rule_type) VALUES(@id,@t,@d,@name,@metric,upper(replace(@kind,'-','_')))", token, ("id", id), ("t", tenant), ("d", request.DeviceId), ("name", request.Name), ("metric", request.Metric), ("kind", request.Kind));
        await AddVersionAsync(c, tenant, id, request, token);
        await Database.Execute(c, "INSERT INTO suppression_policies(suppression_policy_id,tenant_id,rule_id,suppress_for_seconds) VALUES(@id,@t,@rule,@s)", token, ("id", Guid.NewGuid()), ("t", tenant), ("rule", id), ("s", request.SuppressionSeconds));
        return true;
    }, ct);
    public Task<bool> AppendVersionAsync(Guid tenant, Guid id, RuleRequest request, CancellationToken ct) => database.ExecuteAsync(tenant, async (c, token) =>
    {
        var rows = await Database.Query(c, "SELECT rule_id FROM alert_rules WHERE rule_id=@id AND device_id=@d FOR UPDATE", token, ("id", id), ("d", request.DeviceId));
        if (rows.Count == 0)
            return false;
        await AddVersionAsync(c, tenant, id, request, token);
        return true;
    }, ct);
    private static Task AddVersionAsync(NpgsqlConnection c, Guid tenant, Guid rule, RuleRequest req, CancellationToken ct) => Database.Execute(c, "WITH added AS (INSERT INTO rule_versions(rule_version_id,tenant_id,rule_id,version_number,comparison_operator,threshold_value,window_seconds,configuration) SELECT @id,@t,@rule,coalesce(max(version_number),0)+1,'>',@v,@w,jsonb_build_object('kind',CAST(@kind AS text),'metric',CAST(@m AS text)) FROM rule_versions WHERE rule_id=@rule RETURNING version_number) UPDATE alert_rules SET current_version=(SELECT version_number FROM added) WHERE rule_id=@rule", ct, ("id", Guid.NewGuid()), ("t", tenant), ("rule", rule), ("kind", req.Kind), ("m", req.Metric), ("v", req.Threshold), ("w", req.WindowSeconds));
}
