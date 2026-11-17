using System.Text.Json;
using Pulse.Application;
using Pulse.Domain;
using Pulse.Infrastructure;
using StackExchange.Redis;
namespace Pulse.Api;

public sealed record RuleRequest(Guid DeviceId, string Name, string Kind, string Metric, double Threshold, int WindowSeconds, int SuppressionSeconds);
public sealed record TransitionRequest(string State);
public static class AlertEndpoints
{
    public static void MapAlertEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/rules", async (HttpContext ctx, Database db, CancellationToken ct) =>
        {
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, Endpoints.Tenant(ctx), ct);
            return Results.Ok(await Database.Query(c, "SELECT r.id,r.device_id,r.name,r.enabled,v.id AS version_id,v.version,v.kind,v.metric,v.threshold,v.window_seconds FROM alert_rules r JOIN LATERAL (SELECT * FROM rule_versions WHERE rule_id=r.id ORDER BY version DESC LIMIT 1) v ON true ORDER BY r.created_at DESC LIMIT 500", ct));
        }).RequireAuthorization("tenant");
        app.MapGet("/v1/rules/{id:guid}/versions", async (Guid id, HttpContext ctx, Database db, CancellationToken ct) =>
        {
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, Endpoints.Tenant(ctx), ct);
            return Results.Ok(await Database.Query(c, "SELECT * FROM rule_versions WHERE rule_id=@id ORDER BY version DESC", ct, ("id", id)));
        }).RequireAuthorization("tenant");
        app.MapPost("/v1/rules", async (RuleRequest req, HttpContext ctx, Database db, CancellationToken ct) =>
        {
            Validate(req); var id = Guid.NewGuid(); var tenant = Endpoints.Tenant(ctx);
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, tenant, ct);
            await Database.Execute(c, "INSERT INTO alert_rules(id,tenant_id,device_id,name) VALUES(@id,@t,@d,@name)", ct, ("id", id), ("t", tenant), ("d", req.DeviceId), ("name", req.Name));
            await AddVersion(c, tenant, id, req, ct);
            await Database.Execute(c, "INSERT INTO suppression_policies(id,tenant_id,rule_id,seconds) VALUES(@id,@t,@rule,@s)", ct, ("id", Guid.NewGuid()), ("t", tenant), ("rule", id), ("s", req.SuppressionSeconds));
            await tx.CommitAsync(ct); return Results.Created($"/v1/rules/{id}", new { id });
        }).RequireAuthorization("operate");
        app.MapPost("/v1/rules/{id:guid}/versions", async (Guid id, RuleRequest req, HttpContext ctx, Database db, CancellationToken ct) =>
        {
            Validate(req); var tenant = Endpoints.Tenant(ctx);
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, tenant, ct);
            var rows = await Database.Query(c, "SELECT id FROM alert_rules WHERE id=@id AND device_id=@d FOR UPDATE", ct, ("id", id), ("d", req.DeviceId));
            if (rows.Count == 0) return Results.NotFound();
            await AddVersion(c, tenant, id, req, ct); await tx.CommitAsync(ct); return Results.NoContent();
        }).RequireAuthorization("operate");
        app.MapGet("/v1/telemetry", async (Guid deviceId, HttpContext ctx, Database db, CancellationToken ct) =>
        {
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, Endpoints.Tenant(ctx), ct);
            return Results.Ok(await Database.Query(c, "SELECT event_id,metric,value,occurred_at FROM telemetry_events WHERE device_id=@d ORDER BY occurred_at DESC LIMIT 500", ct, ("d", deviceId)));
        }).RequireAuthorization("tenant");
        app.MapGet("/v1/alerts", async (string? state, DateTimeOffset? before, Guid? beforeId, HttpContext ctx, Database db, CancellationToken ct) =>
        {
            if ((before is null) != (beforeId is null)) return Results.BadRequest();
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, Endpoints.Tenant(ctx), ct);
            var rows = await Database.Query(c, "SELECT id,device_id,rule_version_id,state,message,created_at,updated_at FROM alerts WHERE (@state='' OR state=@state) AND (created_at,id)<(@at,@id) ORDER BY created_at DESC,id DESC LIMIT 100", ct,
                ("state", state ?? ""), ("at", before ?? DateTimeOffset.UtcNow.AddDays(1)), ("id", beforeId ?? Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")));
            return Results.Ok(new { items = rows, next = rows.Count == 100 ? new { before = rows[^1]["created_at"], beforeId = rows[^1]["id"] } : null });
        }).RequireAuthorization("tenant");
        app.MapGet("/v1/alerts/{id:guid}/transitions", async (Guid id, HttpContext ctx, Database db, CancellationToken ct) =>
        {
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, Endpoints.Tenant(ctx), ct);
            return Results.Ok(await Database.Query(c, "SELECT id,from_state,to_state,user_id,created_at FROM alert_transitions WHERE alert_id=@id ORDER BY created_at,id", ct, ("id", id)));
        }).RequireAuthorization("tenant");
        app.MapPost("/v1/alerts/{id:guid}/transitions", async (Guid id, TransitionRequest req, HttpContext ctx, Database db, CancellationToken ct) =>
        {
            var tenant = Endpoints.Tenant(ctx);
            await using var c = await db.App(ct); await using var tx = await Database.Scope(c, tenant, ct);
            var rows = await Database.Query(c, "SELECT state FROM alerts WHERE id=@id FOR UPDATE", ct, ("id", id));
            if (rows.Count == 0) return Results.NotFound();
            var from = (string)rows[0]["state"]!;
            if (!AlertState.CanTransition(from, req.State)) return Results.Conflict(new { error = "Invalid state transition" });
            await Database.Execute(c, "UPDATE alerts SET state=@state,updated_at=now() WHERE id=@id", ct, ("state", req.State), ("id", id));
            await Database.Execute(c, "INSERT INTO alert_transitions(id,tenant_id,alert_id,user_id,from_state,to_state) VALUES(@id,@t,@alert,@u,@from,@to)", ct,
                ("id", Guid.NewGuid()), ("t", tenant), ("alert", id), ("u", Endpoints.UserId(ctx)), ("from", from), ("to", req.State));
            await Database.Execute(c, "INSERT INTO outbox_messages(id,tenant_id,kind,payload) VALUES(@id,@t,'alert',CAST(@p AS jsonb))", ct,
                ("id", Guid.NewGuid()), ("t", tenant), ("p", JsonSerializer.Serialize(new { alertId = id, state = req.State })));
            await tx.CommitAsync(ct); return Results.NoContent();
        }).RequireAuthorization("operate");
        app.MapGet("/v1/alerts/stream", Stream).RequireAuthorization("tenant");
    }
    private static void Validate(RuleRequest req)
    {
        Administration.ValidateName(req.Name);
        if (req.Kind is not ("threshold" or "rate-of-change") || string.IsNullOrWhiteSpace(req.Metric) || req.Metric.Length > 128 || !double.IsFinite(req.Threshold) ||
            req.WindowSeconds is < 1 or > 259200 || req.SuppressionSeconds is < 0 or > 259200) throw new ArgumentException("Invalid rule configuration.");
    }
    private static async Task AddVersion(Npgsql.NpgsqlConnection c, Guid tenant, Guid rule, RuleRequest req, CancellationToken ct)
    {
        await Database.Execute(c, "INSERT INTO rule_versions(id,tenant_id,rule_id,version,kind,metric,threshold,window_seconds) SELECT @id,@t,@rule,coalesce(max(version),0)+1,@kind,@m,@v,@w FROM rule_versions WHERE rule_id=@rule", ct,
            ("id", Guid.NewGuid()), ("t", tenant), ("rule", rule), ("kind", req.Kind), ("m", req.Metric), ("v", req.Threshold), ("w", req.WindowSeconds));
    }
    private static async Task Stream(HttpContext ctx, Database db, IConnectionMultiplexer redis, IUserIdentityService identities, CancellationToken ct)
    {
        var tenant = Endpoints.Tenant(ctx);
        var expires = DateTimeOffset.FromUnixTimeSeconds(long.Parse(ctx.User.FindFirst("exp")!.Value));
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";
        // Redis is a wakeup hint. Periodic durable snapshots recover missed publications and reconnects.
        using var signal = new SemaphoreSlim(0, 1);
        var channel = RedisChannel.Literal($"pulse:alerts:{tenant}");
        var subscriber = redis.GetSubscriber();
        Action<RedisChannel, RedisValue> handler = (_, _) => { try { signal.Release(); } catch (SemaphoreFullException) { } };
        await subscriber.SubscribeAsync(channel, handler);
        try
        {
            while (!ct.IsCancellationRequested && DateTimeOffset.UtcNow < expires)
            {
                var identity = await identities.Find(Endpoints.UserId(ctx), ct);
                if (identity is null || identity.TenantId != tenant || !identity.Roles.Any(r => r is "tenant-admin" or "operator" or "viewer")) break;
                await using var c = await db.App(ct); await using var tx = await Database.Scope(c, tenant, ct);
                var rows = await Database.Query(c, "SELECT id,state,message,created_at,updated_at FROM alerts ORDER BY updated_at DESC,id DESC LIMIT 100", ct);
                await ctx.Response.WriteAsync($"event: snapshot\ndata: {JsonSerializer.Serialize(rows)}\n\n", ct);
                await ctx.Response.Body.FlushAsync(ct);
                await signal.WaitAsync(TimeSpan.FromSeconds(15), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { await subscriber.UnsubscribeAsync(channel, handler); }
    }
}
