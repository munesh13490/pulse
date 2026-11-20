using Npgsql;
using System.Text.Json;

namespace Pulse.Infrastructure.Repositories;

public sealed class DeviceKeyRepository(TenantDatabase database) : IDeviceKeyRepository
{
    public Task<bool> RotateAsync(Guid tenant, Guid device, DeviceKeyDigest key, DateTimeOffset expires, CancellationToken ct) => database.ExecuteAsync(tenant, async (c, token) =>
    {
        var rows = await Database.Query(c, "SELECT device_id FROM devices WHERE device_id=@id AND status='ACTIVE' FOR UPDATE", token, ("id", device));
        if (rows.Count == 0)
            return false;
        await Database.Execute(c, "UPDATE device_api_keys SET revoked_at=now() WHERE device_id=@d AND revoked_at IS NULL", token, ("d", device));
        await Database.Execute(c, "INSERT INTO device_api_keys(key_id,tenant_id,device_id,key_digest,expires_at) VALUES(@id,@t,@d,@hash,@expires)", token, ("id", key.Id), ("t", tenant), ("d", device), ("hash", key.Digest), ("expires", expires));
        return true;
    }, ct);
    public Task<List<Dictionary<string, object?>>> ListAsync(Guid tenant, Guid device, CancellationToken ct) => database.ExecuteAsync(tenant, (c, token) => Database.Query(c, "SELECT key_id AS id,expires_at,revoked_at,created_at FROM device_api_keys WHERE device_id=@id ORDER BY created_at DESC", token, ("id", device)), ct);
    public Task<bool> RevokeAsync(Guid tenant, Guid device, Guid key, CancellationToken ct) => database.ExecuteAsync(tenant, async (c, token) => await Database.Execute(c, "UPDATE device_api_keys SET revoked_at=coalesce(revoked_at,now()) WHERE key_id=@key AND device_id=@id", token, ("key", key), ("id", device)) > 0, ct);
}
