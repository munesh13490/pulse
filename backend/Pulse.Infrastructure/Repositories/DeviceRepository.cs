using Npgsql;
using System.Text.Json;

namespace Pulse.Infrastructure.Repositories;

public sealed class DeviceRepository(TenantDatabase database) : IDeviceRepository
{
    public Task<List<Dictionary<string, object?>>> ListAsync(Guid tenant, CancellationToken ct) => database.ExecuteAsync(tenant, (c, token) => Database.Query(c, "SELECT device_id AS id,name,status='ACTIVE' AS enabled,created_at FROM devices ORDER BY created_at DESC LIMIT 500", token), ct);
    public Task<Dictionary<string, object?>?> GetAsync(Guid tenant, Guid id, CancellationToken ct) => database.ExecuteAsync<Dictionary<string, object?>?>(tenant, async (c, token) =>
    {
        var rows = await Database.Query(c, "SELECT device_id AS id,name,status='ACTIVE' AS enabled,created_at FROM devices WHERE device_id=@id", token, ("id", id));
        return rows.FirstOrDefault();
    }, ct);
    public Task CreateAsync(Guid tenant, Guid id, string name, CancellationToken ct) => database.ExecuteAsync(tenant, (c, token) => Database.Execute(c, "INSERT INTO devices(device_id,tenant_id,name,external_id) VALUES(@id,@t,@name,CAST(CAST(@id AS uuid) AS text))", token, ("id", id), ("t", tenant), ("name", name)), ct);
    public Task<bool> UpdateAsync(Guid tenant, Guid id, string name, bool enabled, CancellationToken ct) => database.ExecuteAsync(tenant, async (c, token) => await Database.Execute(c, "UPDATE devices SET name=@name,status=CASE WHEN @enabled THEN 'ACTIVE' ELSE 'DISABLED' END WHERE device_id=@id", token, ("name", name), ("enabled", enabled), ("id", id)) > 0, ct);
}
