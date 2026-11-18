namespace Pulse.Application.Abstractions.Repositories;

public interface IUserRepository
{
    Task CreateAsync(UserRegistration user, CancellationToken ct);
    Task BootstrapAsync(UserRegistration user, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ListAsync(UserIdentity actor, CancellationToken ct);
    Task<bool> UpdateAsync(UserIdentity actor, Guid id, string? name, bool disable, CancellationToken ct);
}
