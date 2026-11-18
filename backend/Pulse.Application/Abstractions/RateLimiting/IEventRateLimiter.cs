using Pulse.Domain;

namespace Pulse.Application.Abstractions.RateLimiting;

public interface IEventRateLimiter
{
    Task<bool> Allow(Guid tenant, int count, int limit);
}
