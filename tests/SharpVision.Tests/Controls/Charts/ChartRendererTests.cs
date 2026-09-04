// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Charts;

/// <summary>Verifies shared chart rendering math used across the concrete chart renderers.</summary>
public sealed class ChartRendererTests
{
    /// <summary>Verifies a fractional bar's extent in eighths from a whole-cell zero boundary.</summary>
    [Theory]
    [InlineData(0, 10, 5, 10, 0, 40)]
    [InlineData(-10, 10, -5, 10, 5, 20)]
    [InlineData(0, 16, 1, 1, 0, 1)]
    [InlineData(0, 10, 8, 10, 3, 40)]
    public void ExtentEighths_WhenGivenRangeAndZeroBoundary_ComputesAbsoluteEighthExtent(
        double minimum,
        double maximum,
        double value,
        int extent,
        int zeroCells,
        int expected)
    {
        // Arrange
        var range = new ChartScaleRange(minimum, maximum);

        // Act
        var eighths = ChartRenderer.ExtentEighths(range, value, extent, zeroCells);

        // Assert
        eighths.ShouldBe(expected);
    }

    /// <summary>Verifies a zero-value bar always has zero extent, even when the shared zero
    /// boundary's whole-cell rounding and the value's own eighth-cell rounding of the identical
    /// zero ratio would otherwise disagree and fabricate a visible stub.</summary>
    [Fact]
    public void ExtentEighths_WhenValueIsZero_ReturnsZero()
    {
        // Arrange
        var range = new ChartScaleRange(-1, 6);
        var zeroCells = (int) Math.Round((0.0 - (-1.0)) / (6.0 - (-1.0)) * 10, MidpointRounding.AwayFromZero);

        // Act
        var eighths = ChartRenderer.ExtentEighths(range, 0, 10, zeroCells);

        // Assert
        eighths.ShouldBe(0);
    }

    /// <summary>Verifies doubled chart coordinates saturate at the drawing primitive's integer
    /// boundary instead of wrapping to the opposite side of the canvas.</summary>
    [Fact]
    public void MapHalf_WhenPlotGeometryExceedsHalfIntegerRange_SaturatesWithoutWrapping()
    {
        // Arrange
        var range = new ChartScaleRange(0, 1);
        var plot = new Rect(int.MaxValue - 10, int.MaxValue - 10, int.MaxValue, int.MaxValue);

        // Act
        var first = ChartRenderer.MapHalf(range, 0, 2, 1, plot);
        var last = ChartRenderer.MapHalf(range, 1, 2, 0, plot);

        // Assert
        first.X.ShouldBe(int.MaxValue);
        first.Y.ShouldBe(0);
        last.X.ShouldBe(int.MaxValue);
        last.Y.ShouldBe(int.MaxValue);
    }

    /// <summary>Verifies eighth-cell scaling clamps only after using a wide intermediate.</summary>
    [Fact]
    public void ScaleEighths_WhenExtentExceedsDrawingRange_SaturatesWithoutWrapping()
    {
        // Arrange
        const int extent = int.MaxValue;

        // Act
        var eighths = ChartRenderer.ScaleEighths(1, extent);

        // Assert
        eighths.ShouldBe(int.MaxValue);
    }

    /// <summary>Verifies the eighths-to-cells ceiling division widens before adding its rounding
    /// bias, so a saturated <see cref="int.MaxValue"/> eighths value - as
    /// <see cref="ChartRenderer.ExtentEighths"/> or <see cref="ChartRenderer.ScaleEighths"/>
    /// legitimately return for a very large plot extent - produces a large positive cell count
    /// instead of overflowing <c>eighths + 7</c> to a negative number.</summary>
    [Fact]
    public void CeilEighthsToCells_WhenEighthsIsMaxValue_SaturatesWithoutWrapping()
    {
        // Act
        var cells = ChartRenderer.CeilEighthsToCells(int.MaxValue);

        // Assert
        cells.ShouldBe(268435456);
        cells.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies an eighth-cell distance whose exact value exceeds the drawing contract
    /// saturates rather than overflowing during subtraction or absolute-value conversion.</summary>
    [Fact]
    public void ExtentEighths_WhenDistanceExceedsDrawingRange_SaturatesWithoutWrapping()
    {
        // Arrange
        var range = new ChartScaleRange(-1, 1);

        // Act
        var eighths = ChartRenderer.ExtentEighths(range, 1, int.MaxValue, int.MinValue);

        // Assert
        eighths.ShouldBe(int.MaxValue);
    }

    /// <summary>Verifies close values retain their exact sub-cell distance when both absolute
    /// eighth-cell positions exceed the drawing primitive's integer range.</summary>
    [Fact]
    public void ExtentEighths_WhenAbsolutePositionsExceedDrawingRange_PreservesSmallDistance()
    {
        // Arrange
        var range = new ChartScaleRange(-1, 1);
        const int extent = int.MaxValue;
        var zeroCells = (int) Math.Round(extent / 2.0, MidpointRounding.AwayFromZero);

        // Act
        var eighths = ChartRenderer.ExtentEighths(range, -0.02, extent, zeroCells);

        // Assert
        eighths.ShouldBe(171798696);
    }

    /// <summary>Verifies category slot centering retains monotonic coordinates at maximum extent.</summary>
    [Fact]
    public void CenterSlot_WhenProductsExceedIntegerRange_RemainsOrdered()
    {
        // Arrange
        const int count = int.MaxValue;

        // Act
        var first = BarChartRenderer.CenterSlot(0, count, 0, int.MaxValue);
        var last = BarChartRenderer.CenterSlot(count - 1, count, 0, int.MaxValue);

        // Assert
        first.ShouldBe(0);
        last.ShouldBe(int.MaxValue - 1);
    }

    /// <summary>Verifies category partitioning uses wide products and preserves the final band.</summary>
    [Fact]
    public void CategoryBand_WhenProductsExceedIntegerRange_PreservesFinalBand()
    {
        // Arrange
        const int count = int.MaxValue;

        // Act
        var band = BarChartRenderer.CategoryBand(count - 1, count, 0, int.MaxValue);

        // Assert
        band.Start.ShouldBe(int.MaxValue - 1);
        band.Length.ShouldBe(1);
    }
}
