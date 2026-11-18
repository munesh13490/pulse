using StackExchange.Redis;

namespace Pulse.Infrastructure.Observability;

public sealed class InfrastructureHealthProbe(Database db, IConnectionMultiplexer redis) : IHealthProbe
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        try
        {
            await using var c = await db.App(ct);
            await Database.Execute(c, "SELECT 1", ct);
            await redis.GetDatabase().PingAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
