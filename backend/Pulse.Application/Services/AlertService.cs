namespace Pulse.Application.Services;

public sealed class AlertService(IAlertRepository repository) : IAlertService
{
    public Task<AlertPage> ListAsync(Guid tenant, string? state, DateTimeOffset? before, Guid? beforeId, CancellationToken ct)
    {
        if ((before is null) != (beforeId is null))
            throw new ArgumentException("Both cursor components are required.");
        return repository.ListAsync(tenant, state, before, beforeId, ct);
    }

    public Task<List<Dictionary<string, object?>>> TransitionsAsync(Guid tenant, Guid alert, CancellationToken ct) => repository.TransitionsAsync(tenant, alert, ct);
    public Task<TransitionOutcome> TransitionAsync(Guid tenant, Guid user, Guid alert, string state, CancellationToken ct) => repository.TransitionAsync(tenant, user, alert, state, ct);
    public Task<List<Dictionary<string, object?>>> SnapshotAsync(Guid tenant, CancellationToken ct) => repository.SnapshotAsync(tenant, ct);
}
