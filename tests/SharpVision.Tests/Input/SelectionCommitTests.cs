// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

using System.ComponentModel;

/// <summary>Verifies the shared cancellable diff-and-commit logic used by ListView and TreeView to
/// apply a proposed selection.</summary>
public sealed class SelectionCommitTests
{
    private sealed class ChangingArgs(int[] added, int[] removed): CancelEventArgs
    {
        public int[] Added { get; } = added;

        public int[] Removed { get; } = removed;
    }

    private static readonly Func<int[], int[], ChangingArgs> _createChangingArgs =
        static (added, removed) => new ChangingArgs(added, removed);

    private static (int[] Added, int[] Removed) DefaultDelta(IReadOnlySet<int> current, IReadOnlySet<int> selectable) =>
        ([.. selectable.Except(current).Order()], [.. current.Except(selectable).Order()]);

    #region No-op short circuit

    /// <summary>Verifies a proposal that is already equal to the current selection is rejected
    /// without raising the changing notification or touching the version counter.</summary>
    [Fact]
    public void TryCommit_WhenProposedEqualsCurrentSelection_ReturnsFalseWithoutRaisingChanging()
    {
        // Arrange
        var current = new HashSet<int> { 1, 2 };
        var raised = false;
        var version = 0;

        // Act
        var committed = SelectionCommit<int>.TryCommit(
            current: current,
            proposed: [1, 2],
            comparer: EqualityComparer<int>.Default,
            isEligible: null,
            selectionDisabled: false,
            computeDelta: DefaultDelta,
            cancellable: true,
            selectionVersion: ref version,
            createChangingArgs: _createChangingArgs,
            raiseChanging: _ => raised = true,
            selection: out _,
            added: out _,
            removed: out _);

        // Assert
        committed.ShouldBeFalse();
        raised.ShouldBeFalse();
        version.ShouldBe(0);
    }

    /// <summary>Verifies the equality check that short-circuits a no-op is performed after
    /// availability filtering, not against the raw proposal.</summary>
    [Fact]
    public void TryCommit_WhenFilteringMakesProposalEqualCurrent_ReturnsFalse()
    {
        // Arrange
        var current = new HashSet<int> { 1 };
        var version = 0;

        // Act: 2 is proposed but never eligible, so the filtered proposal is just {1}, matching current.
        var committed = SelectionCommit<int>.TryCommit(
            current: current,
            proposed: [1, 2],
            comparer: EqualityComparer<int>.Default,
            isEligible: static key => key == 1,
            selectionDisabled: false,
            computeDelta: DefaultDelta,
            cancellable: true,
            selectionVersion: ref version,
            createChangingArgs: _createChangingArgs,
            raiseChanging: static _ => { },
            selection: out var selection,
            added: out _,
            removed: out _);

        // Assert
        committed.ShouldBeFalse();
        selection.ShouldBe([1]);
    }

    #endregion

    #region Committing

    /// <summary>Verifies a non-cancellable commit applies the filtered selection and increments the
    /// version counter without raising the changing notification at all.</summary>
    [Fact]
    public void TryCommit_WhenNotCancellable_CommitsWithoutRaisingChanging()
    {
        // Arrange
        var current = new HashSet<int> { 1 };
        var raised = false;
        var version = 3;

        // Act
        var committed = SelectionCommit<int>.TryCommit(
            current: current,
            proposed: [2, 3],
            comparer: EqualityComparer<int>.Default,
            isEligible: null,
            selectionDisabled: false,
            computeDelta: DefaultDelta,
            cancellable: false,
            selectionVersion: ref version,
            createChangingArgs: _createChangingArgs,
            raiseChanging: _ => raised = true,
            selection: out var selection,
            added: out var added,
            removed: out var removed);

        // Assert
        committed.ShouldBeTrue();
        raised.ShouldBeFalse();
        version.ShouldBe(4);
        selection.ShouldBe([2, 3], ignoreOrder: true);
        added.ShouldBe([2, 3], ignoreOrder: true);
        removed.ShouldBe([1]);
    }

    /// <summary>Verifies a cancellable commit that nothing objects to raises the changing
    /// notification with the caller's own delta computation and then commits.</summary>
    [Fact]
    public void TryCommit_WhenCancellableAndNotCancelled_RaisesChangingThenCommits()
    {
        // Arrange
        var current = new HashSet<int> { 1 };
        ChangingArgs? observed = null;
        var version = 0;

        // Act
        var committed = SelectionCommit<int>.TryCommit(
            current: current,
            proposed: [1, 2],
            comparer: EqualityComparer<int>.Default,
            isEligible: null,
            selectionDisabled: false,
            computeDelta: DefaultDelta,
            cancellable: true,
            selectionVersion: ref version,
            createChangingArgs: _createChangingArgs,
            raiseChanging: args => observed = args,
            selection: out var selection,
            added: out var added,
            removed: out var removed);

        // Assert
        committed.ShouldBeTrue();
        _ = observed.ShouldNotBeNull();
        observed.Added.ShouldBe([2]);
        observed.Removed.ShouldBeEmpty();
        version.ShouldBe(1);
        selection.ShouldBe([1, 2], ignoreOrder: true);
        added.ShouldBe([2]);
        removed.ShouldBeEmpty();
    }

    #endregion

    #region Cancellation and reentrancy

    /// <summary>Verifies a handler that cancels the changing notification aborts the commit and
    /// leaves the version counter untouched.</summary>
    [Fact]
    public void TryCommit_WhenChangingHandlerCancels_AbortsAndLeavesVersionUnchanged()
    {
        // Arrange
        var current = new HashSet<int> { 1 };
        var version = 7;

        // Act
        var committed = SelectionCommit<int>.TryCommit(
            current: current,
            proposed: [2],
            comparer: EqualityComparer<int>.Default,
            isEligible: null,
            selectionDisabled: false,
            computeDelta: DefaultDelta,
            cancellable: true,
            selectionVersion: ref version,
            createChangingArgs: _createChangingArgs,
            raiseChanging: static args => args.Cancel = true,
            selection: out _,
            added: out _,
            removed: out _);

        // Assert
        committed.ShouldBeFalse();
        version.ShouldBe(7);
    }

    /// <summary>Verifies a reentrant handler that bumps the version counter while the changing
    /// notification is in flight invalidates this commit even though it never set Cancel - the
    /// stale proposal must not land on top of whatever the handler already decided.</summary>
    [Fact]
    public void TryCommit_WhenHandlerReentrantlyBumpsVersion_AbortsWithoutExplicitCancel()
    {
        // Arrange
        var current = new HashSet<int> { 1 };
        var version = 0;

        // Act
        var committed = SelectionCommit<int>.TryCommit(
            current: current,
            proposed: [2],
            comparer: EqualityComparer<int>.Default,
            isEligible: null,
            selectionDisabled: false,
            computeDelta: DefaultDelta,
            cancellable: true,
            selectionVersion: ref version,
            createChangingArgs: _createChangingArgs,
            raiseChanging: _ => version++,
            selection: out _,
            added: out _,
            removed: out _);

        // Assert
        committed.ShouldBeFalse();
        version.ShouldBe(1);
    }

    #endregion

    #region Selection-disabled guard

    /// <summary>Verifies a cancellable commit that would still leave a non-empty selection is
    /// refused when the owning collection's selection mode disables selection entirely.</summary>
    [Fact]
    public void TryCommit_WhenSelectionDisabledAndCancellable_AbortsNonEmptyProposal()
    {
        // Arrange
        var current = new HashSet<int>();
        var version = 0;

        // Act
        var committed = SelectionCommit<int>.TryCommit(
            current: current,
            proposed: [1],
            comparer: EqualityComparer<int>.Default,
            isEligible: null,
            selectionDisabled: true,
            computeDelta: DefaultDelta,
            cancellable: true,
            selectionVersion: ref version,
            createChangingArgs: _createChangingArgs,
            raiseChanging: static _ => { },
            selection: out _,
            added: out _,
            removed: out _);

        // Assert
        committed.ShouldBeFalse();
        version.ShouldBe(0);
    }

    /// <summary>Verifies the selection-disabled guard only applies to a cancellable commit - a
    /// non-cancellable one (an internal repair, not a fresh request) is not required to also
    /// renormalize against the mode itself, matching the documented current-behavior asymmetry
    /// between how ListView and TreeView reach this method.</summary>
    [Fact]
    public void TryCommit_WhenSelectionDisabledButNotCancellable_StillCommits()
    {
        // Arrange
        var current = new HashSet<int>();
        var version = 0;

        // Act
        var committed = SelectionCommit<int>.TryCommit(
            current: current,
            proposed: [1],
            comparer: EqualityComparer<int>.Default,
            isEligible: null,
            selectionDisabled: true,
            computeDelta: DefaultDelta,
            cancellable: false,
            selectionVersion: ref version,
            createChangingArgs: _createChangingArgs,
            raiseChanging: static _ => { },
            selection: out var selection,
            added: out _,
            removed: out _);

        // Assert
        committed.ShouldBeTrue();
        selection.ShouldBe([1]);
    }

    #endregion

    #region Availability filtering

    /// <summary>Verifies a supplied eligibility predicate drops ineligible keys from the committed
    /// selection before it is ever compared or diffed.</summary>
    [Fact]
    public void TryCommit_WhenIsEligibleProvided_FiltersProposedSelection()
    {
        // Arrange
        var current = new HashSet<int>();
        var version = 0;

        // Act
        var committed = SelectionCommit<int>.TryCommit(
            current: current,
            proposed: [1, 2, 3],
            comparer: EqualityComparer<int>.Default,
            isEligible: static key => key != 2,
            selectionDisabled: false,
            computeDelta: DefaultDelta,
            cancellable: false,
            selectionVersion: ref version,
            createChangingArgs: _createChangingArgs,
            raiseChanging: static _ => { },
            selection: out var selection,
            added: out var added,
            removed: out _);

        // Assert
        selection.ShouldBe([1, 3], ignoreOrder: true);
        added.ShouldBe([1, 3], ignoreOrder: true);
    }

    /// <summary>Verifies a null eligibility predicate skips filtering entirely, accepting the
    /// proposal as-is.</summary>
    [Fact]
    public void TryCommit_WhenIsEligibleIsNull_SkipsFiltering()
    {
        // Arrange
        var current = new HashSet<int>();
        var version = 0;

        // Act
        var committed = SelectionCommit<int>.TryCommit(
            current: current,
            proposed: [1, 2, 3],
            comparer: EqualityComparer<int>.Default,
            isEligible: null,
            selectionDisabled: false,
            computeDelta: DefaultDelta,
            cancellable: false,
            selectionVersion: ref version,
            createChangingArgs: _createChangingArgs,
            raiseChanging: static _ => { },
            selection: out var selection,
            added: out _,
            removed: out _);

        // Assert
        selection.ShouldBe([1, 2, 3], ignoreOrder: true);
    }

    #endregion

    #region Delegated delta computation

    /// <summary>Verifies the caller's own delta delegate - not a built-in ordering - decides what
    /// counts as added and removed, receiving the current and filtered-proposed sets.</summary>
    [Fact]
    public void TryCommit_PassesCurrentAndFilteredSelectionToComputeDelta()
    {
        // Arrange
        var current = new HashSet<int> { 5 };
        var version = 0;
        IReadOnlySet<int>? seenCurrent = null;
        IReadOnlySet<int>? seenSelectable = null;

        (int[], int[]) captureDelta(IReadOnlySet<int> currentArg, IReadOnlySet<int> selectableArg)
        {
            seenCurrent = currentArg;
            seenSelectable = selectableArg;
            return ([99], [98]);
        }

        // Act
        var committed = SelectionCommit<int>.TryCommit(
            current: current,
            proposed: [7],
            comparer: EqualityComparer<int>.Default,
            isEligible: null,
            selectionDisabled: false,
            computeDelta: captureDelta,
            cancellable: false,
            selectionVersion: ref version,
            createChangingArgs: _createChangingArgs,
            raiseChanging: static _ => { },
            selection: out _,
            added: out var added,
            removed: out var removed);

        // Assert
        committed.ShouldBeTrue();
        seenCurrent.ShouldBeSameAs(current);
        _ = seenSelectable.ShouldNotBeNull();
        seenSelectable.ShouldBe([7]);
        added.ShouldBe([99]);
        removed.ShouldBe([98]);
    }

    #endregion
}
