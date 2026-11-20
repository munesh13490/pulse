using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Pulse.Infrastructure.Migrations;

[DbContext(typeof(PulseDbContext))]
[Migration("20261117000100_Initial")]
public sealed class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var assembly = typeof(Initial).Assembly;
        foreach (var name in assembly.GetManifestResourceNames()
            .Where(x => x.EndsWith(".sql", StringComparison.Ordinal) &&
                !x.EndsWith(SingleUserRole.ScriptSuffix, StringComparison.Ordinal) &&
                !x.EndsWith(".008_pulse_schema.sql", StringComparison.Ordinal) &&
                !x.EndsWith(".009_pulse_runtime.sql", StringComparison.Ordinal))
            .OrderBy(x => x, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            migrationBuilder.Sql(reader.ReadToEnd());
        }
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Destructive rollback requires an explicit backup/restore operation.");
}
