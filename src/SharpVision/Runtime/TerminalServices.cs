// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using System.Buffers;
using System.Diagnostics;
using System.Text;

using SharpVision.Terminal.Clipboard;
using SharpVision.Terminal.Protocols;

/// <summary>Encodes implemented output protocols and posts them through the ordered write path.</summary>
internal sealed class TerminalServices: ITerminalServices, IBell, IClipboard
{
    private readonly Application _application;

    internal TerminalServices(Application application)
    {
        Debug.Assert(application is not null, "The owning application must be provided.");
        _application = application;
    }

    /// <inheritdoc/>
    public IBell Bell => this;

    /// <inheritdoc/>
    public IClipboard Clipboard => this;

    /// <inheritdoc/>
    public bool IsSupported =>
        _application.Capabilities.Osc52.IsSupported || _application.Capabilities.KittyClipboard.IsSupported;

    /// <inheritdoc/>
    public void Ring() => _application.PostOutOfBand(new byte[] { 0x07 });

    /// <inheritdoc/>
    public void SetTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        int byteCount = Encoding.UTF8.GetByteCount(title);
        ArrayBufferWriter<byte> destination = new(byteCount + 8);
        byte[] rented = ArrayPool<byte>.Shared.Rent(Math.Max(1, byteCount));

        try
        {
            int written = Encoding.UTF8.GetBytes(title, rented);
            Osc.Title(new Writer(destination), rented.AsSpan(0, written));
            _application.PostOutOfBand(destination.WrittenMemory);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    /// <inheritdoc/>
    public void Write(ReadOnlySpan<char> text, Selection selection = Selection.Clipboard)
    {
        if (!IsSupported)
        {
            return;
        }

        int byteCount = Encoding.UTF8.GetByteCount(text);
        ArrayBufferWriter<byte> destination = new(byteCount + 16);
        byte[] rented = ArrayPool<byte>.Shared.Rent(Math.Max(1, byteCount));

        try
        {
            int written = Encoding.UTF8.GetBytes(text, rented);
            Osc52.Write(new Writer(destination), selection, rented.AsSpan(0, written));
            _application.PostOutOfBand(destination.WrittenMemory);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    /// <inheritdoc/>
    public void Request(Selection selection = Selection.Clipboard)
    {
        if (!IsSupported)
        {
            return;
        }

        ArrayBufferWriter<byte> destination = new(8);
        Osc52.Query(new Writer(destination), selection);
        _application.PostOutOfBand(destination.WrittenMemory);
    }
}
