namespace Pulse.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
        app.MapGet("/health/ready", async (IHealthProbe probe, CancellationToken ct) => await probe.IsReadyAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
    }
}
