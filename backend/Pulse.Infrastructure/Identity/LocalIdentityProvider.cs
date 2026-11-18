using Microsoft.AspNetCore.Identity;

namespace Pulse.Infrastructure.Identity;

public sealed class LocalIdentityProvider(ILocalIdentityRepository repository, ITokenIssuer issuer) : IUserIdentityService
{
    private readonly PasswordHasher<string> hasher = new();
    public Task<UserIdentity?> Find(Guid id, CancellationToken ct) => repository.FindAsync(id, ct);
    public Task<TokenPair?> Login(string username, string password, CancellationToken ct) => repository.LoginAsync(username, hash => VerifyPassword(username, password, hash), CreatePair, ct);
    public Task<TokenPair?> Refresh(string token, CancellationToken ct) => repository.RefreshAsync(KeyMaterial.Hash(token), CreatePair, ct);
    public Task Logout(string token, CancellationToken ct) => repository.RevokeAsync(KeyMaterial.Hash(token), ct);
    private bool VerifyPassword(string username, string password, string? hash)
    {
        if (hash is null)
        {
            hasher.VerifyHashedPassword("", hasher.HashPassword("", "dummy-password"), password);
            return false;
        }

        return hasher.VerifyHashedPassword(username, hash, password) != PasswordVerificationResult.Failed;
    }

    private TokenPair CreatePair(UserIdentity user) => new(issuer.Issue(user), KeyMaterial.Random(), DateTimeOffset.UtcNow.AddMinutes(15));
}
