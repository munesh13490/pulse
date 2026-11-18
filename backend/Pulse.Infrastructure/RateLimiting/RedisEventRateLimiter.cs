using System.Diagnostics.Metrics;
using Pulse.Application;
using StackExchange.Redis;

namespace Pulse.Infrastructure.RateLimiting;

public sealed class RedisEventRateLimiter(IConnectionMultiplexer redis) : RedisLimiter(redis, "events"), IEventRateLimiter;
