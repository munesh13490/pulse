namespace Pulse.Application.Abstractions.Repositories;

public interface ITenantRepository
{
    Task CreateAsync(Guid id, string name, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ListAsync(CancellationToken ct);
}
