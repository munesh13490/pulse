using Microsoft.AspNetCore.Identity;
using Npgsql;
using Pulse.Application;
namespace Pulse.Infrastructure;

public sealed class LocalIdentityProvider(Database db, ITokenIssuer issuer) : IUserIdentityService
{
    private readonly PasswordHasher<string> hasher = new();
    public async Task<UserIdentity?> Find(Guid id, CancellationToken ct)
    {
        await using var c = await db.Auth(ct);
        return await ReadUser(c, id, ct);
    }
    private static async Task<UserIdentity?> ReadUser(NpgsqlConnection c, Guid id, CancellationToken ct)
    {
        var rows = await Database.Query(c, "SELECT id,tenant_id,user_name FROM app_users WHERE id=@id AND NOT disabled", ct, ("id", id));
        if (rows.Count == 0) return null;
        var roles = await Database.Query(c, "SELECT r.name FROM app_roles r JOIN app_user_roles ur ON ur.role_id=r.id WHERE ur.user_id=@id", ct, ("id", id));
        var u = rows[0];
        return new((Guid)u["id"]!, (Guid?)u["tenant_id"], (string)u["user_name"]!, roles.Select(r => (string)r["name"]!).ToArray());
    }
    public async Task<TokenPair?> Login(string username, string password, CancellationToken ct)
    {
        await using var c = await db.Auth(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        var rows = await Database.Query(c, "SELECT id,password_hash,access_failed_count,lockout_end FROM app_users WHERE normalized_user_name=@name AND NOT disabled FOR UPDATE", ct, ("name", username.ToUpperInvariant()));
        if (rows.Count == 0) { hasher.VerifyHashedPassword("", hasher.HashPassword("", "dummy-password"), password); return null; }
        var row = rows[0]; var id = (Guid)row["id"]!;
        if (row["lockout_end"] is DateTime locked && locked > DateTime.UtcNow) return null;
        var result = hasher.VerifyHashedPassword(username, (string)row["password_hash"]!, password);
        if (result == PasswordVerificationResult.Failed)
        {
            await Database.Execute(c, "UPDATE app_users SET access_failed_count=access_failed_count+1,lockout_end=CASE WHEN access_failed_count>=4 THEN now()+interval '5 minutes' ELSE lockout_end END WHERE id=@id", ct, ("id", id));
            await tx.CommitAsync(ct); return null;
        }
        await Database.Execute(c, "UPDATE app_users SET access_failed_count=0,lockout_end=NULL WHERE id=@id", ct, ("id", id));
        var pair = await CreatePair(c, (await ReadUser(c, id, ct))!, ct);
        await tx.CommitAsync(ct); return pair;
    }
    public async Task<TokenPair?> Refresh(string token, CancellationToken ct)
    {
        await using var c = await db.Auth(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        var rows = await Database.Query(c, "UPDATE user_refresh_tokens SET revoked_at=now() WHERE token_hash=@hash AND revoked_at IS NULL AND expires_at>now() RETURNING user_id", ct, ("hash", KeyMaterial.Hash(token)));
        if (rows.Count == 0) return null;
        var user = await ReadUser(c, (Guid)rows[0]["user_id"]!, ct);
        if (user is null) return null;
        var pair = await CreatePair(c, user, ct);
        await tx.CommitAsync(ct); return pair;
    }
    public async Task Logout(string token, CancellationToken ct)
    {
        await using var c = await db.Auth(ct);
        await Database.Execute(c, "UPDATE user_refresh_tokens SET revoked_at=now() WHERE token_hash=@hash", ct, ("hash", KeyMaterial.Hash(token)));
    }
    private async Task<TokenPair> CreatePair(NpgsqlConnection c, UserIdentity user, CancellationToken ct)
    {
        var secret = KeyMaterial.Random();
        await Database.Execute(c, "INSERT INTO user_refresh_tokens(id,user_id,token_hash,expires_at) VALUES(@id,@uid,@hash,now()+interval '7 days')", ct,
            ("id", Guid.NewGuid()), ("uid", user.Id), ("hash", KeyMaterial.Hash(secret)));
        return new(issuer.Issue(user), secret, DateTimeOffset.UtcNow.AddMinutes(15));
    }
}
