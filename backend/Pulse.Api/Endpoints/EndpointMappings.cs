namespace Pulse.Api.Endpoints;

public static class EndpointMappings
{
    public static void MapPulseEndpoints(this WebApplication app)
    {
        app.MapAuthEndpoints();
        app.MapTenantEndpoints();
        app.MapUserEndpoints();
        app.MapDeviceEndpoints();
        app.MapDeviceKeyEndpoints();
        app.MapTenantRateLimitEndpoints();
        app.MapTelemetryEndpoints();
        app.MapRuleEndpoints();
        app.MapAlertEndpoints();
        app.MapHealthEndpoints();
    }
}
