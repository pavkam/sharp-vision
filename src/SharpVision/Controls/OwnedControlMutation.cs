// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Stores one prevalidated slot snapshot inside a compound ownership transaction.</summary>
internal readonly struct OwnedControlMutation
{
    /// <summary>Initializes one complete structural delta.</summary>
    /// <param name="slot">The distinct target slot.</param>
    /// <param name="next">The copied proposed ordered contents.</param>
    /// <param name="removed">The roots leaving the slot.</param>
    /// <param name="added">The roots entering the slot.</param>
    /// <param name="reason">The reason removed roots become unavailable.</param>
    /// <param name="notifyUnavailable">Whether removed roots publish unavailability.</param>
    internal OwnedControlMutation(
        OwnedControlSlot slot,
        List<ControlBase> next,
        List<ControlBase> removed,
        List<ControlBase> added,
        ReleaseReason reason,
        bool notifyUnavailable)
    {
        Debug.Assert(slot is not null, "A mutation requires one target slot.");
        Debug.Assert(next is not null, "A mutation requires a proposed snapshot.");
        Debug.Assert(removed is not null, "A mutation requires a removed-root snapshot.");
        Debug.Assert(added is not null, "A mutation requires an added-root snapshot.");
        Debug.Assert(Enum.IsDefined(reason), "A mutation requires a defined release reason.");
        Slot = slot;
        Next = next;
        Removed = removed;
        Added = added;
        Reason = reason;
        NotifyUnavailable = notifyUnavailable;
    }

    /// <summary>Gets the target slot.</summary>
    internal OwnedControlSlot Slot { get; }

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
}
