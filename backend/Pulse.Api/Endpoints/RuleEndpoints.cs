namespace Pulse.Api.Endpoints;

public static class RuleEndpoints
{
    public static void MapRuleEndpoints(this WebApplication app)
    {
        app.MapGet(
            "/v1/rules",
            async (HttpContext ctx, IRuleService service, CancellationToken ct) =>
                Results.Ok(await service.ListAsync(RequestIdentity.Tenant(ctx), ct)))
            .RequireAuthorization("tenant");

        app.MapGet(
            "/v1/rules/{id:guid}/versions",
            async (Guid id, HttpContext ctx, IRuleService service, CancellationToken ct) =>
                Results.Ok(await service.VersionsAsync(RequestIdentity.Tenant(ctx), id, ct)))
            .RequireAuthorization("tenant");

        app.MapPost("/v1/rules", async (RuleRequest req, HttpContext ctx, IRuleService service, CancellationToken ct) =>
        {
            var id = await service.CreateAsync(RequestIdentity.Tenant(ctx), req, ct);
            return Results.Created($"/v1/rules/{id}", new { id });
        }).RequireAuthorization("operate");

        app.MapPost(
            "/v1/rules/{id:guid}/versions",
            async (Guid id, RuleRequest req, HttpContext ctx, IRuleService service, CancellationToken ct) =>
            {
                var appended = await service.AppendVersionAsync(
                    RequestIdentity.Tenant(ctx),
                    id,
                    req,
                    ct);

                return appended ? Results.NoContent() : Results.NotFound();
            })
            .RequireAuthorization("operate");
    }
}
