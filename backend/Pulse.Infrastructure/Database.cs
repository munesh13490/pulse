using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;
namespace Pulse.Infrastructure;

public sealed class PulseDbContext(DbContextOptions<PulseDbContext> options) : DbContext(options);
public sealed class DesignFactory : IDesignTimeDbContextFactory<PulseDbContext>
{
    public PulseDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PulseDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Migration") ?? throw new InvalidOperationException("Set ConnectionStrings__Migration")).Options);
}
public sealed class Database(IConfiguration configuration)
{
    public Task<NpgsqlConnection> Auth(CancellationToken ct = default) => Open("Auth", ct);
    public Task<NpgsqlConnection> App(CancellationToken ct = default) => Open("App", ct);
    private async Task<NpgsqlConnection> Open(string name, CancellationToken ct)
    {
        var c = new NpgsqlConnection(configuration.GetConnectionString(name) ?? throw new InvalidOperationException($"Missing ConnectionStrings:{name}"));
        await c.OpenAsync(ct);
        var expected = name == "App" ? "pulse_app" : "pulse_auth";
        var roles = await Query(c, "SELECT current_user AS name,rolsuper,rolbypassrls FROM pg_roles WHERE rolname=current_user", ct);
        if (roles.Count != 1 || (string)roles[0]["name"]! != expected || (bool)roles[0]["rolsuper"]! || (bool)roles[0]["rolbypassrls"]!)
        {
            await c.DisposeAsync();
            throw new InvalidOperationException($"Connection {name} must use restricted role {expected}.");
        }
        return c;
    }
    public static async Task<NpgsqlTransaction> Scope(NpgsqlConnection c, Guid tenant, CancellationToken ct = default)
    {
        var tx = await c.BeginTransactionAsync(ct);
        await Execute(c, "SELECT set_config('pulse.tenant_id',@tenant,true)", ct, ("tenant", tenant.ToString()));
        return tx;
    }
    public static async Task<int> Execute(NpgsqlConnection c, string sql, CancellationToken ct = default, params (string, object?)[] args)
    {
        await using var cmd = Command(c, sql, args);
        return await cmd.ExecuteNonQueryAsync(ct);
    }
    public static NpgsqlCommand Command(NpgsqlConnection c, string sql, params (string, object?)[] args)
    {
        var cmd = new NpgsqlCommand(sql, c);
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }
    public static async Task<List<Dictionary<string, object?>>> Query(NpgsqlConnection c, string sql, CancellationToken ct = default, params (string, object?)[] args)
    {
        await using var cmd = Command(c, sql, args);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }
}
