// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;


/// <summary>Records completed activations from the shared press behavior.</summary>
internal sealed class ProbePressable: Pressable
{
    /// <summary>Initializes an empty pressable probe.</summary>
    internal ProbePressable()
    {
    }

    /// <summary>Gets completed activation causes in commit order.</summary>
    internal List<ActivationCause> Activations { get; } = [];

    /// <summary>Gets implicit capture-cancellation reasons after the shared behavior has reset.</summary>
    internal List<ReleaseReason> CaptureCancellations { get; } = [];

    /// <summary>Gets whether capture was still reported inside the latest cancellation callback.</summary>
    internal bool HadCaptureDuringCancellation { get; private set; }

    /// <summary>Gets whether pressed state remained inside the latest cancellation callback.</summary>
    internal bool WasPressedDuringCancellation { get; private set; }

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause) => Activations.Add(cause);

    /// <inheritdoc/>
    protected override void OnPointerCaptureCancelled(ReleaseReason reason)
    {
        base.OnPointerCaptureCancelled(reason);
        HadCaptureDuringCancellation = HasPointerCapture;
        WasPressedDuringCancellation = IsPressed;
        CaptureCancellations.Add(reason);
    }
}
