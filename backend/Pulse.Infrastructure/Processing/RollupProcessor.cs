using System.Text.Json;
using Npgsql;

namespace Pulse.Infrastructure.Processing;

public sealed class RollupProcessor()
{
    public async Task Process(NpgsqlConnection c, Guid tenant, CancellationToken ct)
    {
        await Database.Execute(c, "INSERT INTO hourly_rollups(id,tenant_id,device_id,metric,hour,count,sum,min,max) SELECT gen_random_uuid(),tenant_id,device_id,metric,date_trunc('hour',occurred_at),count(*),sum(value),min(value),max(value) FROM telemetry_events WHERE occurred_at>=date_trunc('hour',now()-interval '96 hours') GROUP BY tenant_id,device_id,metric,date_trunc('hour',occurred_at) ON CONFLICT(tenant_id,device_id,metric,hour) DO UPDATE SET count=excluded.count,sum=excluded.sum,min=excluded.min,max=excluded.max", ct);
    }
}
