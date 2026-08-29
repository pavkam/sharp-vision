// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Stores one prevalidated slot snapshot inside a compound ownership transaction.</summary>
internal readonly struct OwnedControlMutation
{
    /// <summary>Initializes one complete structural delta.</summary>
    /// <param name="slot">The distinct target slot.</param>
    /// <param name="previous">The copied prior ordered contents.</param>
    /// <param name="next">The copied proposed ordered contents.</param>
    /// <param name="removed">The roots leaving the slot.</param>
    /// <param name="added">The roots entering the slot.</param>
    /// <param name="reason">The reason removed roots become unavailable.</param>
    /// <param name="notifyUnavailable">Whether removed roots publish unavailability.</param>
    /// <param name="forcedKind">An exact operation kind supplied by a narrow mutation API, or null.</param>
    internal OwnedControlMutation(
        OwnedControlSlot slot,
        List<ControlBase> previous,
        List<ControlBase> next,
        List<ControlBase> removed,
        List<ControlBase> added,
        ReleaseReason reason,
        bool notifyUnavailable,
        OwnedControlMutationKind? forcedKind)
    {
        Debug.Assert(slot is not null, "A mutation requires one target slot.");
        Debug.Assert(previous is not null, "A mutation requires a prior snapshot.");
        Debug.Assert(next is not null, "A mutation requires a proposed snapshot.");
        Debug.Assert(removed is not null, "A mutation requires a removed-root snapshot.");
        Debug.Assert(added is not null, "A mutation requires an added-root snapshot.");
        Debug.Assert(Enum.IsDefined(reason), "A mutation requires a defined release reason.");
        Slot = slot;
        Previous = previous;
        Next = next;
        Removed = removed;
        Added = added;
        Reason = reason;
        NotifyUnavailable = notifyUnavailable;
        ForcedKind = forcedKind;
    }

    /// <summary>Gets the target slot.</summary>
    internal OwnedControlSlot Slot { get; }

    /// <summary>Gets the copied prior ordered contents.</summary>
    internal List<ControlBase> Previous { get; }

    /// <summary>Gets the copied proposed ordered contents.</summary>
    internal List<ControlBase> Next { get; }

    /// <summary>Gets roots leaving the slot in prior order.</summary>
    internal List<ControlBase> Removed { get; }

    /// <summary>Gets roots entering the slot in proposed order.</summary>
    internal List<ControlBase> Added { get; }

    /// <summary>Gets the reason removed roots become unavailable.</summary>
    internal ReleaseReason Reason { get; }

    /// <summary>Gets whether removed roots publish unavailability.</summary>
    internal bool NotifyUnavailable { get; }

    /// <summary>Gets an exact operation kind supplied by a narrow mutation API, or null.</summary>
    internal OwnedControlMutationKind? ForcedKind { get; }

    /// <summary>Creates the immutable post-commit notification for this mutation.</summary>
    /// <returns>A copied change that does not expose registry storage.</returns>
    internal OwnedControlChange CreateChange()
    {
        var kind = ResolveKind(out var previousIndex, out var currentIndex);
        return new OwnedControlChange(
            [.. Previous],
            [.. Next],
            [.. Removed],
            [.. Added],
            kind,
            previousIndex,
            currentIndex,
            Reason);
    }

    private OwnedControlMutationKind ResolveKind(out int previousIndex, out int currentIndex)
    {
        previousIndex = -1;
        currentIndex = -1;

        if (ForcedKind == OwnedControlMutationKind.Clear)
        {
            return OwnedControlMutationKind.Clear;
        }

        if (Reason == ReleaseReason.Disposed && Removed.Count == 1 && Added.Count == 0)
        {
            previousIndex = IndexOfIdentity(Previous, Removed[0]);
            return OwnedControlMutationKind.DirectDisposal;
        }

        if (Removed.Count == 0 && Added.Count == 1)
        {
            currentIndex = IndexOfIdentity(Next, Added[0]);
            return OwnedControlMutationKind.Insert;
        }

        if (Removed.Count == 1 && Added.Count == 0)
        {
            previousIndex = IndexOfIdentity(Previous, Removed[0]);
            return OwnedControlMutationKind.Remove;
        }

        if (Removed.Count == 1 && Added.Count == 1 && Previous.Count == Next.Count)
        {
            previousIndex = IndexOfIdentity(Previous, Removed[0]);
            currentIndex = IndexOfIdentity(Next, Added[0]);
            return OwnedControlMutationKind.Replace;
        }

        return Removed.Count == 0 && Added.Count == 0 && Previous.Count == Next.Count &&
               TryResolveMove(out previousIndex, out currentIndex)
            ? OwnedControlMutationKind.Move
            : Next.Count == 0 && Previous.Count > 0
            ? OwnedControlMutationKind.Clear
            : OwnedControlMutationKind.Reset;
    }

    private bool TryResolveMove(out int previousIndex, out int currentIndex)
    {
        for (var oldIndex = 0; oldIndex < Previous.Count; oldIndex++)
        {
            var candidate = new List<ControlBase>(Previous);
            var control = candidate[oldIndex];
            candidate.RemoveAt(oldIndex);

            for (var newIndex = 0; newIndex < candidate.Count + 1; newIndex++)
            {
                candidate.Insert(newIndex, control);

                if (SameOrder(candidate, Next))
                {
                    previousIndex = oldIndex;
                    currentIndex = newIndex;
                    return true;
                }

                candidate.RemoveAt(newIndex);
            }
        }

        previousIndex = -1;
        currentIndex = -1;
        return false;
    }

    private static int IndexOfIdentity(List<ControlBase> controls, ControlBase candidate)
    {
        for (var index = 0; index < controls.Count; index++)
        {
            if (ReferenceEquals(controls[index], candidate))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool SameOrder(List<ControlBase> left, List<ControlBase> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!ReferenceEquals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }
}
