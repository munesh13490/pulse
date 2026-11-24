namespace Pulse.Api.Endpoints;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this WebApplication app)
    {
        app.MapPost(
            "/v1/admin/tenants",
            async (NameRequest req, ITenantService service, CancellationToken ct) =>
            {
                var id = await service.CreateAsync(req.Name, ct);
                return Results.Created($"/v1/admin/tenants/{id}", new { id, name = req.Name });
            })
            .RequireAuthorization("platform");

        app.MapGet(
            "/v1/admin/tenants",
            async (ITenantService service, CancellationToken ct) =>
                Results.Ok(await service.ListAsync(ct)))
            .RequireAuthorization("platform");
    }
}
