using Microsoft.Extensions.Hosting;
using Pulse.Infrastructure;
using Pulse.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddPulse(builder.Configuration);
builder.Services.AddHostedService<RuleEvaluationWorker>();
builder.Services.AddHostedService<RollupWorker>();
await builder.Build().RunAsync();
