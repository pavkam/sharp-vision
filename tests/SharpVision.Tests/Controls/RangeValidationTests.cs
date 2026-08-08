// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the shared Minimum/Maximum endpoint guards controls delegate to.</summary>
public sealed class RangeValidationTests
{
    /// <summary>Verifies a strictly ordered pair passes the inclusive "at or above" guard.</summary>
    [Fact]
    public void ThrowIfAtOrAboveMaximum_WhenMinimumIsBelowMaximum_DoesNotThrow() =>
        Should.NotThrow(() => 1.0.ThrowIfAtOrAboveMaximum(2.0, "value", "message"));

    /// <summary>Verifies an ordinary equal pair is rejected.</summary>
    [Fact]
    public void ThrowIfAtOrAboveMaximum_WhenEndpointsAreEqual_Throws() =>
        Should.Throw<ArgumentException>(() => 1.0.ThrowIfAtOrAboveMaximum(1.0, "value", "message"));

    /// <summary>Verifies -0.0 and +0.0 - numerically equal but distinct bit patterns - are rejected,
    /// since <see cref="double.CompareTo(double)"/> reports them as equal.</summary>
    [Fact]
    public void ThrowIfAtOrAboveMaximum_WhenEndpointsAreNegativeAndPositiveZero_Throws() =>
        Should.Throw<ArgumentException>(() => (-0.0).ThrowIfAtOrAboveMaximum(0.0, "value", "message"));

    /// <summary>Verifies a minimum above the maximum is rejected.</summary>
    [Fact]
    public void ThrowIfAtOrAboveMaximum_WhenMinimumExceedsMaximum_Throws() =>
        Should.Throw<ArgumentException>(() => 3.0.ThrowIfAtOrAboveMaximum(2.0, "value", "message"));

    /// <summary>Verifies a strictly ordered pair passes the inclusive "at or below" guard.</summary>
    [Fact]
    public void ThrowIfAtOrBelowMinimum_WhenMaximumIsAboveMinimum_DoesNotThrow() =>
        Should.NotThrow(() => 2.0.ThrowIfAtOrBelowMinimum(1.0, "value", "message"));

    /// <summary>Verifies an ordinary equal pair is rejected.</summary>
    [Fact]
    public void ThrowIfAtOrBelowMinimum_WhenEndpointsAreEqual_Throws() =>
        Should.Throw<ArgumentException>(() => 1.0.ThrowIfAtOrBelowMinimum(1.0, "value", "message"));

    /// <summary>Verifies -0.0 and +0.0 - numerically equal but distinct bit patterns - are rejected,
    /// since <see cref="double.CompareTo(double)"/> reports them as equal.</summary>
    [Fact]
    public void ThrowIfAtOrBelowMinimum_WhenEndpointsArePositiveAndNegativeZero_Throws() =>
        Should.Throw<ArgumentException>(() => 0.0.ThrowIfAtOrBelowMinimum(-0.0, "value", "message"));

    /// <summary>Verifies a maximum below the minimum is rejected.</summary>
    [Fact]
    public void ThrowIfAtOrBelowMinimum_WhenMaximumIsBelowMinimum_Throws() =>
        Should.Throw<ArgumentException>(() => 1.0.ThrowIfAtOrBelowMinimum(2.0, "value", "message"));

    /// <summary>Verifies a minimum equal to the maximum passes the non-strict "above maximum" guard.</summary>
    [Fact]
    public void ThrowIfAboveMaximum_WhenMinimumEqualsMaximum_DoesNotThrow() =>
        Should.NotThrow(() => 1.0.ThrowIfAboveMaximum(1.0, "value", "message"));

    /// <summary>Verifies a minimum above the maximum is rejected.</summary>
    [Fact]
    public void ThrowIfAboveMaximum_WhenMinimumExceedsMaximum_Throws() =>
        Should.Throw<ArgumentException>(() => 2.0.ThrowIfAboveMaximum(1.0, "value", "message"));

    /// <summary>Verifies a maximum equal to the minimum passes the non-strict "below minimum" guard.</summary>
    [Fact]
    public void ThrowIfBelowMinimum_WhenMaximumEqualsMinimum_DoesNotThrow() =>
        Should.NotThrow(() => 1.0.ThrowIfBelowMinimum(1.0, "value", "message"));

    /// <summary>Verifies a maximum below the minimum is rejected.</summary>
    [Fact]
    public void ThrowIfBelowMinimum_WhenMaximumIsBelowMinimum_Throws() =>
        Should.Throw<ArgumentException>(() => 1.0.ThrowIfBelowMinimum(2.0, "value", "message"));

    /// <summary>Verifies a value inside the inclusive range passes.</summary>
    [Fact]
    public void ThrowIfOutsideInclusiveRange_WhenValueIsInsideRange_DoesNotThrow() =>
        Should.NotThrow(() => 5.0.ThrowIfOutsideInclusiveRange(1.0, 10.0, "value", "message"));

    /// <summary>Verifies a value at either inclusive endpoint passes.</summary>
    [Fact]
    public void ThrowIfOutsideInclusiveRange_WhenValueIsAtAnEndpoint_DoesNotThrow()
    {
        Should.NotThrow(() => 1.0.ThrowIfOutsideInclusiveRange(1.0, 10.0, "value", "message"));
        Should.NotThrow(() => 10.0.ThrowIfOutsideInclusiveRange(1.0, 10.0, "value", "message"));
    }

    /// <summary>Verifies a value below the range is rejected.</summary>
    [Fact]
    public void ThrowIfOutsideInclusiveRange_WhenValueIsBelowMinimum_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => 0.0.ThrowIfOutsideInclusiveRange(1.0, 10.0, "value", "message"));

    /// <summary>Verifies a value above the range is rejected.</summary>
    [Fact]
    public void ThrowIfOutsideInclusiveRange_WhenValueIsAboveMaximum_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => 11.0.ThrowIfOutsideInclusiveRange(1.0, 10.0, "value", "message"));
}
