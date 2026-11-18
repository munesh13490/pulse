using Microsoft.Extensions.Logging;
using Npgsql;

namespace Pulse.Worker.Workers;

public sealed class RuleEvaluationWorker(TenantJobRunner runner, RuleEvaluationProcessor processor) : TenantWorker(runner, TimeSpan.FromSeconds(5))
{
    protected override Task Process(NpgsqlConnection connection, Guid tenant, CancellationToken ct) => processor.Process(connection, tenant, ct);
}
