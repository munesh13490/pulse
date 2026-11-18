using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Pulse.Worker.Workers;

public abstract class TenantWorker(TenantJobRunner runner, TimeSpan interval) : BackgroundService
{
    protected abstract Task Process(NpgsqlConnection connection, Guid tenant, CancellationToken ct);
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await runner.RunAsync(GetType().Name, Process, ct);
            await Task.Delay(interval, ct);
        }
    }
}
