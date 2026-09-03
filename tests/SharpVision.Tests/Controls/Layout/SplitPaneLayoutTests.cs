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

    /// <summary>Verifies a trailing maximum transfers otherwise unused finite cells to the leading pane.</summary>
    [Fact]
    public void Resolve_WhenTrailingMaximumLeavesUnusedPool_ExpandsLeadingIntoFeasibleRange()
    {
        // Arrange
        Span<int> extents = stackalloc int[2];
        Span<int> margins = stackalloc int[2];

        // Act
        SplitPaneLayout.Resolve(
            Length.Percent(10),
            firstAutomatic: 0,
            secondAutomatic: 0,
            firstMinimum: 0,
            firstMaximum: 20,
            secondMinimum: 0,
            secondMaximum: 6,
            firstMargin: 0,
            secondMargin: 0,
            available: 20,
            percentBase: 20,
            extents,
            margins,
            out var minimum,
            out var maximum);

        // Assert
        extents.ToArray().ShouldBe([14, 6]);
        minimum.ShouldBe(14);
        maximum.ShouldBe(20);
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

    /// <summary>Verifies varied finite inputs remain deterministic, contained, and inside the reported range.</summary>
    [Fact]
    public void Resolve_WhenFixedSeedInputsVary_ContainsTracksAndReportsStableFeasibleRange()
    {
        // Arrange
        const int seed = 0x51_17_50;
        var random = new Random(seed);
        Span<int> extents = stackalloc int[2];
        Span<int> margins = stackalloc int[2];
        Span<int> repeatedExtents = stackalloc int[2];
        Span<int> repeatedMargins = stackalloc int[2];

        // Act and assert
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            var available = random.Next(0, 41);
            var firstMargin = random.Next(0, 61);
            var secondMargin = random.Next(0, 61);
            var firstMinimum = random.Next(0, 21);
            var secondMinimum = random.Next(0, 21);
            var firstMaximum = firstMinimum + random.Next(0, 31);
            var secondMaximum = secondMinimum + random.Next(0, 31);
            var firstAutomatic = random.Next(0, 31);
            var secondAutomatic = random.Next(0, 31);
            var firstLength = random.Next(0, 2) == 0
                ? Length.Cells(random.Next(0, 61))
                : Length.Percent(random.NextDouble() * 100d);

            SplitPaneLayout.Resolve(
                firstLength,
                firstAutomatic,
                secondAutomatic,
                firstMinimum,
                firstMaximum,
                secondMinimum,
                secondMaximum,
                firstMargin,
                secondMargin,
                available,
                available,
                extents,
                margins,
                out var minimum,
                out var maximum);
            SplitPaneLayout.Resolve(
                firstLength,
                firstAutomatic,
                secondAutomatic,
                firstMinimum,
                firstMaximum,
                secondMinimum,
                secondMaximum,
                firstMargin,
                secondMargin,
                available,
                available,
                repeatedExtents,
                repeatedMargins,
                out var repeatedMinimum,
                out var repeatedMaximum);
            var context = FormattableString.Invariant(
                $"seed={seed}, iteration={iteration}, available={available}, firstLength={firstLength}, automatic=[{firstAutomatic},{secondAutomatic}], minimum=[{firstMinimum},{secondMinimum}], maximum=[{firstMaximum},{secondMaximum}], margin=[{firstMargin},{secondMargin}], extent=[{extents[0]},{extents[1]}], range=[{minimum},{maximum}]");

            margins[0].ShouldBe(Math.Min(firstMargin, available), context);
            margins[1].ShouldBe(Math.Min(secondMargin, available - margins[0]), context);
            extents[0].ShouldBeGreaterThanOrEqualTo(0, context);
            extents[1].ShouldBeGreaterThanOrEqualTo(0, context);
            extents[0].Add(extents[1]).Add(margins[0]).Add(margins[1])
                .ShouldBeLessThanOrEqualTo(available, context);
            minimum.ShouldBeInRange(0, available - margins[0] - margins[1], context);
            maximum.ShouldBeInRange(minimum, available - margins[0] - margins[1], context);
            extents[0].ShouldBeInRange(minimum, maximum, context);
            repeatedExtents.ToArray().ShouldBe(extents.ToArray(), context);
            repeatedMargins.ToArray().ShouldBe(margins.ToArray(), context);
            repeatedMinimum.ShouldBe(minimum, context);
            repeatedMaximum.ShouldBe(maximum, context);
        }
    }
}
