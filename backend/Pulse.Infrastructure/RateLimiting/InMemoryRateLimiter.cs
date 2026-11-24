using System.Collections.Concurrent;
using Pulse.Application;

namespace Pulse.Infrastructure.RateLimiting;

public sealed class InMemoryRateLimiter : IRequestRateLimiter, IEventRateLimiter
{
    private static readonly TimeSpan WindowLength = TimeSpan.FromSeconds(60);
    private readonly ConcurrentDictionary<(Guid Tenant, string Kind), Counter> _counters = new();
    private int _callsSinceCleanup;

    Task<bool> IRequestRateLimiter.Allow(Guid tenant, int count, int limit) =>
        Allow(tenant, count, limit, "requests");

    Task<bool> IEventRateLimiter.Allow(Guid tenant, int count, int limit) =>
        Allow(tenant, count, limit, "events");

    private Task<bool> Allow(Guid tenant, int count, int limit, string kind)
    {
        if (count < 1 || limit < 1)
        {
            return Task.FromResult(false);
        }

        var now = DateTimeOffset.UtcNow;
        bool allowed;
        while (true)
        {
            var counter = _counters.GetOrAdd((tenant, kind), _ => new Counter(now));
            var result = counter.TryConsume(count, limit, now);
            if (result is { } value)
            {
                allowed = value;
                break;
            }
        }

        if (Interlocked.Increment(ref _callsSinceCleanup) >= 1024)
        {
            Interlocked.Exchange(ref _callsSinceCleanup, 0);
            RemoveExpiredCounters(now);
        }

        return Task.FromResult(allowed);
    }

    private void RemoveExpiredCounters(DateTimeOffset now)
    {
        foreach (var (key, counter) in _counters)
        {
            if (counter.TryRetire(now, WindowLength))
            {
                _counters.TryRemove(new KeyValuePair<(Guid Tenant, string Kind), Counter>(key, counter));
            }
        }
    }

    private sealed class Counter(DateTimeOffset startedAt)
    {
        private readonly object _gate = new();
        private DateTimeOffset _startedAt = startedAt;
        private long _used;

        public bool? TryConsume(int count, int limit, DateTimeOffset now)
        {
            lock (_gate)
            {
                if (_retired)
                {
                    return null;
                }

                if (now - _startedAt >= WindowLength)
                {
                    _startedAt = now;
                    _used = 0;
                }

                _used += count;
                return _used <= limit;
            }
        }

        public bool TryRetire(DateTimeOffset now, TimeSpan windowLength)
        {
            lock (_gate)
            {
                if (now - _startedAt < windowLength)
                {
                    return false;
                }

                _retired = true;
                return true;
            }
        }

        private bool _retired;
    }
}
