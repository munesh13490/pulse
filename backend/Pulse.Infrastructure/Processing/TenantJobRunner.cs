using Microsoft.Extensions.Logging;
using Npgsql;

namespace Pulse.Infrastructure.Processing;

public sealed class TenantJobRunner(Database db, ILogger<TenantJobRunner> logger)
{
    public async Task RunAsync(string worker, Func<NpgsqlConnection, Guid, CancellationToken, Task> process, CancellationToken ct)
    {
        try
        {
            await using var auth = await db.Auth(ct);
            var tenants = await Database.Query(auth, "SELECT id FROM tenants", ct);
            foreach (var t in tenants)
            {
                var tenant = (Guid)t["id"]!;
                try
                {
                    await using var c = await db.App(ct);
                    await using var tx = await Database.Scope(c, tenant, ct);
                    // One replica per worker type and tenant; transaction locks release on crash.
                    var locks = await Database.Query(c, "SELECT pg_try_advisory_xact_lock(hashtextextended(@key,0)) AS acquired", ct, ("key", $"{worker}:{tenant}"));
                    if (!(bool)locks[0]["acquired"]!)
                        continue;
                    await process(c, tenant, ct);
                    await tx.CommitAsync(ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    logger.LogError(ex, "Worker {Worker} failed for tenant {Tenant}", worker, tenant);
                }
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Worker cycle failed");
        }
    }
}
