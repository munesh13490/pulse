using System.Diagnostics.Metrics;
using Pulse.Application;
using StackExchange.Redis;

namespace Pulse.Infrastructure.RateLimiting;

public abstract class RedisLimiter(IConnectionMultiplexer redis, string prefix)
{
    private const string Script = "local v=redis.call('INCRBY',KEYS[1],ARGV[1]); if v==tonumber(ARGV[1]) then redis.call('EXPIRE',KEYS[1],60) end; if v>tonumber(ARGV[2]) then return 0 end; return 1";
    public async Task<bool> Allow(Guid tenant, int count, int limit) => (long)await redis.GetDatabase().ScriptEvaluateAsync(Script, [(RedisKey)$"pulse:{prefix}:{tenant}"], [(RedisValue)count, (RedisValue)limit]) == 1;
}
