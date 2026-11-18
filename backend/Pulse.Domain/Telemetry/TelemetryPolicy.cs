namespace Pulse.Domain.Telemetry;

public static class TelemetryPolicy
{
    public static string? Validate(TelemetryItem item, DateTimeOffset now) => item.EventId == Guid.Empty ? "eventId is required" : string.IsNullOrWhiteSpace(item.Metric) || item.Metric.Length > 128 ? "metric must contain 1-128 characters" : !double.IsFinite(item.Value) ? "value must be finite" : item.OccurredAt < now.AddHours(-72) ? "event is older than 72 hours" : item.OccurredAt > now ? "event is in the future" : null;
}
