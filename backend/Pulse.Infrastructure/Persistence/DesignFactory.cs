using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Pulse.Infrastructure.Persistence;

public sealed class DesignFactory : IDesignTimeDbContextFactory<PulseDbContext>
{
    public PulseDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PulseDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Migration") ?? throw new InvalidOperationException("Set ConnectionStrings__Migration")).Options);
}
