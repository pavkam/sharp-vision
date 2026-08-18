// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Support;

using SharpVision.Terminal.Input;

/// <summary>Records typed input and complete protocol events in delivery order.</summary>
internal sealed class RecordingProtocolSink:
    IProtocolSink,
    IPaletteResponseSink,
    IMetricsResponseSink,
    IStatusResponseSink,
    ICapabilityResponseSink,
    IKittyGraphicsResponseSink,
    IClipboardReplySink,
    IKittyClipboardPacketSink
{
    /// <summary>Gets key events in delivery order.</summary>
    internal List<Stroke> Strokes { get; } = [];

    /// <summary>Gets text events in delivery order.</summary>
    internal List<TerminalText> Text { get; } = [];

    /// <summary>Gets pointer events in delivery order.</summary>
    internal List<Pointer> Pointers { get; } = [];

    /// <summary>Gets owned paste events in delivery order.</summary>
    internal List<Paste> Pastes { get; } = [];

    /// <summary>Gets terminal focus events in delivery order.</summary>
    internal List<TerminalFocus> Focus { get; } = [];

    /// <summary>Gets redacted diagnostics in delivery order.</summary>
    internal List<Diagnostic> Diagnostics { get; } = [];

    /// <summary>Gets recognized responses in delivery order.</summary>
    internal List<XtermCapabilitiesResponse> Responses { get; } = [];

    /// <summary>Gets recognized color responses in delivery order.</summary>
    internal List<PaletteResponse> PaletteResponses { get; } = [];

    /// <summary>Gets recognized metrics responses in delivery order.</summary>
    internal List<MetricsResponse> MetricsResponses { get; } = [];

    /// <summary>Gets recognized DECRQSS responses in delivery order.</summary>
    internal List<StatusResponse> StatusResponses { get; } = [];

    /// <summary>Gets recognized XTGETTCAP responses in delivery order.</summary>
    internal List<CapabilityResponse> CapabilityResponses { get; } = [];

    /// <summary>Gets recognized Kitty graphics responses in delivery order.</summary>
    internal List<Kitty.Graphics.KittyGraphicsResponse> KittyGraphicsResponses { get; } = [];

    /// <summary>Gets recognized OSC 52 clipboard replies in delivery order.</summary>
    internal List<ClipboardReply> ClipboardReplies { get; } = [];

    /// <summary>Gets recognized Kitty OSC 5522 clipboard packets in delivery order.</summary>
    internal List<Kitty.Clipboard.KittyClipboardPacket> KittyClipboardPackets { get; } = [];

    /// <summary>Gets owned terminal strings in delivery order.</summary>
    internal List<ProtocolSequence> Sequences { get; } = [];

    /// <summary>Gets callback family names in delivery order.</summary>
    internal List<string> Order { get; } = [];

    /// <inheritdoc/>
    public void Input(in Stroke value)
    {
        Strokes.Add(value);
        Order.Add("key");
    }

    /// <inheritdoc/>
    public void Input(in TerminalText value)
    {
        Text.Add(value);
        Order.Add("text");
    }

    /// <inheritdoc/>
    public void Input(in Pointer value)
    {
        Pointers.Add(value);
        Order.Add("pointer");
    }

    /// <inheritdoc/>
    public void Input(Paste value)
    {
        Pastes.Add(value);
        Order.Add("paste");
    }

    /// <inheritdoc/>
    public void Input(in TerminalFocus value)
    {
        Focus.Add(value);
        Order.Add("focus");
    }

    /// <inheritdoc/>
    public void Input(in Diagnostic value)
    {
        Diagnostics.Add(value);
        Order.Add("diagnostic");
    }

    /// <inheritdoc/>
    public void Response(in XtermCapabilitiesResponse value)
    {
        Responses.Add(value);
        Order.Add("response");
    }

    /// <inheritdoc/>
    public void Response(in PaletteResponse value)
    {
        PaletteResponses.Add(value);
        Order.Add("palette-response");
    }

    /// <inheritdoc/>
    public void Response(in MetricsResponse value)
    {
        MetricsResponses.Add(value);
        Order.Add("metrics-response");
    }

    /// <inheritdoc/>
    public void Response(in StatusResponse value)
    {
        StatusResponses.Add(value);
        Order.Add("status-response");
    }

    /// <inheritdoc/>
    public void Response(CapabilityResponse value)
    {
        CapabilityResponses.Add(value);
        Order.Add("capability-response");
    }

    /// <inheritdoc/>
    public void Response(Kitty.Graphics.KittyGraphicsResponse value)
    {
        KittyGraphicsResponses.Add(value);
        Order.Add("kitty-graphics-response");
    }

    /// <inheritdoc/>
    public void Response(in ClipboardReply value)
    {
        ClipboardReplies.Add(value);
        Order.Add("clipboard-reply");
    }

    /// <inheritdoc/>
    public void Response(Kitty.Clipboard.KittyClipboardPacket value)
    {
        KittyClipboardPackets.Add(value);
        Order.Add("kitty-clipboard-packet");
    }

    /// <inheritdoc/>
    public void Sequence(ProtocolSequence value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Sequences.Add(value);
        Order.Add("sequence");
    }
}
