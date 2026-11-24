using Pulse.Infrastructure.RateLimiting;
using Xunit;

namespace Pulse.Tests.Unit.Services;

public sealed class InMemoryRateLimiterTests
{
    [Fact]
    public async Task RequestAndEventQuotasHaveIndependentTenantCounters()
    {
        var limiter = new InMemoryRateLimiter();
        var requests = (IRequestRateLimiter)limiter;
        var events = (IEventRateLimiter)limiter;
        var tenant = Guid.NewGuid();

        Assert.True(await requests.Allow(tenant, 1, 1));
        Assert.False(await requests.Allow(tenant, 1, 1));
        Assert.True(await events.Allow(tenant, 1, 1));
        Assert.True(await requests.Allow(Guid.NewGuid(), 1, 1));
    }

    [Fact]
    public async Task ConcurrentRequestsCannotExceedTenantLimit()
    {
        var limiter = new InMemoryRateLimiter();
        var tenant = Guid.NewGuid();
        var attempts = Enumerable.Range(0, 100)
            .Select(_ => limiter.AllowRequest(tenant));

        var results = await Task.WhenAll(attempts);

        Assert.Equal(25, results.Count(allowed => allowed));
    }

    private static Task<bool> AllowRequest(this InMemoryRateLimiter limiter, Guid tenant) =>
        ((IRequestRateLimiter)limiter).Allow(tenant, 1, 25);
}
