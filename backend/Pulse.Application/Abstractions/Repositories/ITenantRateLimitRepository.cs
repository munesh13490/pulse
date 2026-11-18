namespace Pulse.Application.Abstractions.Repositories;

public interface ITenantRateLimitRepository
{
    Task EnsureAsync(Guid tenant, CancellationToken ct);
    Task<TenantLimits?> GetAsync(Guid tenant, CancellationToken ct);
    Task UpdateAsync(Guid tenant, TenantLimits limits, CancellationToken ct);
}
