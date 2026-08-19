// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.ComponentModel;

using JetBrains.Annotations;

/// <summary>Diffs and commits a proposed selection set against a keyed collection's current
/// selection, including the cancellable-event and reentrancy machinery that guards the commit.</summary>
/// <remarks>
/// A multi-select collection commits a proposed selection the same way regardless of what it
/// iterates over: filter the proposal against availability, short-circuit when it would not
/// actually change anything, compute the added/removed delta, snapshot a version counter, raise a
/// cancellable changing notification, and refuse to commit if that notification was cancelled or a
/// reentrant handler already bumped the version counter while it ran. This type owns only that
/// orchestration - the delta algorithm and the changing event's argument type are both supplied by
/// the caller, because a flat index range and a tree walk disagree on how "stable order" is defined,
/// and each control's changing event carries its own argument type.
/// </remarks>
internal static class SelectionCommit<TKey>
{
    /// <summary>Attempts to commit <paramref name="proposed"/> as the new selection.</summary>
    /// <param name="current">The selection before this commit.</param>
    /// <param name="proposed">The proposed next selection.</param>
    /// <param name="comparer">The equality comparer the filtered selection set is built with.</param>
    /// <param name="isEligible">Reports whether a key may remain selected, or null to skip
    /// filtering entirely and accept <paramref name="proposed"/> as-is.</param>
    /// <param name="selectionDisabled">Whether the owning collection's selection mode forbids any
    /// selection at all; a cancellable commit that would still leave one non-empty is refused.</param>
    /// <param name="computeDelta">Computes the stable added/removed lists for a transition from
    /// <paramref name="current"/> to the filtered selection.</param>
    /// <param name="cancellable">Whether a changing notification is raised before committing.</param>
    /// <param name="selectionVersion">The owning collection's selection-version counter. Read once
    /// before any notification is raised and compared after, so a handler that reentrantly commits
    /// its own selection change invalidates this one; incremented on an accepted commit.</param>
    /// <param name="createChangingArgs">Constructs the caller's own changing event arguments from
    /// the computed added/removed delta.</param>
    /// <param name="raiseChanging">Raises the caller's own changing event with the constructed
    /// arguments.</param>
    /// <param name="selection">The filtered selection the caller should adopt when this method
    /// returns true.</param>
    /// <param name="added">The stable list of keys newly selected by this commit.</param>
    /// <param name="removed">The stable list of keys newly deselected by this commit.</param>
    /// <returns><see langword="true"/> when the commit was accepted and <paramref name="selection"/>,
    /// <paramref name="added"/>, and <paramref name="removed"/> should be applied; otherwise,
    /// <see langword="false"/>.</returns>
    public static bool TryCommit<TChangingArgs>(
        IReadOnlySet<TKey> current,
        IEnumerable<TKey> proposed,
        IEqualityComparer<TKey> comparer,
        [InstantHandle] Func<TKey, bool>? isEligible,
        bool selectionDisabled,
        [InstantHandle] Func<IReadOnlySet<TKey>, IReadOnlySet<TKey>, (TKey[] Added, TKey[] Removed)> computeDelta,
        bool cancellable,
        ref int selectionVersion,
        [InstantHandle] Func<TKey[], TKey[], TChangingArgs> createChangingArgs,
        [InstantHandle] Action<TChangingArgs> raiseChanging,
        out HashSet<TKey> selection,
        out TKey[] added,
        out TKey[] removed)
        where TChangingArgs : CancelEventArgs
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentNullException.ThrowIfNull(computeDelta);
        ArgumentNullException.ThrowIfNull(createChangingArgs);
        ArgumentNullException.ThrowIfNull(raiseChanging);

        selection = isEligible is null
            ? new HashSet<TKey>(proposed, comparer)
            : new HashSet<TKey>(proposed.Where(isEligible), comparer);

        if (selection.SetEquals(current))
        {
            added = [];
            removed = [];
            return false;
        }

        (added, removed) = computeDelta(current, selection);

        var version = selectionVersion;

        if (cancellable)
        {
            var changingArgs = createChangingArgs(added, removed);
            raiseChanging(changingArgs);

            if (changingArgs.Cancel ||
                version != selectionVersion ||
                (selectionDisabled && selection.Count > 0))
            {
                return false;
            }
        }

        selectionVersion++;
        return true;
    }
}
