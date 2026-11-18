namespace Pulse.Domain.Rules;

public sealed record MetricSample(DateTimeOffset At, double Value);
