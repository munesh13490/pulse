namespace Pulse.Application.Abstractions.Repositories;

public interface IDeviceKeyRepository
{
    Task<bool> RotateAsync(Guid tenant, Guid device, DeviceKeyDigest key, DateTimeOffset expires, CancellationToken ct);
    Task<List<Dictionary<string, object?>>> ListAsync(Guid tenant, Guid device, CancellationToken ct);
    Task<bool> RevokeAsync(Guid tenant, Guid device, Guid key, CancellationToken ct);
}
