using System.Net;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args.Where(a => a is not ("--export-openapi" or "--migrate" or "--bootstrap")).ToArray());
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
if (!builder.Configuration.GetValue<bool>("Database:AllowConnectionStringOverrides"))
{
    using var fileSettings = new ConfigurationManager();
    fileSettings.SetBasePath(builder.Environment.ContentRootPath)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
    var connectionKeys = builder.Configuration.GetSection("ConnectionStrings").GetChildren();
    var fileValues = connectionKeys.ToDictionary(key => key.Path, key => fileSettings[key.Path]);
    builder.Configuration.AddInMemoryCollection(fileValues);
}
builder.Host.UseSerilog((context, logger) => logger.WriteTo.Console(new RenderedCompactJsonFormatter()));
builder.Services.AddPulseApi(builder.Configuration);
var app = builder.Build();
if (await app.RunSetupCommandAsync(args))
    return;
app.UsePulseMiddleware();
// Enable Swagger UI
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/openapi/v1.json",
        "Pulse API v1"
    );

    // Swagger opens directly at root URL
    options.RoutePrefix = string.Empty;
});

// OpenAPI endpoint
app.MapOpenApi().AllowAnonymous();
app.MapPulseEndpoints();
app.Run();
public partial class Program;
