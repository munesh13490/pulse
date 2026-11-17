using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Pulse.Application;
namespace Pulse.Infrastructure;

public sealed class EnvironmentSecretProvider(IConfiguration configuration) : ISecretProvider
{
    public string Get(string name) => configuration[$"Secrets:{name}"] is { Length: >= 32 } value ? value :
        throw new InvalidOperationException($"Secrets:{name} must contain at least 32 characters; configure environment or user-secrets.");
}
public sealed class JwtTokenIssuer(ISecretProvider secrets) : ITokenIssuer
{
    public string Issue(UserIdentity user)
    {
        var claims = new List<Claim> { new("sub", user.Id.ToString()), new("name", user.Name) };
        if (user.TenantId is { } tenant) claims.Add(new("tenant_id", tenant.ToString()));
        claims.AddRange(user.Roles.Select(r => new Claim("role", r)));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("pulse-local", "pulse-dashboard", claims,
            DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15), new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secrets.Get("JwtSigningKey"))), SecurityAlgorithms.HmacSha256)));
    }
}
public static class KeyMaterial
{
    public static string Random() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    public static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    public static byte[] Digest(string secret, string pepper) => HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper), Encoding.UTF8.GetBytes(secret));
}
public sealed class PostgreSqlDeviceCredentialValidator(Database db, ISecretProvider secrets) : IDeviceCredentialValidator
{
    public async Task<DeviceIdentity?> Validate(string key, CancellationToken ct)
    {
        var parts = key.Split('.');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var id) || parts[1].Length != 64) return null;
        await using var c = await db.Auth(ct);
        var rows = await Database.Query(c, "SELECT * FROM lookup_device_key(@id)", ct, ("id", id));
        if (rows.Count != 1) return null;
        var row = rows[0];
        return CryptographicOperations.FixedTimeEquals((byte[])row["digest"]!, KeyMaterial.Digest(parts[1], secrets.Get("ApiKeyHmacKey")))
            ? new DeviceIdentity((Guid)row["tenant_id"]!, (Guid)row["device_id"]!) : null;
    }
}
