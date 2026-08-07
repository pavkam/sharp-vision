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
        var range = ChartScaleResolver.Resolve(ChartScale.Automatic, []);

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
}
