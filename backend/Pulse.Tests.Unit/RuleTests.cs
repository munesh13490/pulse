using Pulse.Domain;
using Xunit;
namespace Pulse.Tests.Unit;

public sealed class RuleTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.UnixEpoch;
    [Fact] public void ThresholdUsesWindowAverage() => Assert.Equal(15d, RuleEvaluation.Evaluate("threshold", [new(At, 10), new(At.AddSeconds(5), 20)]));
    [Fact] public void RateUsesElapsedSeconds() => Assert.Equal(2d, RuleEvaluation.Evaluate("rate-of-change", [new(At, 10), new(At.AddSeconds(5), 20)]));
    [Fact] public void RateRequiresDistinctTimes() => Assert.Null(RuleEvaluation.Evaluate("rate-of-change", [new(At, 10), new(At, 20)]));
    [Fact] public void EmptyWindowDoesNotTrigger() => Assert.Null(RuleEvaluation.Evaluate("threshold", []));
}
