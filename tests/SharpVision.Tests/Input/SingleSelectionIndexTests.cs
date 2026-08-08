// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies the shared single-selection cursor scans used by TabControl and Menu.</summary>
public sealed class SingleSelectionIndexTests
{
    private static readonly Func<int, bool> _none = static _ => false;
    private static readonly Func<int, bool> _all = static _ => true;

    #region FindLinear

    /// <summary>Verifies an empty collection returns -1 without evaluating the predicate against
    /// any index.</summary>
    [Fact]
    public void FindLinear_WhenCountIsZero_ReturnsNegativeOne()
    {
        // Act
        var result = SingleSelectionIndex.FindLinear(0, 1, 0, _all);

        // Assert
        result.ShouldBe(-1);
    }

    /// <summary>Verifies a forward scan stops at the single eligible index it encounters.</summary>
    [Fact]
    public void FindLinear_WhenSingleIndexIsEligible_ReturnsThatIndex()
    {
        // Arrange
        static bool isEligible(int index) => index == 2;

        // Act
        var result = SingleSelectionIndex.FindLinear(0, 1, 5, isEligible);

        // Assert
        result.ShouldBe(2);
    }

    /// <summary>Verifies a scan that starts exactly on the one ineligible index still finds a
    /// later eligible one.</summary>
    [Fact]
    public void FindLinear_WhenStartIndexIsIneligible_ContinuesPastIt()
    {
        // Arrange
        static bool isEligible(int index) => index != 0;

        // Act
        var result = SingleSelectionIndex.FindLinear(0, 1, 3, isEligible);

        // Assert
        result.ShouldBe(1);
    }

    /// <summary>Verifies a scan over an entirely ineligible collection runs off the end and
    /// returns -1.</summary>
    [Fact]
    public void FindLinear_WhenAllIndexesAreIneligible_ReturnsNegativeOne()
    {
        // Act
        var result = SingleSelectionIndex.FindLinear(0, 1, 4, _none);

        // Assert
        result.ShouldBe(-1);
    }

    /// <summary>Verifies a negative start index runs the loop condition false immediately and
    /// returns -1 rather than throwing.</summary>
    [Fact]
    public void FindLinear_WhenStartIsNegative_ReturnsNegativeOne()
    {
        // Act
        var result = SingleSelectionIndex.FindLinear(-1, 1, 4, _all);

        // Assert
        result.ShouldBe(-1);
    }

    /// <summary>Verifies a start index at or past the collection's end returns -1 rather than
    /// throwing, for a forward scan.</summary>
    [Fact]
    public void FindLinear_WhenStartIsAtOrPastCount_ReturnsNegativeOne()
    {
        // Act
        var result = SingleSelectionIndex.FindLinear(4, 1, 4, _all);

        // Assert
        result.ShouldBe(-1);
    }

    /// <summary>Verifies a forward scan visits indexes in ascending order and stops at the first
    /// eligible one.</summary>
    [Fact]
    public void FindLinear_WhenScanningForward_StopsAtFirstEligibleIndex()
    {
        // Arrange
        static bool isEligible(int index) => index >= 3;

        // Act
        var result = SingleSelectionIndex.FindLinear(0, 1, 6, isEligible);

        // Assert
        result.ShouldBe(3);
    }

    /// <summary>Verifies a backward scan visits indexes in descending order and stops at the
    /// first eligible one.</summary>
    [Fact]
    public void FindLinear_WhenScanningBackward_StopsAtFirstEligibleIndex()
    {
        // Arrange
        static bool isEligible(int index) => index <= 2;

        // Act
        var result = SingleSelectionIndex.FindLinear(5, -1, 6, isEligible);

        // Assert
        result.ShouldBe(2);
    }

    #endregion

    #region FindNearest

    /// <summary>Verifies an empty collection returns -1 rather than throwing.</summary>
    [Fact]
    public void FindNearest_WhenCountIsZero_ReturnsNegativeOne()
    {
        // Act
        var result = SingleSelectionIndex.FindNearest(0, 0, _all);

        // Assert
        result.ShouldBe(-1);
    }

    /// <summary>Verifies the inclusive forward scan finds a successor at or after start before
    /// ever considering a predecessor.</summary>
    [Fact]
    public void FindNearest_WhenSuccessorExistsFromStart_ReturnsSuccessor()
    {
        // Arrange
        static bool isEligible(int index) => index == 2;

        // Act
        var result = SingleSelectionIndex.FindNearest(0, 5, isEligible);

        // Assert
        result.ShouldBe(2);
    }

    /// <summary>Verifies that when the forward scan from the last index finds nothing, the walk
    /// falls back to the nearest eligible predecessor.</summary>
    [Fact]
    public void FindNearest_WhenStartIsLastIndexAndNoSuccessorExists_FallsBackToPredecessor()
    {
        // Arrange
        static bool isEligible(int index) => index == 1;

        // Act
        var result = SingleSelectionIndex.FindNearest(3, 4, isEligible);

        // Assert
        result.ShouldBe(1);
    }

    /// <summary>Verifies an entirely ineligible collection returns -1 after both the forward and
    /// backward scans are exhausted.</summary>
    [Fact]
    public void FindNearest_WhenAllIndexesAreIneligible_ReturnsNegativeOne()
    {
        // Act
        var result = SingleSelectionIndex.FindNearest(2, 5, _none);

        // Assert
        result.ShouldBe(-1);
    }

    /// <summary>Verifies a single-item eligible collection resolves to that one index regardless
    /// of the requested start.</summary>
    [Fact]
    public void FindNearest_WhenCollectionHasSingleEligibleItem_ReturnsThatIndex()
    {
        // Act
        var result = SingleSelectionIndex.FindNearest(0, 1, _all);

        // Assert
        result.ShouldBe(0);
    }

    /// <summary>Verifies a single-item collection whose only item is ineligible returns -1.</summary>
    [Fact]
    public void FindNearest_WhenCollectionHasSingleIneligibleItem_ReturnsNegativeOne()
    {
        // Act
        var result = SingleSelectionIndex.FindNearest(0, 1, _none);

        // Assert
        result.ShouldBe(-1);
    }

    #endregion

    #region FindWrapped

    /// <summary>Verifies an empty collection returns -1 rather than dividing by zero in the
    /// modulo wrap.</summary>
    [Fact]
    public void FindWrapped_WhenCountIsZero_ReturnsNegativeOne()
    {
        // Act
        var result = SingleSelectionIndex.FindWrapped(-1, 1, 0, _all);

        // Assert
        result.ShouldBe(-1);
    }

    /// <summary>Verifies a cleared selection (start of -1) stepping forward scans from index 0.</summary>
    [Fact]
    public void FindWrapped_WhenStartIsNegativeOneAndDirectionIsForward_ScansFromIndexZero()
    {
        // Arrange
        static bool isEligible(int index) => index == 0;

        // Act
        var result = SingleSelectionIndex.FindWrapped(-1, 1, 4, isEligible);

        // Assert
        result.ShouldBe(0);
    }

    /// <summary>Verifies a cleared selection (start of -1) stepping backward scans from the last
    /// index, not one short of it.</summary>
    [Fact]
    public void FindWrapped_WhenStartIsNegativeOneAndDirectionIsBackward_ScansFromLastIndex()
    {
        // Arrange
        static bool isEligible(int index) => index == 3;

        // Act
        var result = SingleSelectionIndex.FindWrapped(-1, -1, 4, isEligible);

        // Assert
        result.ShouldBe(3);
    }

    /// <summary>Verifies a single-item collection wraps to itself in either direction.</summary>
    [Fact]
    public void FindWrapped_WhenCollectionHasSingleItem_WrapsToItself()
    {
        // Act
        var forward = SingleSelectionIndex.FindWrapped(0, 1, 1, _all);
        var backward = SingleSelectionIndex.FindWrapped(0, -1, 1, _all);

        // Assert
        forward.ShouldBe(0);
        backward.ShouldBe(0);
    }

    /// <summary>Verifies a full cycle through an entirely ineligible collection returns -1 rather
    /// than looping forever.</summary>
    [Fact]
    public void FindWrapped_WhenAllIndexesAreIneligible_ReturnsNegativeOneAfterFullCycle()
    {
        // Act
        var result = SingleSelectionIndex.FindWrapped(1, 1, 5, _none);

        // Assert
        result.ShouldBe(-1);
    }

    /// <summary>Verifies a forward wrap that runs past the last index continues from index 0,
    /// crossing the boundary mid-collection.</summary>
    [Fact]
    public void FindWrapped_WhenForwardScanRunsPastLastIndex_WrapsToStart()
    {
        // Arrange
        static bool isEligible(int index) => index == 1;

        // Act: starting at index 3 of 4, only index 1 is eligible, requiring the scan to cross
        // the end-of-collection boundary and continue from index 0.
        var result = SingleSelectionIndex.FindWrapped(3, 1, 4, isEligible);

        // Assert
        result.ShouldBe(1);
    }

    /// <summary>Verifies a backward wrap that runs past the first index continues from the last
    /// index, crossing the boundary mid-collection.</summary>
    [Fact]
    public void FindWrapped_WhenBackwardScanRunsPastFirstIndex_WrapsToEnd()
    {
        // Arrange
        static bool isEligible(int index) => index == 3;

        // Act: starting at index 0 of 4, only index 3 is eligible, requiring the scan to cross
        // the start-of-collection boundary and continue from the last index.
        var result = SingleSelectionIndex.FindWrapped(0, -1, 4, isEligible);

        // Assert
        result.ShouldBe(3);
    }

    #endregion
}
