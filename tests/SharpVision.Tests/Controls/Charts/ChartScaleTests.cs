// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Charts;

/// <summary>Verifies authored chart scale construction, validation, and equality.</summary>
public sealed class ChartScaleTests
{
    /// <summary>Verifies the standard automatic scale has no authored bounds and includes zero.</summary>
    [Fact]
    public void Automatic_WhenRead_HasNoAuthoredBoundsAndIncludesZero()
    {
        // Arrange and act
        var scale = ChartScale.Automatic;

        // Assert
        scale.Minimum.ShouldBeNull();
        scale.Maximum.ShouldBeNull();
        scale.IncludeZero.ShouldBeTrue();
    }

    /// <summary>Verifies a validated construction round-trips its authored bounds and policy.</summary>
    [Fact]
    public void Constructor_WhenBoundsAreValid_RoundTripsMembers()
    {
        // Arrange and act
        var scale = new ChartScale(-10, 10, includeZero: false);

        // Assert
        scale.Minimum.ShouldBe(-10);
        scale.Maximum.ShouldBe(10);
        scale.IncludeZero.ShouldBeFalse();
    }

    /// <summary>Verifies a non-finite minimum or maximum is rejected.</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenMinimumIsNotFinite_Throws(double minimum) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ChartScale(minimum, 10, includeZero: false));

    /// <summary>Verifies a non-finite maximum is rejected.</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenMaximumIsNotFinite_Throws(double maximum) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ChartScale(-10, maximum, includeZero: false));

    /// <summary>Verifies a minimum at or above the maximum is rejected.</summary>
    [Fact]
    public void Constructor_WhenMinimumIsAtOrAboveMaximum_Throws()
    {
        // Act and assert
        _ = Should.Throw<ArgumentException>(() => new ChartScale(10, 10, includeZero: false));
        _ = Should.Throw<ArgumentException>(() => new ChartScale(11, 10, includeZero: false));
    }

    /// <summary>Verifies only one authored bound is permitted without triggering ordering validation.</summary>
    [Fact]
    public void Constructor_WhenOnlyOneBoundIsAuthored_Succeeds()
    {
        // Arrange and act
        var minimumOnly = new ChartScale(2, null, includeZero: true);
        var maximumOnly = new ChartScale(null, 8, includeZero: true);

        // Assert
        minimumOnly.Minimum.ShouldBe(2);
        minimumOnly.Maximum.ShouldBeNull();
        maximumOnly.Minimum.ShouldBeNull();
        maximumOnly.Maximum.ShouldBe(8);
    }

    /// <summary>Verifies equality and the corresponding operators compare every member.</summary>
    [Fact]
    public void Equals_WhenMembersMatch_ReportsEqualityAndOperators()
    {
        // Arrange
        var first = new ChartScale(0, 10, includeZero: true);
        var same = new ChartScale(0, 10, includeZero: true);
        var different = new ChartScale(0, 10, includeZero: false);

        // Act and assert
        first.Equals(same).ShouldBeTrue();
        (first == same).ShouldBeTrue();
        (first != different).ShouldBeTrue();
        first.GetHashCode().ShouldBe(same.GetHashCode());
    }
}
