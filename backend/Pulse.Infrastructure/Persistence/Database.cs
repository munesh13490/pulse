using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Pulse.Infrastructure.Persistence;

public sealed class Database(IConfiguration configuration)
{
    public Task<NpgsqlConnection> Auth(CancellationToken ct = default) => Open("Auth", ct);
    public Task<NpgsqlConnection> App(CancellationToken ct = default) => Open("App", ct);
    private async Task<NpgsqlConnection> Open(string name, CancellationToken ct)
    {
        var c = CreateConnection(configuration, name);
        try
        {
            await c.OpenAsync(ct);
            return c;
        }
        catch
        {
            await c.DisposeAsync();
            throw;
        }
    }

    internal static NpgsqlConnection CreateConnection(IConfiguration configuration, string name)
    {
        var settings = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString(name)
            ?? throw new InvalidOperationException($"Missing ConnectionStrings:{name}"))
        {
            SearchPath = "pulse"
        };
        return new NpgsqlConnection(settings.ConnectionString);
    }

    public static async Task<NpgsqlTransaction> Scope(NpgsqlConnection c, Guid tenant, CancellationToken ct = default)
    {
        var tx = await c.BeginTransactionAsync(ct);
        await Execute(c, "SELECT set_config('app.tenant_id',@tenant,true)", ct, ("tenant", tenant.ToString()));
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
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
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
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        return rows;
    }
}
