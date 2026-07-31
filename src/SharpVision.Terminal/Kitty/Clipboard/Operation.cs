// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Clipboard;

/// <summary>Identifies a Kitty OSC 5522 packet operation.</summary>
[PublicAPI]
public enum Operation
{
    /// <summary>No operation was decoded.</summary>
    None,

    /// <summary>A clipboard read, list, or read response.</summary>
    Read,

    /// <summary>A clipboard write start or write response.</summary>
    Write,

    /// <summary>A clipboard MIME data packet or write terminator.</summary>
    WriteData,

    /// <summary>A MIME alias packet.</summary>
    WriteAlias
}
