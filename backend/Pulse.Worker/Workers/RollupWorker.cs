using Microsoft.Extensions.Logging;
using Npgsql;

namespace Pulse.Worker.Workers;

public sealed class RollupWorker(TenantJobRunner runner, RollupProcessor processor) : TenantWorker(runner, TimeSpan.FromMinutes(1))
{
    protected override Task Process(NpgsqlConnection connection, Guid tenant, CancellationToken ct) => processor.Process(connection, tenant, ct);
}
