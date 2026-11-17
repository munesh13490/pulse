using Microsoft.AspNetCore.Identity;
using Pulse.Application;
namespace Pulse.Infrastructure;

public sealed class Administration(Database db, ISecretProvider secretProvider)
{
    public async Task<Guid> CreateTenant(string name, CancellationToken ct)
    {
        ValidateName(name);
        var id = Guid.NewGuid();
        await using var c = await db.Auth(ct);
        await Database.Execute(c, "INSERT INTO tenants(id,name) VALUES(@id,@name)", ct, ("id", id), ("name", name));
        // The quota row is created through the tenant-scoped role.
        return id;
    }
    public async Task EnsureQuota(Guid tenant, CancellationToken ct)
    {
        await using var c = await db.App(ct); await using var tx = await Database.Scope(c, tenant, ct);
        await Database.Execute(c, "INSERT INTO tenant_rate_limits(id,tenant_id) VALUES(@id,@t) ON CONFLICT(tenant_id) DO NOTHING", ct, ("id", Guid.NewGuid()), ("t", tenant));
        await tx.CommitAsync(ct);
    }
    public async Task<Guid> CreateUser(UserIdentity? actor, Guid? tenant, string name, string password, string role, CancellationToken ct)
    {
        ValidateName(name);
        if (string.IsNullOrEmpty(password) || password.Length < 12 || password.Length > 256) throw new ArgumentException("Password must contain 12-256 characters.");
        var platform = actor?.Roles.Contains("platform-admin") == true;
        if (actor is not null && !platform && (!actor.Roles.Contains("tenant-admin") || tenant != actor.TenantId)) throw new UnauthorizedAccessException();
        if (!new[] { "platform-admin", "tenant-admin", "operator", "viewer" }.Contains(role) || role == "platform-admin" && actor is not null && !platform) throw new ArgumentException("Invalid role");
        if ((role == "platform-admin") != (tenant is null)) throw new ArgumentException("Platform administrators have no tenant; tenant users require one.");
        var id = Guid.NewGuid();
        var hash = new PasswordHasher<string>().HashPassword(name, password);
        await using var c = await db.Auth(ct); await using var tx = await c.BeginTransactionAsync(ct);
        await Database.Execute(c, "INSERT INTO app_users(id,tenant_id,user_name,normalized_user_name,password_hash,security_stamp,concurrency_stamp) VALUES(@id,@t,@name,@normal,@hash,@stamp,@concurrency)", ct,
            ("id", id), ("t", tenant), ("name", name), ("normal", name.ToUpperInvariant()), ("hash", hash), ("stamp", Guid.NewGuid().ToString()), ("concurrency", Guid.NewGuid().ToString()));
        await Database.Execute(c, "INSERT INTO app_user_roles(id,user_id,role_id) SELECT @id,@uid,id FROM app_roles WHERE name=@role", ct, ("id", Guid.NewGuid()), ("uid", id), ("role", role));
        await tx.CommitAsync(ct); return id;
    }
    public async Task Bootstrap(string name, string password, CancellationToken ct)
    {
        await using var c = await db.Auth(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await Database.Execute(c, "SELECT pg_advisory_xact_lock(7343192)", ct);
        var rows = await Database.Query(c, "SELECT id FROM app_users LIMIT 1", ct);
        if (rows.Count > 0) throw new InvalidOperationException("Bootstrap is allowed only before the first user exists.");
        // Keep the advisory lock until the separate create transaction has committed.
        await CreateUser(null, null, name, password, "platform-admin", ct);
        await tx.CommitAsync(ct);
    }
    public async Task<List<Dictionary<string, object?>>> Users(UserIdentity actor, CancellationToken ct)
    {
        await using var c = await db.Auth(ct);
        return await Database.Query(c, "SELECT u.id,u.tenant_id,u.user_name,u.disabled,u.created_at,array_agg(r.name) AS roles FROM app_users u JOIN app_user_roles ur ON ur.user_id=u.id JOIN app_roles r ON r.id=ur.role_id WHERE (@platform OR u.tenant_id=@tenant) GROUP BY u.id ORDER BY u.created_at LIMIT 500", ct,
            ("platform", actor.Roles.Contains("platform-admin")), ("tenant", actor.TenantId ?? Guid.Empty));
    }
    public async Task<bool> UpdateUser(UserIdentity actor, Guid id, string? name, bool disable, CancellationToken ct)
    {
        if (name is not null) ValidateName(name);
        if (id == actor.Id && disable) throw new ArgumentException("Cannot disable yourself.");
        await using var c = await db.Auth(ct); await using var tx = await c.BeginTransactionAsync(ct);
        var count = await Database.Execute(c, "UPDATE app_users SET user_name=coalesce(@name,user_name),normalized_user_name=coalesce(@normal,normalized_user_name),disabled=CASE WHEN @disable THEN true ELSE disabled END WHERE id=@id AND (@platform OR tenant_id=@tenant)", ct,
            ("name", name), ("normal", name?.ToUpperInvariant()), ("disable", disable), ("id", id), ("platform", actor.Roles.Contains("platform-admin")), ("tenant", actor.TenantId ?? Guid.Empty));
        if (disable && count > 0) await Database.Execute(c, "UPDATE user_refresh_tokens SET revoked_at=now() WHERE user_id=@id", ct, ("id", id));
        await tx.CommitAsync(ct); return count > 0;
    }
    public async Task<object?> IssueKey(Guid tenant, Guid device, DateTimeOffset expires, CancellationToken ct)
    {
        if (expires <= DateTimeOffset.UtcNow || expires > DateTimeOffset.UtcNow.AddYears(1)) throw new ArgumentException("Expiry must be within one year.");
        await using var c = await db.App(ct); await using var tx = await Database.Scope(c, tenant, ct);
        var rows = await Database.Query(c, "SELECT id FROM devices WHERE id=@id AND enabled FOR UPDATE", ct, ("id", device));
        if (rows.Count == 0) return null;
        await Database.Execute(c, "UPDATE device_api_keys SET revoked_at=now() WHERE device_id=@d AND revoked_at IS NULL", ct, ("d", device));
        var id = Guid.NewGuid(); var secret = KeyMaterial.Random();
        var pepper = secretProvider.Get("ApiKeyHmacKey");
        await Database.Execute(c, "INSERT INTO device_api_keys(id,tenant_id,device_id,digest,expires_at) VALUES(@id,@t,@d,@hash,@expires)", ct,
            ("id", id), ("t", tenant), ("d", device), ("hash", KeyMaterial.Digest(secret, pepper)), ("expires", expires));
        await tx.CommitAsync(ct); return new { id, apiKey = $"{id}.{secret}", expiresAt = expires };
    }
    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) throw new ArgumentException("Name must contain 1-200 characters.");
    }
}
