// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Identifies one exact detached lifetime of one control.</summary>
/// <remarks>
/// This is the detached counterpart to <see cref="ControlAttachmentToken"/>: a derived control
/// captures one with <see cref="ControlBase.TryCaptureDetachedAttachment"/>, then hands it back to
/// <see cref="ControlBase.TryPublishForCurrentDetachedAttachment"/> so a synchronous publication
/// runs only while this exact detached lifetime - not merely "still detached" - is still current.
/// The identity is intentionally opaque and owner-bound: a derived control can retain and return an
/// instance, but cannot compare, inspect, or recreate current authority from it. Only
/// <see cref="ControlBase"/> itself can validate one, so a token from a foreign control instance is
/// inert everywhere but where it was captured.
/// </remarks>
public sealed class ControlDetachedAttachmentToken
{
    private ControlBase Control { get; }
    private object Identity { get; }

    /// <summary>Captures one owner and its control-owned opaque lifecycle identity.</summary>
    /// <param name="control">The exact detached control.</param>
    /// <param name="identity">The control-owned opaque lifecycle identity.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal ControlDetachedAttachmentToken(ControlBase control, object identity)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(identity);

        Control = control;
        Identity = identity;
    }

    /// <summary>Checks exact owner and opaque lifecycle identity.</summary>
    /// <param name="control">The candidate owner.</param>
    /// <param name="identity">The candidate opaque lifecycle identity.</param>
    /// <returns>True only when both identity components still match.</returns>
    internal bool Matches(ControlBase control, object identity) =>
        ReferenceEquals(Control, control) &&
        ReferenceEquals(Identity, identity);
}
