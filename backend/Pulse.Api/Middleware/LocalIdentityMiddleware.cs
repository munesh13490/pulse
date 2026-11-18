using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace Pulse.Api.Middleware;

public sealed class LocalIdentityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, IUserIdentityService identities, ITenantRateLimitService quotas, IOptions<RateLimitingOptions> options)
    {
        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            if (!Guid.TryParse(ctx.User.FindFirstValue("sub"), out var id) || await identities.Find(id, ctx.RequestAborted) is not { } user)
            {
                ctx.Response.StatusCode = 401;
                return;
            }

            // Rehydrate roles and tenant on every request so disabling or role changes take effect immediately.
            var claims = new List<Claim>
            {
                new("sub", user.Id.ToString()),
                new("name", user.Name)
            };
            if (ctx.User.FindFirstValue("exp") is { } expiry)
                claims.Add(new("exp", expiry));
            if (user.TenantId is { } t)
                claims.Add(new("tenant_id", t.ToString()));
            claims.AddRange(user.Roles.Select(r => new Claim("role", r)));
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Local", "name", "role"));
            var quotaOptions = options.Value;
            if (user.TenantId is { } tenant && quotaOptions.RequestMiddlewareEnabled)
            {
                int limit = (await quotas.GetAsync(tenant, ctx.RequestAborted)).RequestsPerMinute;
                // Defer Redis resolution so liveness and anonymous challenges do not depend on Redis.
                var limiter = ctx.RequestServices.GetRequiredService<IRequestRateLimiter>();
                if (!await limiter.Allow(tenant, 1, limit))
                {
                    ctx.Response.Headers.RetryAfter = "60";
                    ctx.Response.StatusCode = 429;
                    return;
                }
            }
        }

        await next(ctx);
    }
}
