// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Charts;

/// <summary>Verifies deterministic automatic chart-scale resolution.</summary>
public sealed class ChartScaleResolverTests
{
    /// <summary>Verifies mixed values preserve their extrema when zero is already included.</summary>
    [Fact]
    public void Resolve_WhenValuesAreMixedAndZeroIsIncluded_UsesDataExtrema()
    {
        // Arrange
        var values = new[] { -4d, 8d };

        // Act
        var range = ChartScaleResolver.Resolve(ChartScale.Automatic, values);

        // Assert
        range.Minimum.ShouldBe(-4);
        range.Maximum.ShouldBe(8);
    }

    /// <summary>Verifies positive values include zero for the standard automatic scale.</summary>
    [Fact]
    public void Resolve_WhenValuesArePositiveAndZeroIsIncluded_UsesZeroMinimum()
    {
        // Arrange and act
        var range = ChartScaleResolver.Resolve(ChartScale.Automatic, [4d, 8d]);

        // Assert
        range.Minimum.ShouldBe(0);
        range.Maximum.ShouldBe(8);
    }

    /// <summary>Verifies automatic trend scales preserve small non-zero variation.</summary>
    [Fact]
    public void Resolve_WhenZeroIsExcluded_UsesObservedExtrema()
    {
        // Arrange
        var scale = new ChartScale(minimum: null, maximum: null, includeZero: false);

        // Act
        var range = ChartScaleResolver.Resolve(scale, [101d, 103d]);

        // Assert
        range.Minimum.ShouldBe(101);
        range.Maximum.ShouldBe(103);
    }

    /// <summary>Verifies a constant automatic range receives deterministic symmetric space.</summary>
    [Fact]
    public void Resolve_WhenValuesAreConstant_ExpandsSymmetrically()
    {
        // Arrange and act
        var range = ChartScaleResolver.Resolve(
            new ChartScale(minimum: null, maximum: null, includeZero: false),
            [5d, 5d]);

        // Assert
        range.Minimum.ShouldBe(4);
        range.Maximum.ShouldBe(6);
    }

    /// <summary>Verifies empty automatic data has a stable useful range.</summary>
    [Fact]
    public void Resolve_WhenValuesAreEmpty_UsesZeroToOne()
    {
        // Arrange and act
        var range = ChartScaleResolver.Resolve(ChartScale.Automatic, ReadOnlySpan<double>.Empty);

        // Assert
        range.Minimum.ShouldBe(0);
        range.Maximum.ShouldBe(1);
    }

    /// <summary>Verifies explicit bounds override observed values.</summary>
    [Fact]
    public void Resolve_WhenBoundsAreExplicit_UsesAuthoredRange()
    {
        // Arrange
        var scale = new ChartScale(-10, 10, includeZero: false);

        // Act
        var range = ChartScaleResolver.Resolve(scale, [-50d, 50d]);

        // Assert
        range.Minimum.ShouldBe(-10);
        range.Maximum.ShouldBe(10);
    }

    /// <summary>Verifies the series overload scans every series' points for extrema, matching the
    /// span overload's result for the same underlying values, instead of only inspecting one series
    /// or requiring the values to be pre-collected into an array.</summary>
    [Fact]
    public void Resolve_WithSeries_WhenValuesAreMixedAndZeroIsIncluded_UsesDataExtremaAcrossAllSeries()
    {
        // Arrange
        var series = new ChartSeries[]
        {
            new("A", [new ChartDataPoint("1", -4), new ChartDataPoint("2", 3)]),
            new("B", [new ChartDataPoint("1", 8)]),
        };

        // Act
        var range = ChartScaleResolver.Resolve(ChartScale.Automatic, series);

        // Assert
        range.Minimum.ShouldBe(-4);
        range.Maximum.ShouldBe(8);
    }

    /// <summary>Verifies negative-only values resolve to their observed extrema, not a zero-anchored
    /// range, when zero inclusion is disabled.</summary>
    [Fact]
    public void Resolve_WithSeries_WhenValuesAreNegative_UsesObservedExtrema()
    {
        // Arrange
        var scale = new ChartScale(minimum: null, maximum: null, includeZero: false);
        ChartSeries[] series = [new("A", [new ChartDataPoint("1", -103), new ChartDataPoint("2", -101)])];

        // Act
        var range = ChartScaleResolver.Resolve(scale, series);

        // Assert
        range.Minimum.ShouldBe(-103);
        range.Maximum.ShouldBe(-101);
    }

    /// <summary>Verifies a single point - the smallest non-empty input - collapses to the same
    /// symmetric expansion as an all-equal array, rather than throwing or dividing by zero.</summary>
    [Fact]
    public void Resolve_WithSeries_WhenSeriesHasOnePoint_ExpandsSymmetrically()
    {
        // Arrange
        ChartSeries[] series = [new("A", [new ChartDataPoint("1", 5)])];

        // Act
        var range = ChartScaleResolver.Resolve(
            new ChartScale(minimum: null, maximum: null, includeZero: false),
            series);

        // Assert
        range.Minimum.ShouldBe(4);
        range.Maximum.ShouldBe(6);
    }

    /// <summary>Verifies an empty series list resolves the same stable default range as an empty
    /// value array, preserving the array overload's empty-input behavior exactly.</summary>
    [Fact]
    public void Resolve_WithSeries_WhenSeriesListIsEmpty_UsesZeroToOne()
    {
        // Arrange and act
        var range = ChartScaleResolver.Resolve(ChartScale.Automatic, Array.Empty<ChartSeries>());

        // Assert
        range.Minimum.ShouldBe(0);
        range.Maximum.ShouldBe(1);
    }

    /// <summary>Verifies series that are present but contain no points are treated as having no
    /// observed values at all, the same as an empty series list or an empty value array.</summary>
    [Fact]
    public void Resolve_WithSeries_WhenAllSeriesHaveNoPoints_UsesZeroToOne()
    {
        // Arrange
        ChartSeries[] series = [new ChartSeries("A"), new ChartSeries("B")];

        // Act
        var range = ChartScaleResolver.Resolve(ChartScale.Automatic, series);

        // Assert
        range.Minimum.ShouldBe(0);
        range.Maximum.ShouldBe(1);
    }

    /// <summary>Verifies explicit bounds override observed series values, ignoring point extrema
    /// entirely, matching the span overload's authored-range precedence.</summary>
    [Fact]
    public void Resolve_WithSeries_WhenBoundsAreExplicit_UsesAuthoredRange()
    {
        // Arrange
        var scale = new ChartScale(-10, 10, includeZero: false);
        ChartSeries[] series = [new("A", [new ChartDataPoint("1", -50), new ChartDataPoint("2", 50)])];

        // Act
        var range = ChartScaleResolver.Resolve(scale, series);

        // Assert
        range.Minimum.ShouldBe(-10);
        range.Maximum.ShouldBe(10);
    }

    /// <summary>Verifies empty data with only an authored minimum expands one whole unit above it
    /// when zero inclusion would not otherwise reach further, matching <c>ResolveEmpty</c>'s
    /// minimum-only branch instead of the both-bounds or maximum-only branches.</summary>
    [Fact]
    public void Resolve_WhenValuesAreEmptyAndOnlyMinimumIsAuthored_ExpandsOneUnitAbove()
    {
        // Arrange
        var scale = new ChartScale(minimum: 5, maximum: null, includeZero: false);

        // Act
        var range = ChartScaleResolver.Resolve(scale, ReadOnlySpan<double>.Empty);

        // Assert
        range.Minimum.ShouldBe(5);
        range.Maximum.ShouldBe(6);
    }

    /// <summary>Verifies empty data with only an authored negative minimum and zero inclusion
    /// enabled expands to zero instead of one unit above the minimum, since zero is further away
    /// and inclusion is requested.</summary>
    [Fact]
    public void Resolve_WhenValuesAreEmptyAndOnlyMinimumIsAuthoredWithZeroIncluded_ExpandsToZero()
    {
        // Arrange
        var scale = new ChartScale(minimum: -5, maximum: null, includeZero: true);

        // Act
        var range = ChartScaleResolver.Resolve(scale, ReadOnlySpan<double>.Empty);

        // Assert
        range.Minimum.ShouldBe(-5);
        range.Maximum.ShouldBe(0);
    }

    /// <summary>Verifies empty data with only an authored maximum expands one whole unit below it,
    /// matching <c>ResolveEmpty</c>'s maximum-only branch.</summary>
    [Fact]
    public void Resolve_WhenValuesAreEmptyAndOnlyMaximumIsAuthored_ExpandsOneUnitBelow()
    {
        // Arrange
        var scale = new ChartScale(minimum: null, maximum: 5, includeZero: false);

        // Act
        var range = ChartScaleResolver.Resolve(scale, ReadOnlySpan<double>.Empty);

        // Assert
        range.Minimum.ShouldBe(4);
        range.Maximum.ShouldBe(5);
    }

    /// <summary>Verifies empty data with only an authored positive maximum and zero inclusion
    /// enabled expands down to zero instead of one unit below the maximum.</summary>
    [Fact]
    public void Resolve_WhenValuesAreEmptyAndOnlyMaximumIsAuthoredWithZeroIncluded_ExpandsToZero()
    {
        // Arrange
        var scale = new ChartScale(minimum: null, maximum: 5, includeZero: true);

        // Act
        var range = ChartScaleResolver.Resolve(scale, ReadOnlySpan<double>.Empty);

        // Assert
        range.Minimum.ShouldBe(0);
        range.Maximum.ShouldBe(5);
    }

    /// <summary>Verifies empty data with both bounds authored uses the authored range directly,
    /// without consulting <see cref="ChartScale.IncludeZero"/> at all.</summary>
    [Fact]
    public void Resolve_WhenValuesAreEmptyAndBothBoundsAreAuthored_UsesAuthoredRangeDirectly()
    {
        // Arrange
        var scale = new ChartScale(minimum: 2, maximum: 9, includeZero: true);

        // Act
        var range = ChartScaleResolver.Resolve(scale, ReadOnlySpan<double>.Empty);

        // Assert
        range.Minimum.ShouldBe(2);
        range.Maximum.ShouldBe(9);
    }

    /// <summary>Verifies a collapsed observed range with only an authored minimum expands
    /// upward from that fixed minimum instead of symmetrically, matching <c>ExpandCollapsed</c>'s
    /// minimum-authored branch.</summary>
    [Fact]
    public void Resolve_WhenValuesCollapseAndOnlyMinimumIsAuthored_ExpandsUpwardFromMinimum()
    {
        // Arrange
        var scale = new ChartScale(minimum: 5, maximum: null, includeZero: false);

        // Act
        var range = ChartScaleResolver.Resolve(scale, [5d, 5d]);

        // Assert
        range.Minimum.ShouldBe(5);
        range.Maximum.ShouldBe(6);
    }

    /// <summary>Verifies a collapsed observed range with only an authored maximum expands
    /// downward from that fixed maximum instead of symmetrically, matching
    /// <c>ExpandCollapsed</c>'s maximum-authored branch.</summary>
    [Fact]
    public void Resolve_WhenValuesCollapseAndOnlyMaximumIsAuthored_ExpandsDownwardFromMaximum()
    {
        // Arrange
        var scale = new ChartScale(minimum: null, maximum: 5, includeZero: false);

        // Act
        var range = ChartScaleResolver.Resolve(scale, [5d, 5d]);

        // Assert
        range.Minimum.ShouldBe(4);
        range.Maximum.ShouldBe(5);
    }

    /// <summary>Verifies a collapsed zero-valued observed range expands by exactly one unit on
    /// each side rather than a magnitude-proportional margin, since a proportional margin of zero
    /// itself is zero and would otherwise collapse right back to a single point.</summary>
    [Fact]
    public void Resolve_WhenValuesCollapseAtZero_ExpandsByOneUnitOnEachSide()
    {
        // Arrange and act
        var range = ChartScaleResolver.Resolve(
            new ChartScale(minimum: null, maximum: null, includeZero: false),
            [0d, 0d]);

        // Assert
        range.Minimum.ShouldBe(-1);
        range.Maximum.ShouldBe(1);
    }
}
