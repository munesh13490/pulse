using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Pulse.Infrastructure.Migrations;

[DbContext(typeof(PulseDbContext))]
[Migration("20261120000100_PulseSchema")]
public sealed class PulseSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var assembly = typeof(PulseSchema).Assembly;
        foreach (var suffix in new[] { ".008_pulse_schema.sql", ".009_pulse_runtime.sql" })
        {
            var name = assembly.GetManifestResourceNames().Single(resource => resource.EndsWith(suffix, StringComparison.Ordinal));
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            migrationBuilder.Sql(reader.ReadToEnd());
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Rollback requires restoring a backup.");
}
