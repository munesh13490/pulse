using Microsoft.AspNetCore.Identity;

namespace Pulse.Infrastructure.Identity;

public sealed class IdentityPasswordService : IPasswordService
{
    private readonly PasswordHasher<string> hasher = new();
    public string Hash(string username, string password) => hasher.HashPassword(username, password);
}
