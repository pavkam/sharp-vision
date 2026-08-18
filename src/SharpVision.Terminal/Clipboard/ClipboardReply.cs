// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Clipboard;

/// <summary>Contains an immutable OSC 52 clipboard decode result.</summary>
[PublicAPI]
public readonly record struct ClipboardReply
{
    /// <summary>Initializes a clipboard result from owned data.</summary>
    /// <param name="status">The decode or operation status.</param>
    /// <param name="selection">The selected clipboard target.</param>
    /// <param name="data">Owned immutable UTF-8 data.</param>
    internal ClipboardReply(
        ClipboardStatus status,
        Selection selection,
        ReadOnlyMemory<byte> data)
    {
        Status = status;
        Selection = selection;
        Data = data;
    }

    /// <summary>Gets the decode or operation status.</summary>
    public ClipboardStatus Status { get; }

    /// <summary>Gets the clipboard target.</summary>
    public Selection Selection { get; }

    /// <summary>Gets owned UTF-8 clipboard bytes, empty for non-success results.</summary>
    public ReadOnlyMemory<byte> Data { get; }
}
