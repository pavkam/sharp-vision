// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Describes one immutable post-commit change to an owned-control slot.</summary>
/// <remarks>
/// Every array-backed member is a dedicated snapshot copied at commit time, not a view over the
/// slot's live mutable storage, so this change may be retained after its guarded publication
/// callback without ever observing a later mutation to the slot. The <see cref="ControlBase"/>
/// instances referenced from <see cref="Previous"/>, <see cref="Current"/>, <see cref="Removed"/>,
/// and <see cref="Added"/> remain live, mutable objects whose own state - parentage, disposal,
/// properties - can keep changing after this snapshot was taken; only the order and membership this
/// change captured stay fixed.
/// </remarks>
public readonly struct OwnedControlChange
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
    public ReadOnlyMemory<ControlBase> Previous { get; }

    /// <summary>Gets the immutable ordered snapshot after the commit.</summary>
    public ReadOnlyMemory<ControlBase> Current { get; }

    /// <summary>Gets roots that left the slot in prior order.</summary>
    /// <remarks>
    /// For <see cref="OwnedControlMutationKind.DirectDisposal"/>, this holds exactly the one control
    /// that disposed itself and requested removal from its owner before disposal publication;
    /// <see cref="Added"/> is empty for that kind.
    /// </remarks>
    public ReadOnlyMemory<ControlBase> Removed { get; }

    /// <summary>Gets roots that entered the slot in current order.</summary>
    /// <remarks>
    /// For <see cref="OwnedControlMutationKind.DirectDisposal"/>, this is always empty: a directly
    /// disposing control leaves its slot without any replacement entering it.
    /// </remarks>
    public ReadOnlyMemory<ControlBase> Added { get; }

    /// <summary>Gets the normalized structural operation.</summary>
    public OwnedControlMutationKind Kind { get; }

    /// <summary>Gets the affected position in <see cref="Previous"/>, or -1.</summary>
    public int PreviousIndex { get; }

    /// <summary>Gets the affected position in <see cref="Current"/>, or -1.</summary>
    public int CurrentIndex { get; }

    /// <summary>Gets the reason removed roots became unavailable.</summary>
    public ReleaseReason Reason { get; }
}
