using Npgsql;

namespace Pulse.Infrastructure.Persistence;
/// <summary>Owns the transaction-local RLS context for one repository operation.</summary>
public sealed class TenantDatabase(Database database)
{
    public async Task<T> ExecuteAsync<T>(Guid tenant, Func<NpgsqlConnection, CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        await using var connection = await database.App(ct);
        await using var transaction = await Database.Scope(connection, tenant, ct);
        var result = await operation(connection, ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
