// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

/// <summary>
/// Verifies the finite capability-query limit contract.
/// </summary>
public sealed class QueryLimitsTests
{
    /// <summary>
    /// Verifies that every integer limit rejects zero.
    /// </summary>
    [Fact]
    public void Constructor_WhenLimitIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new QueryLimits { MaxConcurrentQueries = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new QueryLimits { MaxCapabilityItems = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new QueryLimits { MaxCapabilityValueBytes = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new QueryLimits { QueryTimeout = TimeSpan.Zero });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () =>
            new QueryLimits { QueryTimeout = Timeout.InfiniteTimeSpan });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () =>
            new QueryLimits { QueryTimeout = TimeSpan.FromMilliseconds(int.MaxValue).Add(TimeSpan.FromMilliseconds(1)) });
    }

    /// <summary>
    /// Verifies that a <see cref="QueryLimits.QueryTimeout"/> at the dispatcher timer's
    /// millisecond ceiling is accepted rather than rejected as out of range.
    /// </summary>
    [Fact]
    public void QueryTimeout_WhenAtDispatcherTimerBound_IsAccepted()
    {
        var limits = new QueryLimits { QueryTimeout = TimeSpan.FromMilliseconds(int.MaxValue) };

        limits.QueryTimeout.ShouldBe(TimeSpan.FromMilliseconds(int.MaxValue));
    }

    /// <summary>
    /// Verifies that the default profile is bounded and suitable for an
    /// interactive terminal session.
    /// </summary>
    [Fact]
    public void Default_WhenRead_HasFiniteInteractiveBounds()
    {
        var limits = QueryLimits.Default;

        limits.MaxConcurrentQueries.ShouldBeInRange(1, 1_024);
        limits.MaxCapabilityItems.ShouldBeInRange(1, 256);
        limits.MaxCapabilityValueBytes.ShouldBeInRange(1, 65_536);
        limits.QueryTimeout.ShouldBeGreaterThan(TimeSpan.Zero);
        limits.QueryTimeout.ShouldBeLessThan(TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Verifies the bounded-limit exception message keeps invariant ASCII digits even when
    /// the current culture would otherwise substitute native digit glyphs.
    /// </summary>
    [Fact]
    public void MaxCapabilityItems_WhenCultureUsesNonAsciiDigits_MessageUsesInvariantDigits()
    {
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");

            var exception = Should.Throw<ArgumentOutOfRangeException>(static () =>
                new QueryLimits { MaxCapabilityItems = 0 });

            exception.Message.ShouldContain("256");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
