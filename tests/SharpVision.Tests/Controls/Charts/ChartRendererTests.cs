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
}
