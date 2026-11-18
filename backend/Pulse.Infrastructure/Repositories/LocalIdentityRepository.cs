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
        var rows = await Database.Query(connection, "SELECT id,password_hash,access_failed_count,lockout_end FROM app_users WHERE normalized_user_name=@name AND NOT disabled FOR UPDATE", ct, ("name", username.ToUpperInvariant()));
        if (rows.Count == 0)
        {
            verifyPassword(null);
            return null;
        }

        var row = rows[0];
        var id = (Guid)row["id"]!;
        if (row["lockout_end"] is DateTime locked && locked > DateTime.UtcNow)
            return null;
        if (!verifyPassword((string)row["password_hash"]!))
        {
            await Database.Execute(connection, "UPDATE app_users SET access_failed_count=access_failed_count+1,lockout_end=CASE WHEN access_failed_count>=4 THEN now()+interval '5 minutes' ELSE lockout_end END WHERE id=@id", ct, ("id", id));
            await transaction.CommitAsync(ct);
            return null;
        }

        await Database.Execute(connection, "UPDATE app_users SET access_failed_count=0,lockout_end=NULL WHERE id=@id", ct, ("id", id));
        var user = (await ReadUserAsync(connection, id, ct))!;
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
        if (rows.Count == 0)
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
        var users = await Database.Query(connection, "SELECT id,tenant_id,user_name FROM app_users WHERE id=@id AND NOT disabled", ct, ("id", id));
        if (users.Count == 0)
            return null;
        var roles = await Database.Query(connection, "SELECT r.name FROM app_roles r JOIN app_user_roles ur ON ur.role_id=r.id WHERE ur.user_id=@id", ct, ("id", id));
        var user = users[0];
        return new((Guid)user["id"]!, (Guid?)user["tenant_id"], (string)user["user_name"]!, roles.Select(role => (string)role["name"]!).ToArray());
    }

    private static Task StoreRefreshTokenAsync(NpgsqlConnection connection, Guid user, string token, CancellationToken ct) => Database.Execute(connection, "INSERT INTO user_refresh_tokens(id,user_id,token_hash,expires_at) VALUES(@id,@uid,@hash,now()+interval '7 days')", ct, ("id", Guid.NewGuid()), ("uid", user), ("hash", KeyMaterial.Hash(token)));
}
