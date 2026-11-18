namespace Pulse.Application.Abstractions.Services;

public interface ITenantRateLimitService
{
    Task<TenantLimits> GetAsync(Guid tenant, CancellationToken ct);
    Task UpdateAsync(Guid tenant, TenantLimits limits, CancellationToken ct);
}
