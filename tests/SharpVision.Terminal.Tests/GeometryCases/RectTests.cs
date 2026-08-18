// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.GeometryCases;

/// <summary>Verifies rectangle containment and intersection arithmetic.</summary>
public sealed class RectTests
{
    /// <summary>
    /// Verifies two non-overlapping rects near opposite ends of the int range intersect to an
    /// empty rect instead of wrapping the unchecked int subtraction into a bogus positive extent.
    /// </summary>
    [Fact]
    public void Intersect_WhenEdgesAreFarApartAndDoNotOverlap_ReturnsEmpty()
    {
        var low = new Rect(int.MinValue, int.MinValue, 10, 10);
        var high = new Rect(int.MaxValue - 10, int.MaxValue - 10, 10, 10);

        var result = low.Intersect(high);

        result.Width.ShouldBe(0);
        result.Height.ShouldBe(0);
    }

    /// <summary>Verifies an ordinary, non-overlapping pair still intersects to an empty rect.</summary>
    [Fact]
    public void Intersect_WhenRectsDoNotOverlap_ReturnsEmpty()
    {
        var first = new Rect(0, 0, 5, 5);
        var second = new Rect(10, 10, 5, 5);

        var result = first.Intersect(second);

        result.Width.ShouldBe(0);
        result.Height.ShouldBe(0);
    }

    /// <summary>Verifies overlapping rects intersect to the shared region.</summary>
    [Fact]
    public void Intersect_WhenRectsOverlap_ReturnsSharedRegion()
    {
        var first = new Rect(0, 0, 10, 10);
        var second = new Rect(5, 5, 10, 10);

        var result = first.Intersect(second);

        result.ShouldBe(new Rect(5, 5, 5, 5));
    }

    /// <summary>
    /// Verifies intersecting a rect whose right edge saturates against itself is the identity:
    /// the shared region must equal the original rect exactly. Combining two already-saturated
    /// <see cref="Rect.Right"/> values with a second int-range subtraction throws away up to
    /// <see cref="Rect.X"/> units of otherwise-representable width, so a naive implementation
    /// under-reports the intersection even though no true overflow of the final result occurs.
    /// </summary>
    [Fact]
    public void Intersect_WhenRectSaturatesAndIntersectsItself_ReturnsSameRect()
    {
        var rect = new Rect(5, 0, int.MaxValue, 1);

        var result = rect.Intersect(rect);

        result.ShouldBe(rect);
    }

    /// <summary>
    /// Verifies a point at the representable coordinate maximum is still contained when the
    /// rect's true (unrepresentable) right edge lies beyond it, instead of being excluded by the
    /// saturated exclusive bound.
    /// </summary>
    [Fact]
    public void Contains_WhenTrueRightEdgeOverflowsPastMaxValue_ContainsMaxValueCoordinate()
    {
        var rect = new Rect(5, 0, int.MaxValue, 1);

        rect.Contains(new Point(int.MaxValue, 0)).ShouldBeTrue();
    }
}
