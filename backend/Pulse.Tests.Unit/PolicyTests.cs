using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Pulse.Domain;
using Pulse.Infrastructure;
using Xunit;

namespace Pulse.Tests.Unit;

public sealed class PolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    [Theory]
    [InlineData(-72, true)]
    [InlineData(-73, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void EventTimeBoundary(int hours, bool valid) => Assert.Equal(valid, TelemetryPolicy.Validate(new(Guid.NewGuid(), Now.AddHours(hours), "temp", 12), Now) is null);
    [Fact]
    public void EmptyEventIdRejected() => Assert.NotNull(TelemetryPolicy.Validate(new(Guid.Empty, Now, "temp", 1), Now));
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteValuesRejected(double value) => Assert.NotNull(TelemetryPolicy.Validate(new(Guid.NewGuid(), Now, "temp", value), Now));
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyMetricRejected(string metric) => Assert.NotNull(TelemetryPolicy.Validate(new(Guid.NewGuid(), Now, metric, 1), Now));
    [Theory]
    [InlineData("OPEN", "ACKNOWLEDGED", true)]
    [InlineData("OPEN", "RESOLVED", true)]
    [InlineData("ACKNOWLEDGED", "RESOLVED", true)]
    [InlineData("RESOLVED", "OPEN", false)]
    [InlineData("ACKNOWLEDGED", "OPEN", false)]
    [InlineData("OPEN", "OPEN", false)]
    public void AlertTransitions(string from, string to, bool allowed) => Assert.Equal(allowed, AlertState.CanTransition(from, to));
    [Fact]
    public void KeyDigestsRequireSamePepper()
    {
        var key = KeyMaterial.Random();
        var pepper = KeyMaterial.Random();
        Assert.True(CryptographicOperations.FixedTimeEquals(KeyMaterial.Digest(key, pepper), KeyMaterial.Digest(key, pepper)));
        Assert.False(CryptographicOperations.FixedTimeEquals(KeyMaterial.Digest(key, pepper), KeyMaterial.Digest(key, KeyMaterial.Random())));
        Assert.DoesNotContain(key, Convert.ToHexString(KeyMaterial.Digest(key, pepper)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PasswordsAreSaltedIdentityHashes()
    {
        var h = new PasswordHasher<string>();
        var a = h.HashPassword("user", "password-long-enough");
        var b = h.HashPassword("user", "password-long-enough");
        Assert.NotEqual(a, b);
        Assert.Equal(PasswordVerificationResult.Success, h.VerifyHashedPassword("user", a, "password-long-enough"));
        Assert.Equal(PasswordVerificationResult.Failed, h.VerifyHashedPassword("user", a, "wrong"));
    }
}
