// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies the shared page-step accumulation loop used by ListView, Table, TreeView,
/// and NavigationView.</summary>
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
}
