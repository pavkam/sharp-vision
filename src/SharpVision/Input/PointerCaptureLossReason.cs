// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Identifies why a control stopped owning exclusive pointer capture.</summary>
[PublicAPI]
public enum PointerCaptureLossReason
{
    /// <summary>The owner explicitly released capture.</summary>
    Explicit,

    /// <summary>Another eligible control replaced the owner.</summary>
    Transferred,

    /// <summary>The owner became hidden, disabled, detached, or disposed.</summary>
    Unavailable,

    /// <summary>The terminal reported focus loss.</summary>
    TerminalFocusLost,
    /// <summary>A newly active modal plane excluded the still-available owner.</summary>
    ModalScopeChanged,
}
