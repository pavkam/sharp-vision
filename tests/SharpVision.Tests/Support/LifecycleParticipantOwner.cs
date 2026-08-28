// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes interaction lifecycle participant registration and publication for foundation tests.</summary>
internal sealed class LifecycleParticipantOwner: ControlBase
{
    /// <summary>Registers one participant through the internal owner seam.</summary>
    /// <param name="participant">The non-null participant.</param>
    internal void Register(IControlLifecycleParticipant participant) => RegisterLifecycleParticipant(participant);

    /// <summary>Commits one direct focus fact.</summary>
    /// <param name="focused">Whether focus is owned.</param>
    internal void CommitFocus(bool focused) => SetFocused(focused);

    /// <summary>Publishes one defined capture-loss reason.</summary>
    /// <param name="reason">The capture-loss reason.</param>
    internal void LoseCapture(PointerCaptureLossReason reason) => NotifyLostPointerCapture(reason);

    /// <summary>Publishes one defined unavailability reason.</summary>
    /// <param name="reason">The unavailability reason.</param>
    internal void BecomeUnavailable(ReleaseReason reason) => NotifyUnavailable(reason);
}
