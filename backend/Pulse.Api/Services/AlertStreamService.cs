using System.Text.Json;
using StackExchange.Redis;

namespace Pulse.Api.Services;

public sealed class AlertStreamService(IAlertService alerts, IConnectionMultiplexer redis, IUserIdentityService identities)
{
    public async Task StreamAsync(HttpContext ctx, CancellationToken ct)
    {
        var tenant = RequestIdentity.Tenant(ctx);
        var expires = DateTimeOffset.FromUnixTimeSeconds(long.Parse(ctx.User.FindFirst("exp")!.Value));
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";
        // Redis is a wakeup hint. Periodic durable snapshots recover missed publications and reconnects.
        using var signal = new SemaphoreSlim(0, 1);
        var channel = RedisChannel.Literal($"pulse:alerts:{tenant}");
        var subscriber = redis.GetSubscriber();
        Action<RedisChannel, RedisValue> handler = (_, _) =>
        {
            try
            {
                signal.Release();
            }
            catch (SemaphoreFullException)
            {
            }
        };
        await subscriber.SubscribeAsync(channel, handler);
        try
        {
            while (!ct.IsCancellationRequested && DateTimeOffset.UtcNow < expires)
            {
                var identity = await identities.Find(RequestIdentity.UserId(ctx), ct);
                if (identity is null || identity.TenantId != tenant || !identity.Roles.Any(r => r is "tenant-admin" or "operator" or "viewer"))
                    break;
                var rows = await alerts.SnapshotAsync(tenant, ct);
                await ctx.Response.WriteAsync($"event: snapshot\ndata: {JsonSerializer.Serialize(rows)}\n\n", ct);
                await ctx.Response.Body.FlushAsync(ct);
                await signal.WaitAsync(TimeSpan.FromSeconds(15), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            await subscriber.UnsubscribeAsync(channel, handler);
        }
    }
}
