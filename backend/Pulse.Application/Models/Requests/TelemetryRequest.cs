namespace Pulse.Application.Models.Requests;

public sealed record TelemetryRequest(List<TelemetryItem>? Items);
