namespace Pulse.Application.Abstractions.Services;

public interface ITenantService
{
    Task<Guid> CreateAsync(string name, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ListAsync(CancellationToken ct);
}
