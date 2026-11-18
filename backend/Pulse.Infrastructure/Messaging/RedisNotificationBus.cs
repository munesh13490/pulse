using System.Diagnostics.Metrics;
using Pulse.Application;
using StackExchange.Redis;

namespace Pulse.Infrastructure.Messaging;

public sealed class RedisNotificationBus(IConnectionMultiplexer redis) : INotificationBus
{
    public async Task Publish(Guid tenant, string message) => await redis.GetSubscriber().PublishAsync(RedisChannel.Literal($"pulse:alerts:{tenant}"), message);
}
