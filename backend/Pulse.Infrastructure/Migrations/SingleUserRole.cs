using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Pulse.Infrastructure.Migrations;

[DbContext(typeof(PulseDbContext))]
[Migration("20261119000100_SingleUserRole")]
public sealed class SingleUserRole : Migration
{
    internal const string ScriptSuffix = ".007_single_user_role.sql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var assembly = typeof(SingleUserRole).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(ScriptSuffix, StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        migrationBuilder.Sql(reader.ReadToEnd());
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Rollback requires an explicit backup/restore operation.");
}
