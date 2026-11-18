namespace Pulse.Application.Abstractions.Services;

public interface IDeviceKeyService
{
    Task<IssuedDeviceKey?> IssueAsync(Guid tenant, Guid device, DateTimeOffset expires, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ListAsync(Guid tenant, Guid device, CancellationToken ct);
    Task<bool> RevokeAsync(Guid tenant, Guid device, Guid key, CancellationToken ct);
}
