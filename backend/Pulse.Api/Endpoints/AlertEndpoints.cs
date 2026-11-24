namespace Pulse.Api.Endpoints;

public static class AlertEndpoints
{
    public static void MapAlertEndpoints(this WebApplication app)
    {
        app.MapGet(
            "/v1/alerts",
            async (
                string? state,
                DateTimeOffset? before,
                Guid? beforeId,
                HttpContext ctx,
                IAlertService service,
                CancellationToken ct) =>
                Results.Ok(await service.ListAsync(
                    RequestIdentity.Tenant(ctx),
                    state,
                    before,
                    beforeId,
                    ct)))
            .RequireAuthorization("tenant");

        app.MapGet(
            "/v1/alerts/{id:guid}/transitions",
            async (Guid id, HttpContext ctx, IAlertService service, CancellationToken ct) =>
                Results.Ok(await service.TransitionsAsync(RequestIdentity.Tenant(ctx), id, ct)))
            .RequireAuthorization("tenant");

        app.MapPost(
            "/v1/alerts/{id:guid}/transitions",
            async (
                Guid id,
                TransitionRequest req,
                HttpContext ctx,
                IAlertService service,
                CancellationToken ct) =>
            {
                var outcome = await service.TransitionAsync(
                    RequestIdentity.Tenant(ctx),
                    RequestIdentity.UserId(ctx),
                    id,
                    req.State,
                    ct);

                return outcome switch
                {
                    TransitionOutcome.Updated => Results.NoContent(),
                    TransitionOutcome.NotFound => Results.NotFound(),
                    _ => Results.Conflict(new { error = "Invalid state transition" })
                };
            })
            .RequireAuthorization("operate");

        app.MapGet(
            "/v1/alerts/stream",
            (HttpContext ctx, AlertStreamService stream, CancellationToken ct) =>
                stream.StreamAsync(ctx, ct))
            .RequireAuthorization("tenant");
    }
}
