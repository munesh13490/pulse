using Microsoft.EntityFrameworkCore;

namespace Pulse.Api.Configuration;

public static class SetupCommands
{
    public static async Task<bool> RunSetupCommandAsync(this WebApplication app, string[] args)
    {
        if (args.Contains("--migrate"))
        {
            var opts = new DbContextOptionsBuilder<PulseDbContext>().UseNpgsql(app.Configuration.GetConnectionString("Migration") ?? throw new InvalidOperationException("Set ConnectionStrings__Migration")).Options;
            await using var db = new PulseDbContext(opts);
            await db.Database.MigrateAsync();
            return true;
        }

        if (args.Contains("--bootstrap"))
        {
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IBootstrapService>().BootstrapAsync(app.Configuration["Bootstrap:Username"] ?? throw new InvalidOperationException("Set Bootstrap__Username"), app.Configuration["Bootstrap:Password"] ?? throw new InvalidOperationException("Set Bootstrap__Password"), default);
            Console.WriteLine("Platform administrator created. Remove Bootstrap__Password from your environment.");
            return true;
        }

        return false;
    }
}
