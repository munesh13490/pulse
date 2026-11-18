using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Pulse.Infrastructure.Repositories;

public sealed class RetentionRepository(IConfiguration config)
{
    public async Task RunAsync(CancellationToken ct)
    {
        // Dedicated local maintenance credentials, never shared with the request-serving API.
        await using var c = new NpgsqlConnection(config.GetConnectionString("Maintenance") ?? throw new InvalidOperationException("Set ConnectionStrings__Maintenance"));
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await Database.Execute(c, "SELECT pg_advisory_xact_lock(7343193); SELECT maintain_telemetry_partitions()", ct);
        var partitions = await Database.Query(c, "SELECT child.relname FROM pg_inherits JOIN pg_class parent ON parent.oid=inhparent JOIN pg_class child ON child.oid=inhrelid WHERE parent.relname='telemetry_events'", ct);
        foreach (var p in partitions)
        {
            var name = (string)p["relname"]!;
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^telemetry_events_\d{4}_\d{2}$") && DateTime.TryParseExact(name["telemetry_events_".Length..], "yyyy_MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var month) && month.AddMonths(1) < DateTime.UtcNow.AddDays(-90))
                await Database.Execute(c, $"DROP TABLE \"{name}\"", ct);
        }

        await Database.Execute(c, "DELETE FROM telemetry_idempotency WHERE created_at<now()-interval '90 days'; DELETE FROM outbox_messages WHERE processed_at<now()-interval '7 days'; DELETE FROM user_refresh_tokens WHERE expires_at<now()-interval '7 days'", ct);
        await tx.CommitAsync(ct);
    }
}
