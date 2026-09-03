// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Identifies one exact detached lifetime of one control.</summary>
/// <remarks>
/// The identity is intentionally opaque and owner-bound. Framework continuations retain it only
/// to request one synchronous publication through its owner; they cannot infer lifecycle order or
/// recreate current authority from numeric state.
/// </remarks>
internal sealed class ControlDetachedAttachmentToken
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
