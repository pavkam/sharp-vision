// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Describes one immutable post-commit change to an owned-control slot.</summary>
/// <remarks>
/// Every sequence is a dedicated snapshot rather than the slot's mutable storage. The change may
/// therefore be retained after its guarded publication callback without observing later mutations.
/// </remarks>
internal readonly struct OwnedControlChange
{
    /// <summary>Initializes one complete committed structural change.</summary>
    /// <param name="previous">The copied order before the commit.</param>
    /// <param name="current">The copied committed order.</param>
    /// <param name="removed">The copied roots that left the slot.</param>
    /// <param name="added">The copied roots that entered the slot.</param>
    /// <param name="kind">The normalized mutation kind.</param>
    /// <param name="previousIndex">The affected position in <paramref name="previous"/>, or -1.</param>
    /// <param name="currentIndex">The affected position in <paramref name="current"/>, or -1.</param>
    /// <param name="reason">The reason removed roots became unavailable.</param>
    internal OwnedControlChange(
        ControlBase[] previous,
        ControlBase[] current,
        ControlBase[] removed,
        ControlBase[] added,
        OwnedControlMutationKind kind,
        int previousIndex,
        int currentIndex,
        ReleaseReason reason)
    {
        Debug.Assert(previous is not null, "A change requires a prior snapshot.");
        Debug.Assert(current is not null, "A change requires a current snapshot.");
        Debug.Assert(removed is not null, "A change requires a removed-root snapshot.");
        Debug.Assert(added is not null, "A change requires an added-root snapshot.");
        Debug.Assert(Enum.IsDefined(kind), "A change requires a defined mutation kind.");
        Debug.Assert(previousIndex >= -1, "A prior index is absent or non-negative.");
        Debug.Assert(currentIndex >= -1, "A current index is absent or non-negative.");
        Debug.Assert(Enum.IsDefined(reason), "A change requires a defined release reason.");
        Previous = previous;
        Current = current;
        Removed = removed;
        Added = added;
        Kind = kind;
        PreviousIndex = previousIndex;
        CurrentIndex = currentIndex;
        Reason = reason;
    }

    /// <summary>Gets the immutable ordered snapshot before the commit.</summary>
    internal ReadOnlyMemory<ControlBase> Previous { get; }

    /// <summary>Gets the immutable ordered snapshot after the commit.</summary>
    internal ReadOnlyMemory<ControlBase> Current { get; }

    /// <summary>Gets roots that left the slot in prior order.</summary>
    internal ReadOnlyMemory<ControlBase> Removed { get; }

    /// <summary>Gets roots that entered the slot in current order.</summary>
    internal ReadOnlyMemory<ControlBase> Added { get; }

    /// <summary>Gets the normalized structural operation.</summary>
    internal OwnedControlMutationKind Kind { get; }

    /// <summary>Gets the affected position in <see cref="Previous"/>, or -1.</summary>
    internal int PreviousIndex { get; }

    /// <summary>Gets the affected position in <see cref="Current"/>, or -1.</summary>
    internal int CurrentIndex { get; }

    /// <summary>Gets the reason removed roots became unavailable.</summary>
    internal ReleaseReason Reason { get; }
}
