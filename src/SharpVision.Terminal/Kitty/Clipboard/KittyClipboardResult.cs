// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Clipboard;

/// <summary>Owns completed Kitty clipboard MIME data until disposal.</summary>
[PublicAPI]
public sealed class KittyClipboardResult: IDisposable
{
    private bool _disposed;

    /// <summary>Initializes a result from transferred MIME data.</summary>
    /// <param name="items">The owned result items.</param>
    internal KittyClipboardResult(KittyClipboardMimeData[] items) => Items = Array.AsReadOnly(items);

    /// <summary>Gets the immutable item collection in terminal delivery order.</summary>
    public IReadOnlyList<KittyClipboardMimeData> Items { get; }

    /// <summary>Clears every owned data buffer. Disposal is idempotent.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var item in Items)
        {
            item.Clear();
        }

        _disposed = true;
    }
}
