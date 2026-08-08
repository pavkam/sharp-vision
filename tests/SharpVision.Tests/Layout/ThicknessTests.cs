// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

/// <summary>Verifies immutable box-model thickness values and saturated deflation geometry.</summary>
public sealed class ThicknessTests
{
    /// <summary>Verifies uniform and axis constructors map to physical edges.</summary>
    [Fact]
    public void Constructor_WhenThicknessIsValid_PreservesEdgesAndSums()
    {
        new Thickness(2).ShouldBe(new Thickness(2, 2, 2, 2));
        new Thickness(horizontal: 2, vertical: 3)
            .ShouldBe(new Thickness(2, 3, 2, 3));
        var value = new Thickness(1, 2, 3, 4);

        value.Horizontal.ShouldBe(4);
        value.Vertical.ShouldBe(6);
    }

    /// <summary>Verifies invalid edges and overflowing sums fail during construction.</summary>
    [Fact]
    public void Constructor_WhenThicknessIsInvalid_ThrowsBeforeConstruction()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Thickness(-1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Thickness(0, -1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Thickness(0, 0, -1, 0));
        _ = Should.Throw<OverflowException>(() => new Thickness(int.MaxValue, 0, 1, 0));
    }

    /// <summary>Verifies box deflation saturates without creating negative extents.</summary>
    [Fact]
    public void Deflate_WhenThicknessExceedsGeometry_SaturatesAtZero()
    {
        var value = new Thickness(1, 2, 3, 4);

        value.Deflate(new Size(2, 3)).ShouldBe(default);
        value.Deflate(new Rect(10, 20, 2, 3)).ShouldBe(new Rect(11, 22, 0, 0));
    }
}
