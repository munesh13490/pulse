namespace Pulse.Application.Abstractions.Services;

public interface IUserService
{
    Task<Guid> CreateAsync(UserIdentity actor, Guid? tenant, string name, string password, string role, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ListAsync(UserIdentity actor, CancellationToken ct);
    Task<bool> UpdateAsync(UserIdentity actor, Guid id, string? name, bool disable, CancellationToken ct);
}
