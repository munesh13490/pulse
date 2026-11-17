using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pulse.Application;
using StackExchange.Redis;
namespace Pulse.Infrastructure;

public static class Registration
{
    public static IServiceCollection AddPulse(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>().Bind(configuration.GetSection(RateLimitingOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<Database>();
        services.AddSingleton<ISecretProvider, EnvironmentSecretProvider>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<IUserIdentityService, LocalIdentityProvider>();
        services.AddScoped<IDeviceCredentialValidator, PostgreSqlDeviceCredentialValidator>();
        services.AddScoped<ITelemetryRepository, TelemetryRepository>();
        services.AddScoped<Administration>();
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("Missing Redis connection")));
        services.AddSingleton<IRequestRateLimiter, RedisRequestRateLimiter>();
        services.AddSingleton<IEventRateLimiter, RedisEventRateLimiter>();
        services.AddSingleton<INotificationBus, RedisNotificationBus>();
        services.AddSingleton<IObservability, LocalObservability>();
        return services;
    }
}
