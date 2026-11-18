using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Pulse.Application;

namespace Pulse.Infrastructure.Identity;

public sealed class PostgreSqlDeviceCredentialValidator(Database db, ISecretProvider secrets) : IDeviceCredentialValidator
{
    public async Task<DeviceIdentity?> Validate(string key, CancellationToken ct)
    {
        var parts = key.Split('.');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var id) || parts[1].Length != 64)
            return null;
        await using var c = await db.Auth(ct);
        var rows = await Database.Query(c, "SELECT * FROM lookup_device_key(@id)", ct, ("id", id));
        if (rows.Count != 1)
            return null;
        var row = rows[0];
        return CryptographicOperations.FixedTimeEquals((byte[])row["digest"]!, KeyMaterial.Digest(parts[1], secrets.Get("ApiKeyHmacKey"))) ? new DeviceIdentity((Guid)row["tenant_id"]!, (Guid)row["device_id"]!) : null;
    }
}
