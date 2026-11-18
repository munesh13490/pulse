using Pulse.Domain;

namespace Pulse.Application.Abstractions.Repositories;

public interface ITelemetryRepository
{
    Task<IReadOnlyList<ItemResult>> Ingest(DeviceIdentity device, IReadOnlyList<TelemetryItem> items, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ReadAsync(Guid tenant, Guid device, CancellationToken ct);
}
