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
var isDevelopment = app.Environment.IsDevelopment();
var enableHostedSwagger = !isDevelopment && app.Configuration.GetValue("OpenApi:EnableSwaggerUi", true);
//if (isDevelopment)
//{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        if ((path.StartsWithSegments("/swagger") || path.StartsWithSegments("/openapi")) &&
            (context.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address)))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next(context);
    });
//}
//if (isDevelopment || enableHostedSwagger)
//{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("../openapi/v1.json", "Pulse API v1");
    });
//}
var openApi = app.MapOpenApi();
if (enableHostedSwagger)
    openApi.AllowAnonymous();
else if (!isDevelopment && !args.Contains("--export-openapi"))
    openApi.RequireAuthorization("admin");
app.MapPulseEndpoints();
app.Run();
public partial class Program;
