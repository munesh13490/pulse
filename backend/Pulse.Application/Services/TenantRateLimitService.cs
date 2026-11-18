namespace Pulse.Application.Services;

public sealed class TenantRateLimitService(ITenantRateLimitRepository repository, TenantLimits defaults) : ITenantRateLimitService
{
    public async Task<TenantLimits> GetAsync(Guid tenant, CancellationToken ct) => await repository.GetAsync(tenant, ct) ?? defaults;
    public Task UpdateAsync(Guid tenant, TenantLimits limits, CancellationToken ct)
    {
        if (limits.RequestsPerMinute is < 1 or > 100000 || limits.EventsPerMinute is < 1 or > 1000000)
            throw new ArgumentException("Invalid tenant quotas.");
        return repository.UpdateAsync(tenant, limits, ct);
    }
}
