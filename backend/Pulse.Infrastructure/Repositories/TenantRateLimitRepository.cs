using Npgsql;
using System.Text.Json;

namespace Pulse.Infrastructure.Repositories;

public sealed class TenantRateLimitRepository(TenantDatabase database) : ITenantRateLimitRepository
{
    public Task EnsureAsync(Guid tenant, CancellationToken ct) => database.ExecuteAsync(tenant, (c, token) => Database.Execute(c, "INSERT INTO tenant_rate_limits(tenant_id) VALUES(@t) ON CONFLICT(tenant_id) DO NOTHING", token, ("t", tenant)), ct);
    public Task<TenantLimits?> GetAsync(Guid tenant, CancellationToken ct) => database.ExecuteAsync<TenantLimits?>(tenant, async (c, token) =>
    {
        var rows = await Database.Query(c, "SELECT requests_per_minute,events_per_minute FROM tenant_rate_limits", token);
        return rows.Count == 0 ? null : new((int)rows[0]["requests_per_minute"]!, (int)rows[0]["events_per_minute"]!);
    }, ct);
    public Task UpdateAsync(Guid tenant, TenantLimits limits, CancellationToken ct) => database.ExecuteAsync(tenant, (c, token) => Database.Execute(c, "INSERT INTO tenant_rate_limits(tenant_id,requests_per_minute,events_per_minute) VALUES(@t,@r,@e) ON CONFLICT(tenant_id) DO UPDATE SET requests_per_minute=@r,events_per_minute=@e", token, ("t", tenant), ("r", limits.RequestsPerMinute), ("e", limits.EventsPerMinute)), ct);
}
