using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Pulse.Tests.Unit.Architecture;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void RepositoryAndServiceGraphIsValidWithoutOpeningInfrastructureConnections()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddPulse(configuration);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRequestRateLimiter));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IEventRateLimiter));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.IsType<UserService>(scope.ServiceProvider.GetRequiredService<IUserService>());
        Assert.IsType<TenantService>(scope.ServiceProvider.GetRequiredService<ITenantService>());
        Assert.IsType<DeviceService>(scope.ServiceProvider.GetRequiredService<IDeviceService>());
        Assert.IsType<DeviceKeyService>(scope.ServiceProvider.GetRequiredService<IDeviceKeyService>());
        Assert.IsType<RuleService>(scope.ServiceProvider.GetRequiredService<IRuleService>());
        Assert.IsType<AlertService>(scope.ServiceProvider.GetRequiredService<IAlertService>());
        Assert.IsType<TelemetryIngestionService>(
            scope.ServiceProvider.GetRequiredService<ITelemetryIngestionService>());
        Assert.IsType<LocalIdentityRepository>(scope.ServiceProvider.GetRequiredService<ILocalIdentityRepository>());
    }
}
