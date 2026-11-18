using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args.Where(a => a is not ("--export-openapi" or "--migrate" or "--bootstrap")).ToArray());
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
builder.Host.UseSerilog((context, logger) => logger.WriteTo.Console(new RenderedCompactJsonFormatter()));
builder.Services.AddPulseApi(builder.Configuration);
var app = builder.Build();
if (await app.RunSetupCommandAsync(args))
    return;
app.UsePulseMiddleware();
var openApi = app.MapOpenApi();
if (!args.Contains("--export-openapi"))
    openApi.RequireAuthorization("admin");
app.MapPulseEndpoints();
app.Run();
public partial class Program;
