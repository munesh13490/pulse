namespace Pulse.Application.Abstractions.Repositories;
/// <summary>Keeps credential checks and refresh rotation inside the transaction that locks the credentials.</summary>
public interface ILocalIdentityRepository
{
    Task<UserIdentity?> FindAsync(Guid id, CancellationToken ct);
    Task<TokenPair?> LoginAsync(string username, Func<string?, bool> verifyPassword, Func<UserIdentity, TokenPair> issueTokens, CancellationToken ct);
    Task<TokenPair?> RefreshAsync(string tokenHash, Func<UserIdentity, TokenPair> issueTokens, CancellationToken ct);
    Task RevokeAsync(string tokenHash, CancellationToken ct);
}
