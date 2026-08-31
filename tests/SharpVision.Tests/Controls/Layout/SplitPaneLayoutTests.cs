// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies SplitPane's pure two-track allocation and feasible divider range.</summary>
public sealed class SplitPaneLayoutTests
{
    /// <summary>Verifies percentage requests use the complete divider-excluded pool before margins.</summary>
    [Fact]
    public void Resolve_WhenPercentageAndMarginsShareFinitePool_UsesOuterPoolAsPercentageBase()
    {
        // Arrange
        Span<int> extents = stackalloc int[2];
        Span<int> margins = stackalloc int[2];

        // Act
        SplitPaneLayout.Resolve(
            Length.Percent(50),
            firstAutomatic: 1,
            secondAutomatic: 1,
            firstMinimum: 0,
            firstMaximum: int.MaxValue,
            secondMinimum: 0,
            secondMaximum: int.MaxValue,
            firstMargin: 2,
            secondMargin: 1,
            available: 10,
            percentBase: 10,
            extents,
            margins,
            out var minimum,
            out var maximum);

        // Assert
        extents.ToArray().ShouldBe([5, 2]);
        margins.ToArray().ShouldBe([2, 1]);
        minimum.ShouldBe(0);
        maximum.ShouldBe(7);
    }

    /// <summary>Verifies authored cell lengths describe the leading border box rather than its margin box.</summary>
    [Fact]
    public void Resolve_WhenCellLengthHasMargins_KeepsBorderBoxRequestIndependent()
    {
        // Arrange
        Span<int> extents = stackalloc int[2];
        Span<int> margins = stackalloc int[2];

        // Act
        SplitPaneLayout.Resolve(
            Length.Cells(5),
            firstAutomatic: 1,
            secondAutomatic: 1,
            firstMinimum: 0,
            firstMaximum: int.MaxValue,
            secondMinimum: 0,
            secondMaximum: int.MaxValue,
            firstMargin: 2,
            secondMargin: 1,
            available: 10,
            percentBase: 10,
            extents,
            margins,
            out _,
            out _);

        // Assert
        extents.ToArray().ShouldBe([5, 2]);
        margins.ToArray().ShouldBe([2, 1]);
    }

    /// <summary>Verifies finite margins consume the pool in source order without producing negative tracks.</summary>
    [Fact]
    public void Resolve_WhenMarginsOversubscribeFinitePool_TruncatesThemInSourceOrder()
    {
        // Arrange
        Span<int> extents = stackalloc int[2];
        Span<int> margins = stackalloc int[2];

        // Act
        SplitPaneLayout.Resolve(
            Length.Percent(50),
            firstAutomatic: 2,
            secondAutomatic: 2,
            firstMinimum: 0,
            firstMaximum: int.MaxValue,
            secondMinimum: 0,
            secondMaximum: int.MaxValue,
            firstMargin: 4,
            secondMargin: 4,
            available: 3,
            percentBase: 3,
            extents,
            margins,
            out var minimum,
            out var maximum);

        // Assert
        extents.ToArray().ShouldBe([0, 0]);
        margins.ToArray().ShouldBe([3, 0]);
        minimum.ShouldBe(0);
        maximum.ShouldBe(0);
    }

    /// <summary>Verifies both panes' border-box limits constrain the feasible leading extent.</summary>
    [Fact]
    public void Resolve_WhenBothPanesHaveLimits_ReportsJointFeasibleRange()
    {
        // Arrange
        Span<int> extents = stackalloc int[2];
        Span<int> margins = stackalloc int[2];

        // Act
        SplitPaneLayout.Resolve(
            Length.Cells(8),
            firstAutomatic: 2,
            secondAutomatic: 2,
            firstMinimum: 2,
            firstMaximum: 7,
            secondMinimum: 3,
            secondMaximum: 6,
            firstMargin: 0,
            secondMargin: 0,
            available: 10,
            percentBase: 10,
            extents,
            margins,
            out var minimum,
            out var maximum);

        // Assert
        extents.ToArray().ShouldBe([7, 3]);
        minimum.ShouldBe(4);
        maximum.ShouldBe(7);
    }

    /// <summary>Verifies contradictory pane minima collapse interaction to the contained allocation.</summary>
    [Fact]
    public void Resolve_WhenJointLimitsAreInfeasible_CollapsesRangeToAllocatedLeadingExtent()
    {
        // Arrange
        Span<int> extents = stackalloc int[2];
        Span<int> margins = stackalloc int[2];

        // Act
        SplitPaneLayout.Resolve(
            Length.Cells(4),
            firstAutomatic: 1,
            secondAutomatic: 1,
            firstMinimum: 4,
            firstMaximum: 10,
            secondMinimum: 4,
            secondMaximum: 10,
            firstMargin: 0,
            secondMargin: 0,
            available: 5,
            percentBase: 5,
            extents,
            margins,
            out var minimum,
            out var maximum);

        // Assert
        (extents[0] + extents[1]).ShouldBe(5);
        minimum.ShouldBe(extents[0]);
        maximum.ShouldBe(extents[0]);
    }

    /// <summary>Verifies unbounded percentages use intrinsic size unless a viewport base is supplied.</summary>
    [Theory]
    [InlineData(null, 3)]
    [InlineData(5, 5)]
    public void Resolve_WhenAvailableIsUnbounded_UsesIntrinsicOrExplicitPercentageBase(
        int? percentBase,
        int expectedFirst)
    {
        // Arrange
        Span<int> extents = stackalloc int[2];
        Span<int> margins = stackalloc int[2];

        // Act
        SplitPaneLayout.Resolve(
            Length.Percent(100),
            firstAutomatic: 3,
            secondAutomatic: 4,
            firstMinimum: 0,
            firstMaximum: int.MaxValue,
            secondMinimum: 0,
            secondMaximum: int.MaxValue,
            firstMargin: 2,
            secondMargin: 1,
            available: null,
            percentBase,
            extents,
            margins,
            out var minimum,
            out var maximum);

        // Assert
        extents.ToArray().ShouldBe([expectedFirst, 4]);
        margins.ToArray().ShouldBe([2, 1]);
        minimum.ShouldBe(expectedFirst);
        maximum.ShouldBe(expectedFirst);
    }

    /// <summary>Verifies repeated two-track resolution uses only caller-owned stack storage.</summary>
    [Fact]
    public void Resolve_WhenRepeated_DoesNotAllocateManagedMemory()
    {
        // Arrange
        Span<int> extents = stackalloc int[2];
        Span<int> margins = stackalloc int[2];
        var length = Length.Percent(50);
        SplitPaneLayout.Resolve(
            length,
            2,
            3,
            0,
            int.MaxValue,
            0,
            int.MaxValue,
            1,
            1,
            20,
            20,
            extents,
            margins,
            out _,
            out _);

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 1_000; index++)
        {
            SplitPaneLayout.Resolve(
                length,
                2,
                3,
                0,
                int.MaxValue,
                0,
                int.MaxValue,
                1,
                1,
                20,
                20,
                extents,
                margins,
                out _,
                out _);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        allocated.ShouldBe(0);
    }
}
