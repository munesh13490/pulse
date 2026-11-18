namespace Pulse.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/admin/users", async (UserRequest req, HttpContext ctx, IUserService service, IUserIdentityService identities, CancellationToken ct) =>
        {
            var id = await service.CreateAsync(await RequestIdentity.Actor(ctx, identities, ct), req.TenantId, req.Username, req.Password, req.Role, ct);
            return Results.Created($"/v1/admin/users/{id}", new
            {
                id
            });
        }).RequireAuthorization("admin");
        app.MapGet("/v1/admin/users", async (HttpContext ctx, IUserService service, IUserIdentityService identities, CancellationToken ct) => Results.Ok(await service.ListAsync(await RequestIdentity.Actor(ctx, identities, ct), ct))).RequireAuthorization("admin");
        app.MapPatch("/v1/admin/users/{id:guid}", async (Guid id, UserPatch req, HttpContext ctx, IUserService service, IUserIdentityService identities, CancellationToken ct) => await service.UpdateAsync(await RequestIdentity.Actor(ctx, identities, ct), id, req.Username, false, ct) ? Results.NoContent() : Results.NotFound()).RequireAuthorization("admin");
        app.MapPost("/v1/admin/users/{id:guid}/disable", async (Guid id, HttpContext ctx, IUserService service, IUserIdentityService identities, CancellationToken ct) => await service.UpdateAsync(await RequestIdentity.Actor(ctx, identities, ct), id, null, true, ct) ? Results.NoContent() : Results.NotFound()).RequireAuthorization("admin");
    }
}
