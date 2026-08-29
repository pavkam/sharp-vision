// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Multiplexing;

using Input;

using Protocols;

using Xterm;

/// <summary>Validates that an unwrapped envelope contains exactly one authorized typed reply.</summary>
internal sealed class ReplyValidationSink:
    IProtocolSink,
    IPaletteResponseSink,
    IMetricsResponseSink,
    IStatusResponseSink,
    ICapabilityResponseSink,
    IKittyGraphicsResponseSink,
    IItermCapabilitiesResponseSink,
    IClipboardReplySink,
    IKittyClipboardPacketSink
{
    private int _responses;
    private bool Invalid { get; set; }

    /// <summary>Gets whether decoding produced exactly one typed reply and no other event.</summary>
    public bool Valid => !Invalid && _responses == 1;

    /// <summary>Gets the typed operation family produced by the decoded reply.</summary>
    public MultiplexingOperation Operation { get; private set; } = MultiplexingOperation.None;

    /// <inheritdoc/>
    public void Input(in Stroke value) => Invalid = true;

    /// <inheritdoc/>
    public void Input(in TerminalText value) => Invalid = true;

    /// <inheritdoc/>
    public void Input(in Pointer value) => Invalid = true;

    /// <inheritdoc/>
    public void Input(Paste value) => Invalid = true;

    /// <inheritdoc/>
    public void Input(in TerminalFocus value) => Invalid = true;

    /// <inheritdoc/>
    public void Input(in Diagnostic value) => Invalid = true;

    /// <inheritdoc/>
    public void Response(in XtermCapabilitiesResponse value)
    {
        if (value.Kind is ResponseKind.PrimaryAttributes or
            ResponseKind.SecondaryAttributes or
            ResponseKind.PrivateMode or
            ResponseKind.Keyboard or
            ResponseKind.CursorPosition)
        {
            _responses++;
            Operation = MultiplexingOperation.CapabilityQueries;
        }
        else
        {
            Invalid = true;
        }
    }

    /// <inheritdoc/>
    public void Response(in PaletteResponse value) => RecordCapabilityReply();

    /// <inheritdoc/>
    public void Response(in MetricsResponse value) => RecordCapabilityReply();

    /// <inheritdoc/>
    public void Response(in StatusResponse value) => RecordCapabilityReply();

    /// <inheritdoc/>
    public void Response(CapabilityResponse value) => RecordCapabilityReply();

    /// <inheritdoc/>
    public void Response(ItermCapabilitiesResponse value) => RecordCapabilityReply();

    /// <inheritdoc/>
    public void Response(Kitty.Graphics.KittyGraphicsResponse value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Valid)
        {
            RecordCapabilityReply();
        }
        else
        {
            Invalid = true;
        }
    }

    /// <inheritdoc/>
    public void Response(in Clipboard.ClipboardReply value) => RecordClipboardReply();

    /// <inheritdoc/>
    public void Response(Kitty.Clipboard.KittyClipboardPacket value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Valid)
        {
            RecordClipboardReply();
        }
        else
        {
            Invalid = true;
        }
    }

    /// <inheritdoc/>
    public void Sequence(ProtocolSequence value) => Invalid = true;

    private void RecordCapabilityReply()
    {
        _responses++;
        Operation = MultiplexingOperation.CapabilityQueries;
    }

    private void RecordClipboardReply()
    {
        _responses++;
        Operation = MultiplexingOperation.Clipboard;
    }
}
