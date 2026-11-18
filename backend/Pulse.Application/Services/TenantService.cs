namespace Pulse.Application.Services;

public sealed class TenantService(ITenantRepository tenants, ITenantRateLimitRepository quotas) : ITenantService
{
    public async Task<Guid> CreateAsync(string name, CancellationToken ct)
    {
        NameValidator.Validate(name);
        var id = Guid.NewGuid();
        await tenants.CreateAsync(id, name, ct);
        await quotas.EnsureAsync(id, ct);
        return id;
    }

    public Task<List<Dictionary<string, object?>>> ListAsync(CancellationToken ct) => tenants.ListAsync(ct);
}
