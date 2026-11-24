namespace Pulse.Api.Endpoints;

public static class TelemetryEndpoints
{
    public static void MapTelemetryEndpoints(this WebApplication app)
    {
        app.MapPost(
            "/v1/telemetry",
            async (
                TelemetryRequest req,
                HttpContext ctx,
                ITelemetryIngestionService service,
                CancellationToken ct) =>
            {
                var result = await service.IngestAsync(
                    ctx.Request.Headers["X-Device-Key"].ToString(),
                    req.Items,
                    ct);

                if (result.Status == IngestionStatus.RateLimited)
                {
                    ctx.Response.Headers.RetryAfter = "60";
                }

                return result.Status switch
                {
                    IngestionStatus.Accepted => Results.Ok(new { items = result.Items }),
                    IngestionStatus.InvalidBatch =>
                        Results.BadRequest(new { error = "items must contain 1-500 objects" }),
                    IngestionStatus.Unauthorized => Results.Unauthorized(),
                    _ => Results.StatusCode(429)
                };
            });

        app.MapGet(
            "/v1/telemetry",
            async (
                Guid deviceId,
                HttpContext ctx,
                ITelemetryRepository repository,
                CancellationToken ct) =>
                Results.Ok(await repository.ReadAsync(RequestIdentity.Tenant(ctx), deviceId, ct)))
            .RequireAuthorization("tenant");
    }
}
