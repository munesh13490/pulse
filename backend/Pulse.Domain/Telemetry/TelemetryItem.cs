namespace Pulse.Domain.Telemetry;

public sealed record TelemetryItem(Guid EventId, DateTimeOffset OccurredAt, string Metric, double Value);
