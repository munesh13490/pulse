using System.Diagnostics.Metrics;
using Pulse.Application;
using StackExchange.Redis;
namespace Pulse.Infrastructure;

public abstract class RedisLimiter(IConnectionMultiplexer redis, string prefix)
{
    private const string Script = "local v=redis.call('INCRBY',KEYS[1],ARGV[1]); if v==tonumber(ARGV[1]) then redis.call('EXPIRE',KEYS[1],60) end; if v>tonumber(ARGV[2]) then return 0 end; return 1";
    public async Task<bool> Allow(Guid tenant, int count, int limit) =>
        (long)await redis.GetDatabase().ScriptEvaluateAsync(Script, [(RedisKey)$"pulse:{prefix}:{tenant}"], [(RedisValue)count, (RedisValue)limit]) == 1;
}
public sealed class RedisRequestRateLimiter(IConnectionMultiplexer redis) : RedisLimiter(redis, "requests"), IRequestRateLimiter;
public sealed class RedisEventRateLimiter(IConnectionMultiplexer redis) : RedisLimiter(redis, "events"), IEventRateLimiter;
public sealed class RedisNotificationBus(IConnectionMultiplexer redis) : INotificationBus
{
    public async Task Publish(Guid tenant, string message) => await redis.GetSubscriber().PublishAsync(RedisChannel.Literal($"pulse:alerts:{tenant}"), message);
}
public sealed class LocalObservability : IObservability
{
    private static readonly Meter Meter = new("Pulse");
    private static readonly Counter<long> Operations = Meter.CreateCounter<long>("pulse.operations");
    public void Count(string operation) => Operations.Add(1, new KeyValuePair<string, object?>("operation", operation));
}
