namespace Pulse.Application.Abstractions.Repositories;

public interface IRuleRepository
{
    Task<List<Dictionary<string, object?>>> ListAsync(Guid tenant, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> VersionsAsync(Guid tenant, Guid rule, CancellationToken ct);
    Task CreateAsync(Guid tenant, Guid id, RuleRequest request, CancellationToken ct);
    Task<bool> AppendVersionAsync(Guid tenant, Guid id, RuleRequest request, CancellationToken ct);
}
