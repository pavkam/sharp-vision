// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies the shared page-step accumulation loop and target-extent computation used by
/// ListView, Table, TreeView, NavigationView, and JsonView.</summary>
public sealed class PagingStepTests
{
    /// <summary>Verifies items are visited forward and the walk stops once the accumulated extent
    /// first reaches the target.</summary>
    [Fact]
    public void Accumulate_WhenSteppingForward_StopsOnceTargetIsReached()
    {
        // Arrange
        int[] heights = [3, 3, 3, 3, 3];

        // Act
        var result = PagingStep.Accumulate(0, 1, heights.Length, target: 7, index => heights[index], clamp: true);

        // Assert: 0 -> 1 (accumulated 3) -> 2 (accumulated 6) -> 3 (accumulated 9 >= 7)
        result.ShouldBe(3);
    }

    /// <summary>Verifies items are visited backward symmetrically to the forward case.</summary>
    [Fact]
    public void Accumulate_WhenSteppingBackward_StopsOnceTargetIsReached()
    {
        // Arrange
        int[] heights = [3, 3, 3, 3, 3];

        // Act
        var result = PagingStep.Accumulate(4, -1, heights.Length, target: 7, index => heights[index], clamp: true);

        // Assert: 4 -> 3 (accumulated 3) -> 2 (accumulated 6) -> 1 (accumulated 9 >= 7)
        result.ShouldBe(1);
    }

    /// <summary>Verifies the loop always advances at least once, even when the starting item's own
    /// extent alone would already satisfy the target.</summary>
    [Fact]
    public void Accumulate_WhenTargetIsAlreadyMet_StillAdvancesOneStep()
    {
        // Arrange
        int[] heights = [100, 1, 1, 1];

        // Act
        var result = PagingStep.Accumulate(0, 1, heights.Length, target: 1, index => heights[index], clamp: true);

        // Assert: the starting item's extent (100) is never counted; the walk must move to index 1
        // first, whose extent (1) meets the target of 1.
        result.ShouldBe(1);
    }

    /// <summary>Verifies a walk that lands exactly on the target stops there rather than overshooting.</summary>
    [Fact]
    public void Accumulate_WhenAccumulatedExtentLandsExactlyOnTarget_StopsAtThatIndex()
    {
        // Arrange
        int[] heights = [4, 4, 4, 4];

        // Act
        var result = PagingStep.Accumulate(0, 1, heights.Length, target: 8, index => heights[index], clamp: true);

        // Assert: 0 -> 1 (accumulated 4) -> 2 (accumulated 8 == target)
        result.ShouldBe(2);
    }

    /// <summary>Verifies a forward walk that runs off the end returns the raw, out-of-range index
    /// when clamping is disabled.</summary>
    [Fact]
    public void Accumulate_WhenRunningPastEndWithoutClamp_ReturnsRawIndex()
    {
        // Arrange
        int[] heights = [1, 1];

        // Act
        var result = PagingStep.Accumulate(0, 1, heights.Length, target: 100, index => heights[index], clamp: false);

        // Assert
        result.ShouldBe(heights.Length);
    }

    /// <summary>Verifies a backward walk that runs off the start returns the raw, negative index
    /// when clamping is disabled.</summary>
    [Fact]
    public void Accumulate_WhenRunningPastStartWithoutClamp_ReturnsRawIndex()
    {
        // Arrange
        int[] heights = [1, 1];

        // Act
        var result = PagingStep.Accumulate(1, -1, heights.Length, target: 100, index => heights[index], clamp: false);

        // Assert
        result.ShouldBe(-1);
    }

    /// <summary>Verifies a forward walk that runs off the end is clamped to the last valid index.</summary>
    [Fact]
    public void Accumulate_WhenRunningPastEndWithClamp_ReturnsLastIndex()
    {
        // Arrange
        int[] heights = [1, 1, 1];

        // Act
        var result = PagingStep.Accumulate(0, 1, heights.Length, target: 100, index => heights[index], clamp: true);

        // Assert
        result.ShouldBe(heights.Length - 1);
    }

    /// <summary>Verifies a backward walk that runs off the start is clamped to zero.</summary>
    [Fact]
    public void Accumulate_WhenRunningPastStartWithClamp_ReturnsZero()
    {
        // Arrange
        int[] heights = [1, 1, 1];

        // Act
        var result = PagingStep.Accumulate(2, -1, heights.Length, target: 100, index => heights[index], clamp: true);

        // Assert
        result.ShouldBe(0);
    }

    /// <summary>Verifies zero and negative extents contribute nothing to the accumulated total,
    /// matching each call site's own <c>Math.Max(0, ·)</c> guard folded into the shared loop.</summary>
    [Fact]
    public void Accumulate_WhenExtentsAreZeroOrNegative_TreatsThemAsZero()
    {
        // Arrange
        int[] heights = [-5, 0, -1, 6];

        // Act
        var result = PagingStep.Accumulate(0, 1, heights.Length, target: 6, index => heights[index], clamp: true);

        // Assert: indexes 1..3 contribute 0 + 0 + 6 = 6, first meeting the target at index 3.
        result.ShouldBe(3);
    }

    /// <summary>Verifies the ordinary case subtracts the retained overlap from the viewport extent.</summary>
    [Fact]
    public void TargetExtent_WhenOverlapIsSmallerThanExtent_SubtractsOverlapFromExtent()
    {
        // Act
        var result = PagingStep.TargetExtent(viewportExtent: 10, pageOverlap: 3);

        // Assert
        result.ShouldBe(7);
    }

    /// <summary>Verifies a zero overlap leaves the full viewport extent as the target.</summary>
    [Fact]
    public void TargetExtent_WhenOverlapIsZero_ReturnsFullExtent()
    {
        // Act
        var result = PagingStep.TargetExtent(viewportExtent: 10, pageOverlap: 0);

        // Assert
        result.ShouldBe(10);
    }

    /// <summary>Verifies an overlap equal to the viewport extent would zero out the target, but the
    /// floor of one keeps a page step always advancing.</summary>
    [Fact]
    public void TargetExtent_WhenOverlapEqualsExtent_FloorsToOne()
    {
        // Act
        var result = PagingStep.TargetExtent(viewportExtent: 5, pageOverlap: 5);

        // Assert
        result.ShouldBe(1);
    }

    /// <summary>Verifies an overlap larger than the viewport extent is clamped to the extent rather
    /// than driving the target negative, landing on the same floor of one as the equal case.</summary>
    [Fact]
    public void TargetExtent_WhenOverlapExceedsExtent_ClampsOverlapAndFloorsToOne()
    {
        // Act
        var result = PagingStep.TargetExtent(viewportExtent: 5, pageOverlap: 10);

        // Assert
        result.ShouldBe(1);
    }

    /// <summary>Verifies a zero-height viewport still floors to a target of one, matching the
    /// six controls' load-bearing "always advance by at least one" contract for <see cref="PagingStep.Accumulate"/>.</summary>
    [Fact]
    public void TargetExtent_WhenViewportExtentIsZero_FloorsToOne()
    {
        // Act
        var result = PagingStep.TargetExtent(viewportExtent: 0, pageOverlap: 0);

        // Assert
        result.ShouldBe(1);
    }

    /// <summary>Verifies an index already fully inside the viewport leaves the current offset untouched.</summary>
    [Fact]
    public void IndexIntoViewOffset_WhenIndexIsAlreadyFullyVisible_ReturnsCurrentOffsetUnchanged()
    {
        // Act: index 2 occupies [20, 30), fully within the visible [10, 40) window.
        var result = PagingStep.IndexIntoViewOffset(index: 2, itemExtent: 10, currentOffset: 10, viewportExtent: 30, contentExtent: 100);

        // Assert
        result.ShouldBe(10);
    }

    /// <summary>Verifies an index above the current viewport scrolls up just far enough to reveal
    /// its row slot at the top of the viewport.</summary>
    [Fact]
    public void IndexIntoViewOffset_WhenIndexIsAboveTheViewport_ScrollsUpToIndexTimesItemExtent()
    {
        // Act: index 1 starts at offset 10, above the current viewport [50, 80).
        var result = PagingStep.IndexIntoViewOffset(index: 1, itemExtent: 10, currentOffset: 50, viewportExtent: 30, contentExtent: 200);

        // Assert
        result.ShouldBe(10);
    }

    /// <summary>Verifies an index below the current viewport scrolls down just far enough to reveal
    /// its row slot at the bottom of the viewport.</summary>
    [Fact]
    public void IndexIntoViewOffset_WhenIndexIsBelowTheViewport_ScrollsDownToRevealIt()
    {
        // Act: index 9 occupies [90, 100), below the current viewport [0, 30).
        var result = PagingStep.IndexIntoViewOffset(index: 9, itemExtent: 10, currentOffset: 0, viewportExtent: 30, contentExtent: 200);

        // Assert: the minimal offset that puts [90, 100) at the bottom of a 30-cell viewport.
        result.ShouldBe(70);
    }

    /// <summary>Verifies the result is clamped to zero when the raw computed target would otherwise
    /// be negative.</summary>
    [Fact]
    public void IndexIntoViewOffset_WhenTargetWouldBeNegative_ClampsToZero()
    {
        // Act: a negative index's row slot starts before zero, and it lies above the current
        // viewport, so the raw (pre-clamp) target is -10.
        var result = PagingStep.IndexIntoViewOffset(index: -1, itemExtent: 10, currentOffset: 5, viewportExtent: 30, contentExtent: 200);

        // Assert
        result.ShouldBe(0);
    }

    /// <summary>Verifies the result is clamped to the maximum scrollable offset when the item's row
    /// slot lies beyond what the content extent can scroll to.</summary>
    [Fact]
    public void IndexIntoViewOffset_WhenTargetExceedsMaximumOffset_ClampsToContentExtentMinusViewport()
    {
        // Act: index 9's slot [90, 100) would want offset 70, but the content is only 95 cells
        // tall, so the maximum scrollable offset is 95 - 30 = 65.
        var result = PagingStep.IndexIntoViewOffset(index: 9, itemExtent: 10, currentOffset: 0, viewportExtent: 30, contentExtent: 95);

        // Assert
        result.ShouldBe(65);
    }

    /// <summary>Verifies an index/item-extent product that would overflow raw <see cref="int"/>
    /// multiplication is handled via saturating arithmetic instead of wrapping into a corrupted,
    /// negative result.</summary>
    [Fact]
    public void IndexIntoViewOffset_WhenIndexTimesItemExtentOverflowsInt_SaturatesInsteadOfWrapping()
    {
        // Act: 30_000_000 * 100 overflows a raw 32-bit multiplication (and would wrap negative),
        // but the saturating arithmetic clamps it to int.MaxValue before the offset is clamped
        // down to the maximum scrollable position.
        var result = PagingStep.IndexIntoViewOffset(
            index: 30_000_000, itemExtent: 100, currentOffset: 0, viewportExtent: 30, contentExtent: 1000);

        // Assert: still clamped into the valid, non-negative offset range.
        result.ShouldBe(970);
    }
}
