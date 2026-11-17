namespace Pulse.Domain;

public sealed record TelemetryItem(Guid EventId, DateTimeOffset OccurredAt, string Metric, double Value);
public sealed record ItemResult(Guid EventId, string Status, string? Reason = null);
public static class TelemetryPolicy
{
    public static string? Validate(TelemetryItem item, DateTimeOffset now) =>
        item.EventId == Guid.Empty ? "eventId is required" :
        string.IsNullOrWhiteSpace(item.Metric) || item.Metric.Length > 128 ? "metric must contain 1-128 characters" :
        !double.IsFinite(item.Value) ? "value must be finite" :
        item.OccurredAt < now.AddHours(-72) ? "event is older than 72 hours" :
        item.OccurredAt > now ? "event is in the future" : null;
}
public static class AlertState
{
    public static bool CanTransition(string from, string to) =>
        from == "OPEN" && (to == "ACKNOWLEDGED" || to == "RESOLVED") ||
        from == "ACKNOWLEDGED" && to == "RESOLVED";
}
