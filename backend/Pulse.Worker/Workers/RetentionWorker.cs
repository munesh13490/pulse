using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Pulse.Worker.Workers;

public sealed class RetentionWorker(RetentionRepository repository, ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await repository.RunAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Retention failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }
}
