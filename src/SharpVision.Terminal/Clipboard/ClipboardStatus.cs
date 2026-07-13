// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Clipboard;

/// <summary>Describes the outcome of a clipboard request or decoded reply.</summary>
public enum ClipboardStatus
{
    /// <summary>Clipboard text was decoded successfully.</summary>
    Success,

    /// <summary>The value requests clipboard data rather than carrying it.</summary>
    Query,

    /// <summary>The effective terminal profile has no safe clipboard path.</summary>
    Unavailable,

    /// <summary>The terminal or user denied clipboard access.</summary>
    Denied,

    /// <summary>The reply violated grammar, Base64, UTF-8, or size bounds.</summary>
    Malformed,
}
