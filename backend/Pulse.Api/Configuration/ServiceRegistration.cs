using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;

namespace Pulse.Api.Configuration;

public static class ServiceRegistration
{
    public static IServiceCollection AddPulseApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPulse(configuration);
        // JWT and password hashing do not require persisted Data Protection keys.
        services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "pulse-dp", Environment.ProcessId.ToString()))).UseEphemeralDataProtectionProvider();
        services.AddOpenApi();
        services.AddProblemDetails();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "pulse-local",
                ValidateAudience = true,
                ValidAudience = "pulse-dashboard",
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(10),
                NameClaimType = "name",
                RoleClaimType = "role",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new EnvironmentSecretProvider(configuration).Get("JwtSigningKey")))
            };
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("platform", p => p.RequireRole("platform-admin"));
            options.AddPolicy("admin", p => p.RequireRole("platform-admin", "tenant-admin"));
            options.AddPolicy("tenant-admin", p => p.RequireRole("tenant-admin"));
            options.AddPolicy("operate", p => p.RequireRole("tenant-admin", "operator"));
            options.AddPolicy("tenant", p => p.RequireClaim("tenant_id").RequireRole("tenant-admin", "operator", "viewer"));
        });
        services.AddScoped<AlertStreamService>();
        return services;
    }
}
