// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Identifies why active pointer interaction was cancelled.</summary>
[PublicAPI]
public enum ReleaseReason
{
    /// <summary>The owned subtree detached.</summary>
    Detached,

    /// <summary>The owned subtree became disabled.</summary>
    Disabled,

    /// <summary>The owned subtree became hidden or collapsed.</summary>
    Hidden,

    /// <summary>The terminal reported focus loss.</summary>
    TerminalFocusLost,

    /// <summary>Another control replaced the active capture owner.</summary>
    Transferred,

    /// <summary>The owned subtree was disposed.</summary>
    Disposed,

    /// <summary>A newly active modal plane excluded active pointer interaction.</summary>
    ModalScopeChanged,
}
