using System.ComponentModel.DataAnnotations;

namespace Pulse.Infrastructure.Configuration;

public sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";
    public bool RequestMiddlewareEnabled { get; set; } = true;

    [Range(1, 100000)]
    public int RequestsPerMinute { get; set; } = 100;

    [Range(1, 1000000)]
    public int EventsPerMinute { get; set; } = 10000;
}
