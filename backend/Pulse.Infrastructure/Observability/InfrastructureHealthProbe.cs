namespace Pulse.Infrastructure.Observability;

public sealed class InfrastructureHealthProbe(Database db) : IHealthProbe
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        try
        {
            await using var c = await db.App(ct);
            await Database.Execute(c, "SELECT 1", ct);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
