namespace Pulse.Api.Configuration;

public static class MiddlewareRegistration
{
    public static void UsePulseMiddleware(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<RequestContextMiddleware>();
        app.UseAuthentication();
        app.UseMiddleware<LocalIdentityMiddleware>();
        app.UseAuthorization();
    }
}
