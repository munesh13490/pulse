using Pulse.Infrastructure;
using Pulse.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Pulse.Tests.Unit;

public sealed class MigrationTests
{
    [Fact]
    public void RoleEntityMapsToPulseRoles()
    {
        using var context = new PulseDbContext(new DbContextOptionsBuilder<PulseDbContext>().UseNpgsql("Host=localhost;Database=pulse;Username=postgres").Options);
        var role = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IEntityType>(context.Model.FindEntityType(typeof(Role)));
        Assert.Equal("pulse", role.GetSchema());
        Assert.Equal("roles", role.GetTableName());
    }

    [Fact]
    public void EfGeneratesExecutableSchemaWithoutConnecting()
    {
        using var context = new PulseDbContext(new DbContextOptionsBuilder<PulseDbContext>().UseNpgsql("Host=localhost;Database=pulse;Username=postgres").Options);
        var script = context.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TABLE tenants", script);
        Assert.Contains("FORCE ROW LEVEL SECURITY", script);
        Assert.Contains("maintain_telemetry_partitions", script);
        Assert.Contains("20261117000100_Initial", script);
        Assert.Contains("20261119000100_SingleUserRole", script);
        Assert.Contains("ALTER TABLE app_users ADD COLUMN role_id", script);
        Assert.Contains("20261120000100_PulseSchema", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS roles", script);
    }

    [Fact]
    public void MigrationEmbedsEverySchemaScriptInOrder()
    {
        var assembly = typeof(PulseDbContext).Assembly;
        var names = assembly.GetManifestResourceNames().Where(n => n.EndsWith(".sql")).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(8, names.Length);
        foreach (var (name, index) in names.Select((n, i) => (n, i)))
        {
            Assert.Contains($"00{index + 2}_", name);
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            Assert.False(string.IsNullOrWhiteSpace(reader.ReadToEnd()));
        }
    }
}
