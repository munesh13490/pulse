namespace Pulse.Api.Endpoints;

public static class TenantRateLimitEndpoints
{
    public static void MapTenantRateLimitEndpoints(this WebApplication app)
    {
        app.MapGet(
            "/v1/admin/tenant-rate-limits",
            async (HttpContext ctx, ITenantRateLimitService service, CancellationToken ct) =>
            {
                var limits = await service.GetAsync(RequestIdentity.Tenant(ctx), ct);
                return Results.Ok(
                    new[]
                    {
                        new
                        {
                            requests_per_minute = limits.RequestsPerMinute,
                            events_per_minute = limits.EventsPerMinute
                        }
                    });
            })
            .RequireAuthorization("tenant-admin");

        app.MapPatch(
            "/v1/admin/tenant-rate-limits",
            async (LimitsRequest req, HttpContext ctx, ITenantRateLimitService service, CancellationToken ct) =>
            {
                await service.UpdateAsync(
                    RequestIdentity.Tenant(ctx),
                    new(req.RequestsPerMinute, req.EventsPerMinute),
                    ct);
                return Results.NoContent();
            })
            .RequireAuthorization("tenant-admin");
    }
}
