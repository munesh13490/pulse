namespace Pulse.Tests.Unit.Fakes;

internal sealed class RecordingUserRepository : IUserRepository
{
    public List<UserRegistration> Created { get; } = [];
    public int UpdateCount
    {
        get; private set;
    }

    public Task CreateAsync(UserRegistration user, CancellationToken ct)
    {
        Created.Add(user);
        return Task.CompletedTask;
    }

    public Task BootstrapAsync(UserRegistration user, CancellationToken ct) => CreateAsync(user, ct);
    public Task<List<Dictionary<string, object?>>> ListAsync(UserIdentity actor, CancellationToken ct) => Task.FromResult(new List<Dictionary<string, object?>>());
    public Task<bool> UpdateAsync(UserIdentity actor, Guid id, string? name, bool disable, CancellationToken ct)
    {
        UpdateCount++;
        return Task.FromResult(true);
    }
}
