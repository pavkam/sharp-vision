// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

/// <summary>Enumerates availability transitions shared by deterministic document-autoscroll evidence.</summary>
internal enum AutoScrollCancellation
{
    /// <summary>The primary pointer is released normally.</summary>
    Release,

    /// <summary>Capture is explicitly cleared.</summary>
    CaptureLoss,

    /// <summary>The terminal reports focus loss.</summary>
    TerminalFocusLoss,

    /// <summary>The document becomes hidden.</summary>
    Hide,

    /// <summary>The document becomes disabled.</summary>
    Disable,

    /// <summary>The document is disposed.</summary>
    Dispose,

    /// <summary>The document is detached from its mounted owner.</summary>
    Detach
}
