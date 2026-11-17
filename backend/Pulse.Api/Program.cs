using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Pulse.Application;
using Pulse.Infrastructure;
using Serilog;
using Serilog.Formatting.Compact;
using StackExchange.Redis;
using Pulse.Api;

var builder = WebApplication.CreateBuilder(args.Where(a => a is not ("--export-openapi" or "--migrate" or "--bootstrap")).ToArray());
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
builder.Host.UseSerilog((context, logger) => logger.WriteTo.Console(new RenderedCompactJsonFormatter()));
builder.Services.AddPulse(builder.Configuration);
// JWT and password hashing do not require persisted Data Protection keys.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "pulse-dp", Environment.ProcessId.ToString())))
    .UseEphemeralDataProtectionProvider();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = "pulse-local",
        ValidateAudience = true,
        ValidAudience = "pulse-dashboard",
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromSeconds(10),
        NameClaimType = "name",
        RoleClaimType = "role",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new EnvironmentSecretProvider(builder.Configuration).Get("JwtSigningKey")))
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("platform", p => p.RequireRole("platform-admin"));
    options.AddPolicy("admin", p => p.RequireRole("platform-admin", "tenant-admin"));
    options.AddPolicy("tenant-admin", p => p.RequireRole("tenant-admin"));
    options.AddPolicy("operate", p => p.RequireRole("tenant-admin", "operator"));
    options.AddPolicy("tenant", p => p.RequireClaim("tenant_id").RequireRole("tenant-admin", "operator", "viewer"));
});
var app = builder.Build();
if (args.Contains("--migrate"))
{
    var opts = new DbContextOptionsBuilder<PulseDbContext>().UseNpgsql(builder.Configuration.GetConnectionString("Migration") ?? throw new InvalidOperationException("Set ConnectionStrings__Migration")).Options;
    await using var db = new PulseDbContext(opts); await db.Database.MigrateAsync(); return;
}
if (args.Contains("--bootstrap"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<Administration>().Bootstrap(
        builder.Configuration["Bootstrap:Username"] ?? throw new InvalidOperationException("Set Bootstrap__Username"),
        builder.Configuration["Bootstrap:Password"] ?? throw new InvalidOperationException("Set Bootstrap__Password"), default);
    Console.WriteLine("Platform administrator created. Remove Bootstrap__Password from your environment."); return;
}
app.UseExceptionHandler();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Correlation-ID"] = ctx.TraceIdentifier;
    ctx.RequestServices.GetRequiredService<IObservability>().Count("http.request");
    if (ctx.Request.Path.StartsWithSegments("/v1/auth")) ctx.Response.Headers.CacheControl = "no-store";
    try { await next(); }
    catch (ArgumentException ex) { await Results.Problem(ex.Message, statusCode: 400).ExecuteAsync(ctx); }
    catch (UnauthorizedAccessException) { ctx.Response.StatusCode = 403; }
    catch (RedisException) { await Results.Problem("Redis unavailable; requests fail closed.", statusCode: 503).ExecuteAsync(ctx); }
    catch (PostgresException ex) when (ex.SqlState is "23505" or "23503" or "23514") { await Results.Problem("The request conflicts with stored data or constraints.", statusCode: 409).ExecuteAsync(ctx); }
});
app.UseAuthentication();
app.Use(async (ctx, next) =>
{
    if (ctx.User.Identity?.IsAuthenticated == true)
    {
        var identities = ctx.RequestServices.GetRequiredService<IUserIdentityService>();
        if (!Guid.TryParse(ctx.User.FindFirstValue("sub"), out var id) || await identities.Find(id, ctx.RequestAborted) is not { } user)
        { ctx.Response.StatusCode = 401; return; }
        // Rehydrate roles and tenant on every request so disabling or role changes take effect immediately.
        var claims = new List<Claim> { new("sub", user.Id.ToString()), new("name", user.Name) };
        if (ctx.User.FindFirstValue("exp") is { } expiry) claims.Add(new("exp", expiry));
        if (user.TenantId is { } t) claims.Add(new("tenant_id", t.ToString()));
        claims.AddRange(user.Roles.Select(r => new Claim("role", r)));
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Local", "name", "role"));
        var quotaOptions = ctx.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitingOptions>>().Value;
        if (user.TenantId is { } tenant && quotaOptions.RequestMiddlewareEnabled)
        {
            var db = ctx.RequestServices.GetRequiredService<Database>();
            await using var c = await db.App(ctx.RequestAborted); await using var tx = await Database.Scope(c, tenant, ctx.RequestAborted);
            var rows = await Database.Query(c, "SELECT requests_per_minute FROM tenant_rate_limits", ctx.RequestAborted);
            int limit = rows.Count > 0 ? (int)rows[0]["requests_per_minute"]! : quotaOptions.RequestsPerMinute;
            var limiter = ctx.RequestServices.GetRequiredService<IRequestRateLimiter>();
            if (!await limiter.Allow(tenant, 1, limit)) { ctx.Response.Headers.RetryAfter = "60"; ctx.Response.StatusCode = 429; return; }
        }
    }
    await next();
});
app.UseAuthorization();
var openApi = app.MapOpenApi();
if (!args.Contains("--export-openapi")) openApi.RequireAuthorization("admin");
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (Database db, IConnectionMultiplexer redis, CancellationToken ct) =>
{
    try { await using var c = await db.App(ct); await Database.Execute(c, "SELECT 1", ct); await redis.GetDatabase().PingAsync(); return Results.Ok(new { status = "ready" }); }
    catch (Exception) { return Results.StatusCode(503); }
});
app.MapPulse();
app.Run();
public partial class Program;
