// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Identifies one cross-cutting status color resolved through the active theme.</summary>
[PublicAPI]
public enum StatusColor
{
    /// <summary>An error or failed state.</summary>
    Error,

    /// <summary>A caution or degraded state.</summary>
    Warning,

    /// <summary>A successful or healthy state.</summary>
    Success,

    /// <summary>Neutral informational emphasis.</summary>
    Info,

    /// <summary>Secondary text, tracks, and quiet supporting information.</summary>
    Muted,

    /// <summary>The marked grapheme in an enabled access-key caption.</summary>
    Hotkey
}
