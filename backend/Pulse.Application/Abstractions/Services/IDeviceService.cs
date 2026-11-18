namespace Pulse.Application.Abstractions.Services;

public interface IDeviceService
{
    Task<List<Dictionary<string, object?>>> ListAsync(Guid tenant, CancellationToken ct);
    Task<Dictionary<string, object?>?> GetAsync(Guid tenant, Guid id, CancellationToken ct);
    Task<Guid> CreateAsync(Guid tenant, string name, CancellationToken ct);
    Task<bool> UpdateAsync(Guid tenant, Guid id, string name, bool enabled, CancellationToken ct);
}
