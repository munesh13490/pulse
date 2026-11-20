using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace Pulse.Api.Configuration;

public static class ServiceRegistration
{
    public static IServiceCollection AddPulseApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPulse(configuration);
        // JWT and password hashing do not require persisted Data Protection keys.
        services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "pulse-dp", Environment.ProcessId.ToString()))).UseEphemeralDataProtectionProvider();
        services.AddOpenApi(options =>
        {
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.AddComponent("Bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                });
                document.AddComponent("DeviceKey", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = "X-Device-Key"
                });
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, _) =>
            {
                var description = context.Description;
                var metadata = description.ActionDescriptor.EndpointMetadata;
                var scheme = description.HttpMethod == "POST" && description.RelativePath == "v1/telemetry"
                    ? "DeviceKey"
                    : metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any()
                        ? "Bearer"
                        : null;
                if (scheme is not null)
                    operation.Security = [new() { [new OpenApiSecuritySchemeReference(scheme, context.Document)] = [] }];
                return Task.CompletedTask;
            });
        });
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
