using System.Text.Json;
using Npgsql;

namespace Pulse.Infrastructure.Processing;

public sealed class RollupProcessor()
{
    public async Task Process(NpgsqlConnection c, Guid tenant, CancellationToken ct)
    {
        await Database.Execute(c, "INSERT INTO hourly_rollups(tenant_id,device_id,metric_name,hour_start,sample_count,min_value,max_value,sum_value,avg_value) SELECT tenant_id,device_id,metric_name,date_trunc('hour',event_timestamp),count(*),min(metric_value),max(metric_value),sum(metric_value),avg(metric_value) FROM telemetry_events WHERE event_timestamp>=date_trunc('hour',now()-interval '96 hours') GROUP BY tenant_id,device_id,metric_name,date_trunc('hour',event_timestamp) ON CONFLICT(tenant_id,device_id,metric_name,hour_start) DO UPDATE SET sample_count=excluded.sample_count,min_value=excluded.min_value,max_value=excluded.max_value,sum_value=excluded.sum_value,avg_value=excluded.avg_value,calculated_at=now()", ct);
    }
}
