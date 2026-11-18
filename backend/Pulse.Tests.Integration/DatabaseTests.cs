using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Pulse.Application;
using Pulse.Domain;
using Pulse.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;

namespace Pulse.Tests.Integration;

public sealed class DatabaseTests
{
    [DockerFact]
    public async Task MigrationsRlsConcurrentIngestionAndKeyRotation()
    {
        await using var pg = new PostgreSqlBuilder("postgres:17").Build();
        await pg.StartAsync();
        await using (var root = new NpgsqlConnection(pg.GetConnectionString()))
        {
            await root.OpenAsync();
            await Database.Execute(root, "CREATE ROLE pulse_app LOGIN PASSWORD 'test-app-only' NOSUPERUSER NOBYPASSRLS; CREATE ROLE pulse_auth LOGIN PASSWORD 'test-auth-only' NOSUPERUSER NOBYPASSRLS");
        }

        var opts = new DbContextOptionsBuilder<PulseDbContext>().UseNpgsql(pg.GetConnectionString()).Options;
        await using (var context = new PulseDbContext(opts))
            await context.Database.MigrateAsync();
        var app = new NpgsqlConnectionStringBuilder(pg.GetConnectionString())
        {
            Username = "pulse_app",
            Password = "test-app-only"
        }.ConnectionString;
        var auth = new NpgsqlConnectionStringBuilder(pg.GetConnectionString())
        {
            Username = "pulse_auth",
            Password = "test-auth-only"
        }.ConnectionString;
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:App"] = app, ["ConnectionStrings:Auth"] = auth, ["Secrets:ApiKeyHmacKey"] = KeyMaterial.Random(), ["Secrets:JwtSigningKey"] = KeyMaterial.Random() }).Build();
        var db = new Database(cfg);
        var secrets = new EnvironmentSecretProvider(cfg);
        var tenantDatabase = new TenantDatabase(db);
        var tenants = new TenantService(new TenantRepository(db), new TenantRateLimitRepository(tenantDatabase));
        var users = new UserService(new UserRepository(db), new IdentityPasswordService());
        var keys = new DeviceKeyService(new DeviceKeyRepository(tenantDatabase), new DeviceKeyGenerator(secrets));
        var platform = new UserIdentity(Guid.NewGuid(), null, "platform", ["platform-admin"]);
        var a = await tenants.CreateAsync("A", default);
        var b = await tenants.CreateAsync("B", default);
        var device = Guid.NewGuid();
        await using (var c = await db.App())
        {
            await using var tx = await Database.Scope(c, a);
            await Database.Execute(c, "INSERT INTO devices(id,tenant_id,name) VALUES(@id,@t,'sensor')", default, ("id", device), ("t", a));
            await tx.CommitAsync();
        }

        await using (var c = await db.App())
        {
            await using (var tx = await Database.Scope(c, b))
            {
                Assert.Empty(await Database.Query(c, "SELECT id FROM devices"));
                await Assert.ThrowsAsync<PostgresException>(() => Database.Execute(c, "INSERT INTO devices(id,tenant_id,name) VALUES(@id,@t,'forbidden')", default, ("id", Guid.NewGuid()), ("t", a)));
                await tx.RollbackAsync();
            }

            Assert.Empty(await Database.Query(c, "SELECT id FROM devices")); // no leaked pooled/transaction context
        }

        var repository = new TelemetryRepository(db);
        var item = new TelemetryItem(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-1), "temp", 42);
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => repository.Ingest(new(a, device), [item], default)));
        Assert.Single(results.SelectMany(x => x), x => x.Status == "accepted");
        Assert.Equal(11, results.SelectMany(x => x).Count(x => x.Status == "duplicate"));
        await using (var c = await db.App())
        {
            await using var tx = await Database.Scope(c, a);
            foreach (var table in new[]
            {
                "telemetry_events",
                "telemetry_idempotency",
                "outbox_messages"
            }

            )
            {
                var rows = await Database.Query(c, $"SELECT count(*) AS count FROM {table}");
                Assert.Equal(1L, rows[0]["count"]);
            }
        }

        Assert.Null(await keys.IssueAsync(b, device, DateTimeOffset.UtcNow.AddDays(1), default));
        var first = await keys.IssueAsync(a, device, DateTimeOffset.UtcNow.AddDays(1), default);
        Assert.NotNull(first);
        var validator = new PostgreSqlDeviceCredentialValidator(db, secrets);
        var firstKey = first.ApiKey;
        Assert.NotNull(await validator.Validate(firstKey, default));
        await keys.IssueAsync(a, device, DateTimeOffset.UtcNow.AddDays(1), default);
        Assert.Null(await validator.Validate(firstKey, default));
        var owner = new UserIdentity(Guid.NewGuid(), a, "admin", ["tenant-admin"]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => users.CreateAsync(owner, b, "bad-user", KeyMaterial.Random(), "viewer", default));
        await users.CreateAsync(platform, a, "viewer", KeyMaterial.Random(), "viewer", default);
        var password = KeyMaterial.Random();
        var uid = await users.CreateAsync(platform, a, "operator", password, "operator", default);
        var identity = new LocalIdentityProvider(new LocalIdentityRepository(db), new JwtTokenIssuer(secrets));
        var pair = await identity.Login("operator", password, default);
        Assert.NotNull(pair);
        var refreshes = await Task.WhenAll(identity.Refresh(pair.RefreshToken, default), identity.Refresh(pair.RefreshToken, default));
        Assert.Single(refreshes, x => x is not null);
        Assert.NotNull(await identity.Find(uid, default));
    }
}
