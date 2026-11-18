using Pulse.Domain;

namespace Pulse.Application.Abstractions.RateLimiting;

public interface IRequestRateLimiter
{
    Task<bool> Allow(Guid tenant, int count, int limit);
}
