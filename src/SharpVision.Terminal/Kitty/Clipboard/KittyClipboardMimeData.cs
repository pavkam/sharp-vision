// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Clipboard;

/// <summary>Contains one owned MIME value in a completed clipboard result.</summary>
[PublicAPI]
public sealed class KittyClipboardMimeData
{
    private readonly byte[] _data;

    /// <summary>Initializes owned MIME data transferred from a transaction.</summary>
    /// <param name="mime">The UTF-8 MIME type.</param>
    /// <param name="data">The owned data buffer.</param>
    internal KittyClipboardMimeData(string mime, byte[] data)
    {
        Mime = mime;
        _data = data;
    }

    /// <summary>Gets the MIME type.</summary>
    public string Mime { get; }

    /// <summary>Gets owned data valid until the containing result is disposed.</summary>
    public ReadOnlyMemory<byte> Data => _data;

    /// <summary>Clears the owned data when its containing result is disposed.</summary>
    internal void Clear() => _data.AsSpan().Clear();
}
