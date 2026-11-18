using System.Diagnostics.Metrics;
using Pulse.Application;
using StackExchange.Redis;

namespace Pulse.Infrastructure.Observability;

public sealed class LocalObservability : IObservability
{
    private static readonly Meter Meter = new("Pulse");
    private static readonly Counter<long> Operations = Meter.CreateCounter<long>("pulse.operations");
    public void Count(string operation) => Operations.Add(1, new KeyValuePair<string, object?>("operation", operation));
}
