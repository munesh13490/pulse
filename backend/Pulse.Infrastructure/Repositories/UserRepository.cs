using Npgsql;
using System.Text.Json;

namespace Pulse.Infrastructure.Repositories;

public sealed class UserRepository(Database db) : IUserRepository
{
    public async Task CreateAsync(UserRegistration user, CancellationToken ct)
    {
        await using var c = await db.Auth(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await InsertAsync(c, user, ct);
        await tx.CommitAsync(ct);
    }

    public async Task BootstrapAsync(UserRegistration user, CancellationToken ct)
    {
        await using var c = await db.Auth(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await Database.Execute(c, "SELECT pg_advisory_xact_lock(7343192)", ct);
        if ((await Database.Query(c, "SELECT id FROM app_users LIMIT 1", ct)).Count > 0)
            throw new InvalidOperationException("Bootstrap is allowed only before the first user exists.");
        await InsertAsync(c, user, ct);
        await tx.CommitAsync(ct);
    }

    private static async Task InsertAsync(NpgsqlConnection c, UserRegistration user, CancellationToken ct)
    {
        await Database.Execute(c, "INSERT INTO app_users(id,tenant_id,user_name,normalized_user_name,password_hash,security_stamp,concurrency_stamp) VALUES(@id,@t,@name,@normal,@hash,@stamp,@concurrency)", ct, ("id", user.Id), ("t", user.TenantId), ("name", user.Name), ("normal", user.Name.ToUpperInvariant()), ("hash", user.PasswordHash), ("stamp", Guid.NewGuid().ToString()), ("concurrency", Guid.NewGuid().ToString()));
        await Database.Execute(c, "INSERT INTO app_user_roles(id,user_id,role_id) SELECT @id,@uid,id FROM app_roles WHERE name=@role", ct, ("id", Guid.NewGuid()), ("uid", user.Id), ("role", user.Role));
    }

    public async Task<List<Dictionary<string, object?>>> ListAsync(UserIdentity actor, CancellationToken ct)
    {
        await using var c = await db.Auth(ct);
        return await Database.Query(c, "SELECT u.id,u.tenant_id,u.user_name,u.disabled,u.created_at,array_agg(r.name) AS roles FROM app_users u JOIN app_user_roles ur ON ur.user_id=u.id JOIN app_roles r ON r.id=ur.role_id WHERE (@platform OR u.tenant_id=@tenant) GROUP BY u.id ORDER BY u.created_at LIMIT 500", ct, ("platform", actor.Roles.Contains("platform-admin")), ("tenant", actor.TenantId ?? Guid.Empty));
    }

    public async Task<bool> UpdateAsync(UserIdentity actor, Guid id, string? name, bool disable, CancellationToken ct)
    {
        await using var c = await db.Auth(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        var count = await Database.Execute(c, "UPDATE app_users SET user_name=coalesce(@name,user_name),normalized_user_name=coalesce(@normal,normalized_user_name),disabled=CASE WHEN @disable THEN true ELSE disabled END WHERE id=@id AND (@platform OR tenant_id=@tenant)", ct, ("name", name), ("normal", name?.ToUpperInvariant()), ("disable", disable), ("id", id), ("platform", actor.Roles.Contains("platform-admin")), ("tenant", actor.TenantId ?? Guid.Empty));
        if (disable && count > 0)
            await Database.Execute(c, "UPDATE user_refresh_tokens SET revoked_at=now() WHERE user_id=@id", ct, ("id", id));
        await tx.CommitAsync(ct);
        return count > 0;
    }
}
