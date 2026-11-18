namespace Pulse.Application.Abstractions.Services;

public interface IRuleService
{
    Task<List<Dictionary<string, object?>>> ListAsync(Guid tenant, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> VersionsAsync(Guid tenant, Guid rule, CancellationToken ct);
    Task<Guid> CreateAsync(Guid tenant, RuleRequest request, CancellationToken ct);
    Task<bool> AppendVersionAsync(Guid tenant, Guid id, RuleRequest request, CancellationToken ct);
}
