// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies the shared three-branch multi-select gesture policy used by ListView and
/// TreeView.</summary>
public sealed class SelectionGestureTests
{
    private static readonly Func<int, int, IEnumerable<int>?> _fullRange =
        static (anchor, target) =>
        {
            List<int> result = [];

            for (var value = Math.Min(anchor, target); value <= Math.Max(anchor, target); value++)
            {
                result.Add(value);
            }

            return result;
        };

    #region Default branch (no modifiers, or a mode that does not allow multiple)

    /// <summary>Verifies no modifiers held clears the selection down to the target and moves the
    /// anchor to it.</summary>
    [Fact]
    public void Resolve_WhenNoModifiersHeld_ClearsSelectionToTargetAndMovesAnchor()
    {
        // Act
        var (selection, anchor) = SelectionGesture<int>.Resolve(
            comparer: EqualityComparer<int>.Default,
            selection: new HashSet<int> { 1, 2, 3 },
            hasAnchor: true,
            anchor: 1,
            target: 5,
            modifiers: Modifiers.None,
            allowsMultiple: true,
            eligibleRange: _fullRange);

        // Assert
        selection.ShouldBe([5]);
        anchor.ShouldBe(5);
    }

    /// <summary>Verifies a single-selection mode ignores Control and Shift entirely, always taking
    /// the default branch.</summary>
    [Theory]
    [InlineData(Modifiers.Control)]
    [InlineData(Modifiers.Shift)]
    [InlineData(Modifiers.Control | Modifiers.Shift)]
    public void Resolve_WhenModeDoesNotAllowMultiple_IgnoresModifiersAndUsesDefaultBranch(Modifiers modifiers)
    {
        // Act
        var (selection, anchor) = SelectionGesture<int>.Resolve(
            comparer: EqualityComparer<int>.Default,
            selection: new HashSet<int> { 1, 2, 3 },
            hasAnchor: true,
            anchor: 1,
            target: 5,
            modifiers: modifiers,
            allowsMultiple: false,
            eligibleRange: _fullRange);

        // Assert
        selection.ShouldBe([5]);
        anchor.ShouldBe(5);
    }

    /// <summary>Verifies Shift held without a live anchor falls through to the default branch
    /// rather than attempting a range.</summary>
    [Fact]
    public void Resolve_WhenShiftHeldWithoutAnchor_FallsBackToDefaultBranch()
    {
        // Act
        var (selection, anchor) = SelectionGesture<int>.Resolve(
            comparer: EqualityComparer<int>.Default,
            selection: new HashSet<int> { 1, 2, 3 },
            hasAnchor: false,
            anchor: 0,
            target: 5,
            modifiers: Modifiers.Shift,
            allowsMultiple: true,
            eligibleRange: _fullRange);

        // Assert
        selection.ShouldBe([5]);
        anchor.ShouldBe(5);
    }

    #endregion

    #region Control toggle branch

    /// <summary>Verifies Control held on an unselected target adds it to the selection without
    /// disturbing the rest, and moves the anchor to it.</summary>
    [Fact]
    public void Resolve_WhenControlHeldOnUnselectedTarget_AddsTargetAndMovesAnchor()
    {
        // Act
        var (selection, anchor) = SelectionGesture<int>.Resolve(
            comparer: EqualityComparer<int>.Default,
            selection: new HashSet<int> { 1, 3 },
            hasAnchor: true,
            anchor: 1,
            target: 2,
            modifiers: Modifiers.Control,
            allowsMultiple: true,
            eligibleRange: _fullRange);

        // Assert
        selection.ShouldBe([1, 2, 3], ignoreOrder: true);
        anchor.ShouldBe(2);
    }

    /// <summary>Verifies Control held on an already-selected target removes just that target and
    /// still moves the anchor to it.</summary>
    [Fact]
    public void Resolve_WhenControlHeldOnSelectedTarget_RemovesTargetAndMovesAnchor()
    {
        // Act
        var (selection, anchor) = SelectionGesture<int>.Resolve(
            comparer: EqualityComparer<int>.Default,
            selection: new HashSet<int> { 1, 2, 3 },
            hasAnchor: true,
            anchor: 1,
            target: 2,
            modifiers: Modifiers.Control,
            allowsMultiple: true,
            eligibleRange: _fullRange);

        // Assert
        selection.ShouldBe([1, 3], ignoreOrder: true);
        anchor.ShouldBe(2);
    }

    #endregion

    #region Shift range branch

    /// <summary>Verifies Shift held with a live anchor clears the selection and replaces it with
    /// the eligible range between the anchor and the target, inclusive.</summary>
    [Fact]
    public void Resolve_WhenShiftHeldWithAnchor_ReplacesSelectionWithEligibleRange()
    {
        // Act
        var (selection, _) = SelectionGesture<int>.Resolve(
            comparer: EqualityComparer<int>.Default,
            selection: new HashSet<int> { 9 },
            hasAnchor: true,
            anchor: 2,
            target: 5,
            modifiers: Modifiers.Shift,
            allowsMultiple: true,
            eligibleRange: _fullRange);

        // Assert
        selection.ShouldBe([2, 3, 4, 5], ignoreOrder: true);
    }

    /// <summary>Verifies a Shift range leaves the anchor exactly where it was - only Control-toggle
    /// and the default branch move it.</summary>
    [Fact]
    public void Resolve_WhenShiftHeldWithAnchor_LeavesAnchorUnchanged()
    {
        // Act
        var (_, anchor) = SelectionGesture<int>.Resolve(
            comparer: EqualityComparer<int>.Default,
            selection: new HashSet<int>(),
            hasAnchor: true,
            anchor: 2,
            target: 5,
            modifiers: Modifiers.Shift,
            allowsMultiple: true,
            eligibleRange: _fullRange);

        // Assert
        anchor.ShouldBe(2);
    }

    /// <summary>Verifies a range request that resolves to no eligible keys still clears the prior
    /// selection down to empty - an empty range is a valid resolution, distinct from the pair being
    /// unresolvable.</summary>
    [Fact]
    public void Resolve_WhenEligibleRangeResolvesToNoKeys_ClearsSelectionToEmpty()
    {
        // Act
        var (selection, _) = SelectionGesture<int>.Resolve(
            comparer: EqualityComparer<int>.Default,
            selection: new HashSet<int> { 9 },
            hasAnchor: true,
            anchor: 2,
            target: 5,
            modifiers: Modifiers.Shift,
            allowsMultiple: true,
            eligibleRange: static (_, _) => []);

        // Assert
        selection.ShouldBeEmpty();
    }

    /// <summary>Verifies a null range - signalling the anchor/target pair could not be resolved at
    /// all, for example because one endpoint left the domain the range is evaluated over - leaves
    /// the selection completely untouched rather than clearing it.</summary>
    [Fact]
    public void Resolve_WhenEligibleRangeIsUnresolvable_LeavesSelectionUntouched()
    {
        // Arrange
        var current = new HashSet<int> { 9 };

        // Act
        var (selection, anchor) = SelectionGesture<int>.Resolve(
            comparer: EqualityComparer<int>.Default,
            selection: current,
            hasAnchor: true,
            anchor: 2,
            target: 5,
            modifiers: Modifiers.Shift,
            allowsMultiple: true,
            eligibleRange: static (_, _) => null);

        // Assert
        selection.ShouldBe([9]);
        anchor.ShouldBe(2);
    }

    #endregion

    #region Comparer and input immutability

    /// <summary>Verifies the supplied equality comparer, not the default one, decides whether the
    /// Control toggle treats the target as already selected.</summary>
    [Fact]
    public void Resolve_UsesSuppliedComparerForToggleMembership()
    {
        // Act: "a" is present under ordinal-ignore-case even though the literal string differs.
        var (selection, _) = SelectionGesture<string>.Resolve(
            comparer: StringComparer.OrdinalIgnoreCase,
            selection: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a" },
            hasAnchor: true,
            anchor: "a",
            target: "A",
            modifiers: Modifiers.Control,
            allowsMultiple: true,
            eligibleRange: static (_, _) => []);

        // Assert
        selection.ShouldBeEmpty();
    }

    /// <summary>Verifies the input selection set is never mutated in place - a fresh set is always
    /// returned.</summary>
    [Fact]
    public void Resolve_DoesNotMutateTheSuppliedSelectionSet()
    {
        // Arrange
        var current = new HashSet<int> { 1, 2 };

        // Act
        _ = SelectionGesture<int>.Resolve(
            comparer: EqualityComparer<int>.Default,
            selection: current,
            hasAnchor: true,
            anchor: 1,
            target: 9,
            modifiers: Modifiers.None,
            allowsMultiple: true,
            eligibleRange: _fullRange);

        // Assert
        current.ShouldBe([1, 2], ignoreOrder: true);
    }

    #endregion
}
