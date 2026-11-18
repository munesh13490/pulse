namespace Pulse.Api.Endpoints;

public static class DeviceKeyEndpoints
{
    public static void MapDeviceKeyEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/devices/{id:guid}/keys", async (Guid id, KeyRequest req, HttpContext ctx, IDeviceKeyService service, CancellationToken ct) =>
        {
            var key = await service.IssueAsync(RequestIdentity.Tenant(ctx), id, req.ExpiresAt, ct);
            ctx.Response.Headers.CacheControl = "no-store";
            return key is null ? Results.NotFound() : Results.Ok(key);
        }).RequireAuthorization("operate");
        app.MapGet("/v1/devices/{id:guid}/keys", async (Guid id, HttpContext ctx, IDeviceKeyService service, CancellationToken ct) => Results.Ok(await service.ListAsync(RequestIdentity.Tenant(ctx), id, ct))).RequireAuthorization("operate");
        app.MapPost("/v1/devices/{id:guid}/keys/{keyId:guid}/revoke", async (Guid id, Guid keyId, HttpContext ctx, IDeviceKeyService service, CancellationToken ct) => await service.RevokeAsync(RequestIdentity.Tenant(ctx), id, keyId, ct) ? Results.NoContent() : Results.NotFound()).RequireAuthorization("operate");
    }
}
