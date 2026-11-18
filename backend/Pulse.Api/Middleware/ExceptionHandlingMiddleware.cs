using Npgsql;
using StackExchange.Redis;

namespace Pulse.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (ArgumentException ex)
        {
            await Results.Problem(ex.Message, statusCode: 400).ExecuteAsync(ctx);
        }
        catch (UnauthorizedAccessException)
        {
            ctx.Response.StatusCode = 403;
        }
        catch (RedisException)
        {
            await Results.Problem("Redis unavailable; requests fail closed.", statusCode: 503).ExecuteAsync(ctx);
        }
        catch (PostgresException ex) when (ex.SqlState is "23505" or "23503" or "23514")
        {
            await Results.Problem("The request conflicts with stored data or constraints.", statusCode: 409).ExecuteAsync(ctx);
        }
    }
}
