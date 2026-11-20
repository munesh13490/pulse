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
        if ((await Database.Query(c, "SELECT user_id FROM app_users LIMIT 1", ct)).Count > 0)
            throw new InvalidOperationException("Bootstrap is allowed only before the first user exists.");
        await InsertAsync(c, user, ct);
        await tx.CommitAsync(ct);
    }

    private static async Task InsertAsync(NpgsqlConnection c, UserRegistration user, CancellationToken ct)
    {
        var inserted = await Database.Execute(c, """
            INSERT INTO app_users(user_id,tenant_id,role_id,email,normalized_email,password_hash)
            SELECT @id,@t,role_id,@name,@normal,@hash FROM roles
            WHERE is_active AND (CASE upper(role_code) WHEN 'ADMIN' THEN 'tenant-admin' ELSE lower(replace(role_code,'_','-')) END)=@role
            ORDER BY role_code LIMIT 1
            """, ct, ("id", user.Id), ("t", user.TenantId), ("name", user.Name), ("normal", user.Name.ToUpperInvariant()), ("hash", user.PasswordHash), ("role", user.Role));
        if (inserted == 0)
            throw new InvalidOperationException("The selected role is unavailable.");
    }

    public async Task<List<Dictionary<string, object?>>> ListAsync(UserIdentity actor, CancellationToken ct)
    {
        await using var c = await db.Auth(ct);
        return await Database.Query(c, "SELECT u.user_id AS id,u.tenant_id,u.email AS user_name,NOT u.is_active AS disabled,u.created_at,ARRAY[CASE upper(r.role_code) WHEN 'ADMIN' THEN 'tenant-admin' ELSE lower(replace(r.role_code,'_','-')) END] AS roles FROM app_users u JOIN roles r ON r.role_id=u.role_id WHERE (@platform OR u.tenant_id=@tenant) ORDER BY u.created_at LIMIT 500", ct, ("platform", actor.Roles.Contains("platform-admin")), ("tenant", actor.TenantId ?? Guid.Empty));
    }

    public async Task<bool> UpdateAsync(UserIdentity actor, Guid id, string? name, bool disable, CancellationToken ct)
    {
        await using var c = await db.Auth(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        var count = await Database.Execute(c, "UPDATE app_users SET email=coalesce(@name,email),normalized_email=coalesce(@normal,normalized_email),is_active=is_active AND NOT @disable,updated_at=now() WHERE user_id=@id AND (@platform OR tenant_id=@tenant)", ct, ("name", name), ("normal", name?.ToUpperInvariant()), ("disable", disable), ("id", id), ("platform", actor.Roles.Contains("platform-admin")), ("tenant", actor.TenantId ?? Guid.Empty));
        if (disable && count > 0)
            await Database.Execute(c, "UPDATE user_refresh_tokens SET revoked_at=now() WHERE user_id=@id", ct, ("id", id));
        await tx.CommitAsync(ct);
        return count > 0;
    }
}
