using Pulse.Domain;

namespace Pulse.Application.Abstractions.Services;

public interface IUserIdentityService
{
    Task<TokenPair?> Login(string username, string password, CancellationToken ct);
    Task<TokenPair?> Refresh(string token, CancellationToken ct);
    Task Logout(string token, CancellationToken ct);
    Task<UserIdentity?> Find(Guid id, CancellationToken ct);
}
