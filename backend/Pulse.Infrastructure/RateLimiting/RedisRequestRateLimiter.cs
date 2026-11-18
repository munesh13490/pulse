using System.Diagnostics.Metrics;
using Pulse.Application;
using StackExchange.Redis;

namespace Pulse.Infrastructure.RateLimiting;

public sealed class RedisRequestRateLimiter(IConnectionMultiplexer redis) : RedisLimiter(redis, "requests"), IRequestRateLimiter;
