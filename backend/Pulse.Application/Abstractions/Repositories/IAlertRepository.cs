namespace Pulse.Application.Abstractions.Repositories;

public interface IAlertRepository
{
    Task<AlertPage> ListAsync(Guid tenant, string? state, DateTimeOffset? before, Guid? beforeId, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> TransitionsAsync(Guid tenant, Guid alert, CancellationToken ct);
    Task<TransitionOutcome> TransitionAsync(Guid tenant, Guid user, Guid alert, string state, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> SnapshotAsync(Guid tenant, CancellationToken ct);
}
