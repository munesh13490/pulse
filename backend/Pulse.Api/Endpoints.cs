using System.Security.Claims;
using Pulse.Application;
using Pulse.Domain;
using Pulse.Infrastructure;
namespace Pulse.Api;

public sealed record LoginRequest(string Username, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record NameRequest(string Name);
public sealed record UserRequest(Guid? TenantId, string Username, string Password, string Role);
public sealed record UserPatch(string? Username);
public sealed record DevicePatch(string Name, bool Enabled);
public sealed record KeyRequest(DateTimeOffset ExpiresAt);
public sealed record LimitsRequest(int RequestsPerMinute, int EventsPerMinute);
public sealed record TelemetryRequest(List<TelemetryItem>? Items);
public static class Endpoints
{
    public static Guid Tenant(HttpContext ctx) => Guid.Parse(ctx.User.FindFirstValue("tenant_id") ?? throw new UnauthorizedAccessException());
    public static Guid UserId(HttpContext ctx) => Guid.Parse(ctx.User.FindFirstValue("sub")!);
    public static async Task<UserIdentity> Actor(HttpContext ctx, IUserIdentityService ids, CancellationToken ct) => await ids.Find(UserId(ctx), ct) ?? throw new UnauthorizedAccessException();
    public static void MapPulse(this WebApplication app)
    {
        app.MapPost("/v1/auth/login", async (LoginRequest req, IUserIdentityService ids, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || req.Username.Length > 200 || string.IsNullOrEmpty(req.Password) || req.Password.Length > 256) return Results.BadRequest();
            var pair = await ids.Login(req.Username, req.Password, ct); return pair is null ? Results.Unauthorized() : Results.Ok(pair);
        }).Produces<TokenPair>().Produces(401);
        app.MapPost("/v1/auth/refresh", async (RefreshRequest req, IUserIdentityService ids, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken) || req.RefreshToken.Length > 256) return Results.BadRequest();
            var pair = await ids.Refresh(req.RefreshToken, ct); return pair is null ? Results.Unauthorized() : Results.Ok(pair);
        }).Produces<TokenPair>().Produces(401);
        app.MapPost("/v1/auth/logout", async (RefreshRequest req, IUserIdentityService ids, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken) || req.RefreshToken.Length > 256) return Results.BadRequest();
            await ids.Logout(req.RefreshToken, ct); return Results.NoContent();
        });
        app.MapGet("/v1/auth/me", async (HttpContext ctx, IUserIdentityService ids, CancellationToken ct) => Results.Ok(await Actor(ctx, ids, ct))).RequireAuthorization();
        app.MapPost("/v1/admin/tenants", async (NameRequest req, Administration admin, CancellationToken ct) =>
        {
            var id = await admin.CreateTenant(req.Name, ct); await admin.EnsureQuota(id, ct); return Results.Created($"/v1/admin/tenants/{id}", new { id, name = req.Name });
        }).RequireAuthorization("platform");
        app.MapGet("/v1/admin/tenants", async (Database db, CancellationToken ct) =>
        {
            await using var c = await db.Auth(ct); return Results.Ok(await Database.Query(c, "SELECT id,name,created_at FROM tenants ORDER BY created_at LIMIT 500", ct));
        }).RequireAuthorization("platform");
        app.MapPost("/v1/admin/users", async (UserRequest req, HttpContext ctx, Administration admin, IUserIdentityService ids, CancellationToken ct) =>
        {
            var id = await admin.CreateUser(await Actor(ctx, ids, ct), req.TenantId, req.Username, req.Password, req.Role, ct);
            return Results.Created($"/v1/admin/users/{id}", new { id });
        }).RequireAuthorization("admin");
        app.MapGet("/v1/admin/users", async (HttpContext ctx, Administration admin, IUserIdentityService ids, CancellationToken ct) => Results.Ok(await admin.Users(await Actor(ctx, ids, ct), ct))).RequireAuthorization("admin");
        app.MapPatch("/v1/admin/users/{id:guid}", async (Guid id, UserPatch req, HttpContext ctx, Administration admin, IUserIdentityService ids, CancellationToken ct) =>
            await admin.UpdateUser(await Actor(ctx, ids, ct), id, req.Username, false, ct) ? Results.NoContent() : Results.NotFound()).RequireAuthorization("admin");
        app.MapPost("/v1/admin/users/{id:guid}/disable", async (Guid id, HttpContext ctx, Administration admin, IUserIdentityService ids, CancellationToken ct) =>
            await admin.UpdateUser(await Actor(ctx, ids, ct), id, null, true, ct) ? Results.NoContent() : Results.NotFound()).RequireAuthorization("admin");
        app.MapGet("/v1/devices", async (HttpContext ctx, Database db, CancellationToken ct) =>
        {
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, Tenant(ctx), ct);
            return Results.Ok(await Database.Query(c, "SELECT id,name,enabled,created_at FROM devices ORDER BY created_at DESC LIMIT 500", ct));
        }).RequireAuthorization("tenant");
        app.MapGet("/v1/devices/{id:guid}", async (Guid id, HttpContext ctx, Database db, CancellationToken ct) =>
        {
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, Tenant(ctx), ct);
            var rows = await Database.Query(c, "SELECT id,name,enabled,created_at FROM devices WHERE id=@id", ct, ("id", id));
            return rows.Count == 0 ? Results.NotFound() : Results.Ok(rows[0]);
        }).RequireAuthorization("tenant");
        app.MapPost("/v1/devices", async (NameRequest req, HttpContext ctx, Database db, CancellationToken ct) =>
        {
            Administration.ValidateName(req.Name); var id = Guid.NewGuid();
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, Tenant(ctx), ct);
            await Database.Execute(c, "INSERT INTO devices(id,tenant_id,name) VALUES(@id,@t,@name)", ct, ("id", id), ("t", Tenant(ctx)), ("name", req.Name));
            await tx.CommitAsync(ct); return Results.Created($"/v1/devices/{id}", new { id, name = req.Name });
        }).RequireAuthorization("operate");
        app.MapPatch("/v1/devices/{id:guid}", async (Guid id, DevicePatch req, HttpContext ctx, Database db, CancellationToken ct) =>
        {
            Administration.ValidateName(req.Name);
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, Tenant(ctx), ct);
            var count = await Database.Execute(c, "UPDATE devices SET name=@name,enabled=@enabled WHERE id=@id", ct, ("name", req.Name), ("enabled", req.Enabled), ("id", id));
            await tx.CommitAsync(ct); return count == 0 ? Results.NotFound() : Results.NoContent();
        }).RequireAuthorization("operate");
        app.MapPost("/v1/devices/{id:guid}/keys", async (Guid id, KeyRequest req, HttpContext ctx, Administration admin, CancellationToken ct) =>
        {
            var key = await admin.IssueKey(Tenant(ctx), id, req.ExpiresAt, ct); ctx.Response.Headers.CacheControl = "no-store";
            return key is null ? Results.NotFound() : Results.Ok(key);
        }).RequireAuthorization("operate");
        app.MapGet("/v1/devices/{id:guid}/keys", async (Guid id, HttpContext ctx, Database db, CancellationToken ct) =>
        {
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, Tenant(ctx), ct);
            return Results.Ok(await Database.Query(c, "SELECT id,expires_at,revoked_at,created_at FROM device_api_keys WHERE device_id=@id ORDER BY created_at DESC", ct, ("id", id)));
        }).RequireAuthorization("operate");
        app.MapPost("/v1/devices/{id:guid}/keys/{keyId:guid}/revoke", async (Guid id, Guid keyId, HttpContext ctx, Database db, CancellationToken ct) =>
        {
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, Tenant(ctx), ct);
            var count = await Database.Execute(c, "UPDATE device_api_keys SET revoked_at=coalesce(revoked_at,now()) WHERE id=@key AND device_id=@id", ct, ("key", keyId), ("id", id));
            await tx.CommitAsync(ct); return count == 0 ? Results.NotFound() : Results.NoContent();
        }).RequireAuthorization("operate");
        app.MapGet("/v1/admin/tenant-rate-limits", async (HttpContext ctx, Database db, CancellationToken ct) =>
        {
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, Tenant(ctx), ct);
            return Results.Ok(await Database.Query(c, "SELECT requests_per_minute,events_per_minute FROM tenant_rate_limits", ct));
        }).RequireAuthorization("tenant-admin");
        app.MapPatch("/v1/admin/tenant-rate-limits", async (LimitsRequest req, HttpContext ctx, Database db, CancellationToken ct) =>
        {
            if (req.RequestsPerMinute is < 1 or > 100000 || req.EventsPerMinute is < 1 or > 1000000) return Results.BadRequest();
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, Tenant(ctx), ct);
            await Database.Execute(c, "INSERT INTO tenant_rate_limits(id,tenant_id,requests_per_minute,events_per_minute) VALUES(@id,@t,@r,@e) ON CONFLICT(tenant_id) DO UPDATE SET requests_per_minute=@r,events_per_minute=@e", ct,
                ("id", Guid.NewGuid()), ("t", Tenant(ctx)), ("r", req.RequestsPerMinute), ("e", req.EventsPerMinute));
            await tx.CommitAsync(ct); return Results.NoContent();
        }).RequireAuthorization("tenant-admin");
        app.MapPost("/v1/telemetry", async (TelemetryRequest req, HttpContext ctx, IDeviceCredentialValidator credentials, IRequestRateLimiter requests, IEventRateLimiter events, ITelemetryRepository repository, Database db, CancellationToken ct) =>
        {
            if (req.Items is null || req.Items.Count is < 1 or > 500 || req.Items.Any(i => i is null)) return Results.BadRequest(new { error = "items must contain 1-500 objects" });
            var device = await credentials.Validate(ctx.Request.Headers["X-Device-Key"].ToString(), ct); if (device is null) return Results.Unauthorized();
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, device.TenantId, ct);
            var limits = await Database.Query(c, "SELECT requests_per_minute,events_per_minute FROM tenant_rate_limits", ct);
            var defaults = ctx.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitingOptions>>().Value;
            var r = limits.Count > 0 ? (int)limits[0]["requests_per_minute"]! : defaults.RequestsPerMinute; var e = limits.Count > 0 ? (int)limits[0]["events_per_minute"]! : defaults.EventsPerMinute;
            if (!await requests.Allow(device.TenantId, 1, r) || !await events.Allow(device.TenantId, req.Items.Count, e))
            { ctx.Response.Headers.RetryAfter = "60"; return Results.StatusCode(429); }
            return Results.Ok(new { items = await repository.Ingest(device, req.Items, ct) });
        });
        app.MapAlertEndpoints();
    }
}
