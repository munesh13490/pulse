namespace Pulse.Api.Middleware;

public sealed class RequestContextMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext ctx, IObservability observability)
    {
        ctx.Response.Headers["X-Correlation-ID"] = ctx.TraceIdentifier;
        observability.Count("http.request");
        if (ctx.Request.Path.StartsWithSegments("/v1/auth"))
            ctx.Response.Headers.CacheControl = "no-store";
        return next(ctx);
    }
}
