using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pulse.Application;
using Pulse.Infrastructure.RateLimiting;

namespace Pulse.Infrastructure.DependencyInjection;

public static class ServiceRegistration
{
    public static IServiceCollection AddPulse(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>().Bind(configuration.GetSection(RateLimitingOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<Database>();
        services.AddSingleton<ISecretProvider, EnvironmentSecretProvider>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<IUserIdentityService, LocalIdentityProvider>();
        services.AddScoped<ILocalIdentityRepository, LocalIdentityRepository>();
        services.AddScoped<IDeviceCredentialValidator, PostgreSqlDeviceCredentialValidator>();
        services.AddScoped<ITelemetryRepository, TelemetryRepository>();
        services.AddSingleton<InMemoryRateLimiter>();
        services.AddSingleton<IRequestRateLimiter>(sp => sp.GetRequiredService<InMemoryRateLimiter>());
        services.AddSingleton<IEventRateLimiter>(sp => sp.GetRequiredService<InMemoryRateLimiter>());
        services.AddSingleton<IObservability, LocalObservability>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IDeviceKeyRepository, DeviceKeyRepository>();
        services.AddScoped<ITenantRateLimitRepository, TenantRateLimitRepository>();
        services.AddScoped<IRuleRepository, RuleRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IBootstrapService, BootstrapService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IDeviceKeyService, DeviceKeyService>();
        services.AddScoped<ITenantRateLimitService, TenantRateLimitService>();
        services.AddScoped<IRuleService, RuleService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<ITelemetryIngestionService, TelemetryIngestionService>();
        services.AddSingleton<TenantDatabase>();
        services.AddSingleton<IPasswordService, IdentityPasswordService>();
        services.AddSingleton<IDeviceKeyGenerator, DeviceKeyGenerator>();
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitingOptions>>().Value;
            return new TenantLimits(options.RequestsPerMinute, options.EventsPerMinute);
        });
        services.AddSingleton<TenantJobRunner>();
        services.AddSingleton<RuleEvaluationProcessor>();
        services.AddSingleton<RollupProcessor>();
        services.AddSingleton<RetentionRepository>();
        services.AddScoped<IHealthProbe, InfrastructureHealthProbe>();
        return services;
    }
}
