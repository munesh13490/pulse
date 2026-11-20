using Npgsql;

namespace Pulse.Infrastructure.Repositories;

public sealed class LocalIdentityRepository(Database db) : ILocalIdentityRepository
{
    public async Task<UserIdentity?> FindAsync(Guid id, CancellationToken ct)
    {
        await using var connection = await db.Auth(ct);
        return await ReadUserAsync(connection, id, ct);
    }

    public async Task<TokenPair?> LoginAsync(string username, Func<string?, bool> verifyPassword, Func<UserIdentity, TokenPair> issueTokens, CancellationToken ct)
    {
        await using var connection = await db.Auth(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var rows = await Database.Query(connection, "SELECT u.user_id AS id,u.password_hash,u.failed_login_count,u.lockout_until FROM app_users u JOIN roles r ON r.role_id=u.role_id WHERE u.normalized_email=@name AND u.is_active AND r.is_active ORDER BY u.user_id LIMIT 2 FOR UPDATE OF u", ct, ("name", username.ToUpperInvariant()));
        if (rows.Count != 1)
        {
            verifyPassword(null);
            return null;
        }

        var row = rows[0];
        var id = (Guid)row["id"]!;
        if (row["lockout_until"] is DateTime locked && locked > DateTime.UtcNow)
            return null;
        if (!verifyPassword((string)row["password_hash"]!))
        {
            await Database.Execute(connection, "UPDATE app_users SET failed_login_count=failed_login_count+1,lockout_until=CASE WHEN failed_login_count>=4 THEN now()+interval '5 minutes' ELSE lockout_until END WHERE user_id=@id", ct, ("id", id));
            await transaction.CommitAsync(ct);
            return null;
        }

        await Database.Execute(connection, "UPDATE app_users SET failed_login_count=0,lockout_until=NULL WHERE user_id=@id", ct, ("id", id));
        var user = await ReadUserAsync(connection, id, ct);
        if (user is null)
            return null;
        var pair = issueTokens(user);
        await StoreRefreshTokenAsync(connection, user.Id, pair.RefreshToken, ct);
        await transaction.CommitAsync(ct);
        return pair;
    }

    public async Task<TokenPair?> RefreshAsync(string tokenHash, Func<UserIdentity, TokenPair> issueTokens, CancellationToken ct)
    {
        await using var connection = await db.Auth(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var rows = await Database.Query(connection, "UPDATE user_refresh_tokens SET revoked_at=now() WHERE token_hash=@hash AND revoked_at IS NULL AND expires_at>now() RETURNING user_id", ct, ("hash", tokenHash));
        if (rows.Count != 1)
            return null;
        var user = await ReadUserAsync(connection, (Guid)rows[0]["user_id"]!, ct);
        if (user is null)
            return null;
        var pair = issueTokens(user);
        await StoreRefreshTokenAsync(connection, user.Id, pair.RefreshToken, ct);
        await transaction.CommitAsync(ct);
        return pair;
    }

    public async Task RevokeAsync(string tokenHash, CancellationToken ct)
    {
        await using var connection = await db.Auth(ct);
        await Database.Execute(connection, "UPDATE user_refresh_tokens SET revoked_at=now() WHERE token_hash=@hash", ct, ("hash", tokenHash));
    }

    private static async Task<UserIdentity?> ReadUserAsync(NpgsqlConnection connection, Guid id, CancellationToken ct)
    {
        var users = await Database.Query(connection, "SELECT u.user_id AS id,u.tenant_id,u.email AS user_name,CASE upper(r.role_code) WHEN 'ADMIN' THEN 'tenant-admin' ELSE lower(replace(r.role_code,'_','-')) END AS role_code FROM app_users u JOIN roles r ON r.role_id=u.role_id WHERE u.user_id=@id AND u.is_active AND r.is_active AND ((upper(replace(r.role_code,'-','_'))='PLATFORM_ADMIN')=(u.tenant_id IS NULL))", ct, ("id", id));
        if (users.Count == 0)
            return null;
        var user = users[0];
        return new((Guid)user["id"]!, (Guid?)user["tenant_id"], (string)user["user_name"]!, [(string)user["role_code"]!]);
    }

    private static Task StoreRefreshTokenAsync(NpgsqlConnection connection, Guid user, string token, CancellationToken ct) => Database.Execute(connection, "INSERT INTO user_refresh_tokens(refresh_token_id,user_id,token_hash,expires_at) VALUES(@id,@uid,@hash,now()+interval '7 days')", ct, ("id", Guid.NewGuid()), ("uid", user), ("hash", KeyMaterial.Hash(token)));
}
