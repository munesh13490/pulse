using Microsoft.Extensions.Logging;
using Npgsql;

namespace Pulse.Worker.Workers;

public sealed class OutboxWorker(TenantJobRunner runner, OutboxProcessor processor) : TenantWorker(runner, TimeSpan.FromSeconds(1))
{
    protected override Task Process(NpgsqlConnection connection, Guid tenant, CancellationToken ct) => processor.Process(connection, tenant, ct);
}
