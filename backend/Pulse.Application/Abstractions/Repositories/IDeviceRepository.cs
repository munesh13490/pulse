namespace Pulse.Application.Abstractions.Repositories;

public interface IDeviceRepository
{
    Task<List<Dictionary<string, object?>>> ListAsync(Guid tenant, CancellationToken ct);
    Task<Dictionary<string, object?>?> GetAsync(Guid tenant, Guid id, CancellationToken ct);
    Task CreateAsync(Guid tenant, Guid id, string name, CancellationToken ct);
    Task<bool> UpdateAsync(Guid tenant, Guid id, string name, bool enabled, CancellationToken ct);
}
