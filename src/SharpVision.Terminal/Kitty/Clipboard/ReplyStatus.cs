// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Clipboard;

/// <summary>Identifies a Kitty OSC 5522 response status.</summary>
[PublicAPI]
public enum ReplyStatus
{
    /// <summary>The packet has no response status.</summary>
    None,

    /// <summary>The terminal accepted a read request.</summary>
    Ok,

    /// <summary>The packet carries one data chunk.</summary>
    Data,

    /// <summary>The transaction completed successfully.</summary>
    Done,

    /// <summary>An input/output error occurred.</summary>
    Io,

    /// <summary>The request or Base64 data was invalid.</summary>
    Invalid,

    /// <summary>The requested selection or operation is unavailable.</summary>
    Unavailable,

    /// <summary>Clipboard permission was denied.</summary>
    Denied,

    /// <summary>The clipboard resource is temporarily busy.</summary>
    Busy
}
