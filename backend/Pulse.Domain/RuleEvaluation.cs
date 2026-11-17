namespace Pulse.Domain;

public sealed record MetricSample(DateTimeOffset At, double Value);
public static class RuleEvaluation
{
    public static double? Evaluate(string kind, IReadOnlyList<MetricSample> samples)
    {
        if (samples.Count == 0) return null;
        if (kind == "threshold") return samples.Average(s => s.Value);
        if (kind != "rate-of-change") throw new ArgumentException("Unsupported rule type", nameof(kind));
        if (samples.Count < 2) return null;
        var seconds = (samples[^1].At - samples[0].At).TotalSeconds;
        return seconds <= 0 ? null : (samples[^1].Value - samples[0].Value) / seconds;
    }
}
