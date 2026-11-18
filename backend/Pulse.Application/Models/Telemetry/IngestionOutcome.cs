namespace Pulse.Application.Models.Telemetry;

public sealed record IngestionOutcome(IngestionStatus Status, IReadOnlyList<ItemResult>? Items = null);
