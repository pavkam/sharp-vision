// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies the shared cell-space pointer drag threshold.</summary>
public sealed class PointerDragThresholdTests
{
    /// <summary>Verifies a stationary pointer remains a click candidate.</summary>
    [Fact]
    public void IsCrossed_WhenPointerDoesNotMove_ReturnsFalse()
    {
        // Arrange
        var origin = new Point(4, 7);

        // Act
        var crossed = PointerDragThreshold.IsCrossed(origin, origin);

        // Assert
        crossed.ShouldBeFalse();
    }

    /// <summary>Verifies one cell on either axis crosses the terminal drag threshold.</summary>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    [InlineData(0, -1)]
    public void IsCrossed_WhenPointerMovesOneCell_ReturnsTrue(int deltaX, int deltaY)
    {
        // Arrange
        var origin = new Point(4, 7);
        var current = new Point(origin.X + deltaX, origin.Y + deltaY);

        // Act
        var crossed = PointerDragThreshold.IsCrossed(origin, current);

        // Assert
        crossed.ShouldBeTrue();
    }

    /// <summary>Verifies extreme coordinates are compared without integer overflow.</summary>
    [Fact]
    public void IsCrossed_WhenCoordinatesSpanIntegerRange_ReturnsTrue()
    {
        // Arrange, act, and assert
        PointerDragThreshold.IsCrossed(
            new Point(int.MinValue, int.MaxValue),
            new Point(int.MaxValue, int.MinValue)).ShouldBeTrue();
    }
}
