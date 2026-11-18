namespace Pulse.Application.Models.Telemetry;

public enum IngestionStatus
{
    Accepted,
    InvalidBatch,
    Unauthorized,
    RateLimited
}
