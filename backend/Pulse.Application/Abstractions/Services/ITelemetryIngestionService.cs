namespace Pulse.Application.Abstractions.Services;

public interface ITelemetryIngestionService
{
    Task<IngestionOutcome> IngestAsync(string apiKey, IReadOnlyList<TelemetryItem>? items, CancellationToken ct);
}
