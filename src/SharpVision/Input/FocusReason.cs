// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Identifies why one logical keyboard-focus transition occurred.</summary>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Pointer is the established terminal input domain term.")]
[PublicAPI]
public enum FocusReason
{
    /// <summary>Focus was requested through <see cref="ControlBase.Focus"/>.</summary>
    Programmatic,

    /// <summary>Focus was moved by keyboard traversal or widget navigation.</summary>
    Keyboard,

    /// <summary>Focus was requested by primary pointer activation.</summary>
    Pointer,

    /// <summary>Focus was restored by an owning scope.</summary>
    Restore,

    /// <summary>Focus was cleared because the old target became unavailable.</summary>
    Unavailable
}
