// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Receives the control lifecycle transitions required to cancel one composed interaction.</summary>
internal interface IControlLifecycleParticipant
{
    /// <summary>Reconciles one committed direct-focus transition.</summary>
    /// <param name="focused">Whether the owning control now has direct focus.</param>
    public void FocusChanged(bool focused);

    /// <summary>Reconciles loss of the owning control's pointer capture.</summary>
    /// <param name="reason">The defined reason capture ended.</param>
    public void CaptureLost(PointerCaptureLossReason reason);

    /// <summary>Releases transient state when the owning control becomes unavailable.</summary>
    /// <param name="reason">The defined reason the control became unavailable.</param>
    public void Unavailable(ReleaseReason reason);
}
