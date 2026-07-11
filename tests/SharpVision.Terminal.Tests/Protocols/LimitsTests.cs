using SharpVision.Terminal.Protocols;

using Shouldly;

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>
/// Verifies the finite protocol limit contract.
/// </summary>
public sealed class LimitsTests
{
    /// <summary>
    /// Verifies that every integer limit rejects zero.
    /// </summary>
    [Fact]
    public void Constructor_WhenLimitIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(
            static () => new Limits { MaxParameterBytes = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(
            static () => new Limits { MaxIntermediateBytes = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(
            static () => new Limits { MaxStringBytes = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(
            static () => new Limits { MaxClipboardBytes = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(
            static () => new Limits { MaxMetadataBytes = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(
            static () => new Limits { MaxConcurrentQueries = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(
            static () => new Limits { QueryTimeout = TimeSpan.Zero });
        _ = Should.Throw<ArgumentOutOfRangeException>(
            static () => new Limits { QueryTimeout = Timeout.InfiniteTimeSpan });
    }

    /// <summary>
    /// Verifies that the default profile is bounded and suitable for an
    /// interactive terminal session.
    /// </summary>
    [Fact]
    public void Default_WhenRead_HasFiniteInteractiveBounds()
    {
        var limits = Limits.Default;

        limits.MaxParameterBytes.ShouldBeInRange(1, 4_096);
        limits.MaxIntermediateBytes.ShouldBeInRange(1, 256);
        limits.MaxStringBytes.ShouldBeInRange(1, 16_777_216);
        limits.MaxClipboardBytes.ShouldBeInRange(limits.MaxStringBytes, 67_108_864);
        limits.MaxMetadataBytes.ShouldBeInRange(1, 65_536);
        limits.MaxConcurrentQueries.ShouldBeInRange(1, 1_024);
        limits.QueryTimeout.ShouldBeGreaterThan(TimeSpan.Zero);
        limits.QueryTimeout.ShouldBeLessThan(TimeSpan.FromMinutes(1));
    }
}
