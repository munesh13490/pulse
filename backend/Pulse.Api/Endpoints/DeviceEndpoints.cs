namespace Pulse.Api.Endpoints;

public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this WebApplication app)
    {
        app.MapGet(
            "/v1/devices",
            async (HttpContext ctx, IDeviceService service, CancellationToken ct) =>
                Results.Ok(await service.ListAsync(RequestIdentity.Tenant(ctx), ct)))
            .RequireAuthorization("tenant");

        app.MapGet(
            "/v1/devices/{id:guid}",
            async (Guid id, HttpContext ctx, IDeviceService service, CancellationToken ct) =>
            {
                var device = await service.GetAsync(RequestIdentity.Tenant(ctx), id, ct);
                return device is null ? Results.NotFound() : Results.Ok(device);
            })
            .RequireAuthorization("tenant");

        app.MapPost(
            "/v1/devices",
            async (NameRequest req, HttpContext ctx, IDeviceService service, CancellationToken ct) =>
            {
                var id = await service.CreateAsync(RequestIdentity.Tenant(ctx), req.Name, ct);
                return Results.Created($"/v1/devices/{id}", new { id, name = req.Name });
            })
            .RequireAuthorization("operate");

        app.MapPatch(
            "/v1/devices/{id:guid}",
            async (
                Guid id,
                DevicePatch req,
                HttpContext ctx,
                IDeviceService service,
                CancellationToken ct) =>
                await service.UpdateAsync(
                    RequestIdentity.Tenant(ctx),
                    id,
                    req.Name,
                    req.Enabled,
                    ct)
                    ? Results.NoContent()
                    : Results.NotFound())
            .RequireAuthorization("operate");
    }
}
