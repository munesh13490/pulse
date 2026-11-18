namespace Pulse.Domain.Telemetry;

public sealed record ItemResult(Guid EventId, string Status, string? Reason = null);
