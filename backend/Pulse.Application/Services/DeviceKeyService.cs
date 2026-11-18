namespace Pulse.Application.Services;

public sealed class DeviceKeyService(IDeviceKeyRepository keys, IDeviceKeyGenerator generator) : IDeviceKeyService
{
    public async Task<IssuedDeviceKey?> IssueAsync(Guid tenant, Guid device, DateTimeOffset expires, CancellationToken ct)
    {
        if (expires <= DateTimeOffset.UtcNow || expires > DateTimeOffset.UtcNow.AddYears(1))
            throw new ArgumentException("Expiry must be within one year.");
        var key = generator.Generate();
        return await keys.RotateAsync(tenant, device, new(key.Id, key.Digest), expires, ct) ? new(key.Id, $"{key.Id}.{key.Secret}", expires) : null;
    }

    public Task<List<Dictionary<string, object?>>> ListAsync(Guid tenant, Guid device, CancellationToken ct) => keys.ListAsync(tenant, device, ct);
    public Task<bool> RevokeAsync(Guid tenant, Guid device, Guid key, CancellationToken ct) => keys.RevokeAsync(tenant, device, key, ct);
}
