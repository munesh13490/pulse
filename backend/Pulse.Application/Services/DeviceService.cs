namespace Pulse.Application.Services;

public sealed class DeviceService(IDeviceRepository devices) : IDeviceService
{
    public Task<List<Dictionary<string, object?>>> ListAsync(Guid tenant, CancellationToken ct) => devices.ListAsync(tenant, ct);
    public Task<Dictionary<string, object?>?> GetAsync(Guid tenant, Guid id, CancellationToken ct) => devices.GetAsync(tenant, id, ct);
    public async Task<Guid> CreateAsync(Guid tenant, string name, CancellationToken ct)
    {
        NameValidator.Validate(name);
        var id = Guid.NewGuid();
        await devices.CreateAsync(tenant, id, name, ct);
        return id;
    }

    public Task<bool> UpdateAsync(Guid tenant, Guid id, string name, bool enabled, CancellationToken ct)
    {
        NameValidator.Validate(name);
        return devices.UpdateAsync(tenant, id, name, enabled, ct);
    }
}
