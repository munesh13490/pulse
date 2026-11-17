using Pulse.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;
namespace Pulse.Tests.Unit;

public sealed class MigrationTests
{
    [Fact]
    public void EfGeneratesExecutableSchemaWithoutConnecting()
    {
        using var context = new PulseDbContext(new DbContextOptionsBuilder<PulseDbContext>().UseNpgsql("Host=localhost;Database=pulse;Username=postgres").Options);
        var script = context.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TABLE tenants", script);
        Assert.Contains("FORCE ROW LEVEL SECURITY", script);
        Assert.Contains("maintain_telemetry_partitions", script);
        Assert.Contains("20261117000100_Initial", script);
    }
    [Fact]
    public void MigrationEmbedsEverySchemaScriptInOrder()
    {
        var assembly = typeof(PulseDbContext).Assembly;
        var names = assembly.GetManifestResourceNames().Where(n => n.EndsWith(".sql")).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(5, names.Length);
        foreach (var (name, index) in names.Select((n, i) => (n, i)))
        {
            Assert.Contains($"00{index + 2}_", name);
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            Assert.False(string.IsNullOrWhiteSpace(reader.ReadToEnd()));
        }
    }
}
