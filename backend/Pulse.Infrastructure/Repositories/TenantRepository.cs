using Npgsql;
using System.Text.Json;

namespace Pulse.Infrastructure.Repositories;

public sealed class TenantRepository(Database db) : ITenantRepository
{
    public async Task CreateAsync(Guid id, string name, CancellationToken ct)
    {
        await using var c = await db.Auth(ct);
        await Database.Execute(c, "INSERT INTO tenants(id,name) VALUES(@id,@name)", ct, ("id", id), ("name", name));
    }

    public async Task<List<Dictionary<string, object?>>> ListAsync(CancellationToken ct)
    {
        await using var c = await db.Auth(ct);
        return await Database.Query(c, "SELECT id,name,created_at FROM tenants ORDER BY created_at LIMIT 500", ct);
    }
}
