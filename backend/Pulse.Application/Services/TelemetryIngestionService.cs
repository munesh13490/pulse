namespace Pulse.Application.Services;

public sealed class TelemetryIngestionService(IDeviceCredentialValidator credentials, IRequestRateLimiter requests, IEventRateLimiter events, ITenantRateLimitService limits, ITelemetryRepository repository) : ITelemetryIngestionService
{
    public async Task<IngestionOutcome> IngestAsync(string apiKey, IReadOnlyList<TelemetryItem>? items, CancellationToken ct)
    {
        if (items is null || items.Count is < 1 or > 500 || items.Any(i => i is null))
            return new(IngestionStatus.InvalidBatch);
        var device = await credentials.Validate(apiKey, ct);
        if (device is null)
            return new(IngestionStatus.Unauthorized);
        var quota = await limits.GetAsync(device.TenantId, ct);
        if (!await requests.Allow(device.TenantId, 1, quota.RequestsPerMinute) || !await events.Allow(device.TenantId, items.Count, quota.EventsPerMinute))
            return new(IngestionStatus.RateLimited);
        return new(IngestionStatus.Accepted, await repository.Ingest(device, items, ct));
    }
}
