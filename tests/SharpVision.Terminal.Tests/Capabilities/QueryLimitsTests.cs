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
}
