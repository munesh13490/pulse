using Npgsql;
using System.Text.Json;

namespace Pulse.Infrastructure.Repositories;

public sealed class DeviceRepository(TenantDatabase database) : IDeviceRepository
{
    public Task<List<Dictionary<string, object?>>> ListAsync(Guid tenant, CancellationToken ct) => database.ExecuteAsync(tenant, (c, token) => Database.Query(c, "SELECT id,name,enabled,created_at FROM devices ORDER BY created_at DESC LIMIT 500", token), ct);
    public Task<Dictionary<string, object?>?> GetAsync(Guid tenant, Guid id, CancellationToken ct) => database.ExecuteAsync<Dictionary<string, object?>?>(tenant, async (c, token) =>
    {
        var rows = await Database.Query(c, "SELECT id,name,enabled,created_at FROM devices WHERE id=@id", token, ("id", id));
        return rows.FirstOrDefault();
    }, ct);
    public Task CreateAsync(Guid tenant, Guid id, string name, CancellationToken ct) => database.ExecuteAsync(tenant, (c, token) => Database.Execute(c, "INSERT INTO devices(id,tenant_id,name) VALUES(@id,@t,@name)", token, ("id", id), ("t", tenant), ("name", name)), ct);
    public Task<bool> UpdateAsync(Guid tenant, Guid id, string name, bool enabled, CancellationToken ct) => database.ExecuteAsync(tenant, async (c, token) => await Database.Execute(c, "UPDATE devices SET name=@name,enabled=@enabled WHERE id=@id", token, ("name", name), ("enabled", enabled), ("id", id)) > 0, ct);
}
