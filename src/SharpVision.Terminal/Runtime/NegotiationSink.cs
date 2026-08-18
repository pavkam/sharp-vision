// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using Capabilities;

using Input;

using Xterm;

using InputText = TerminalText;

/// <summary>Updates capability negotiation before forwarding ordered protocol events.</summary>
internal sealed class NegotiationSink:
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
    private readonly ISink _destination;
    private readonly Negotiator _negotiator;

    /// <summary>Initializes one synchronous interception boundary.</summary>
    /// <param name="destination">The non-null runtime destination.</param>
    /// <param name="negotiator">The non-null active negotiator.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public NegotiationSink(ISink destination, Negotiator negotiator)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(negotiator);
        _destination = destination;
        _negotiator = negotiator;
    }

    /// <inheritdoc/>
    public void Input(in Stroke value) => _destination.Input(in value);

    /// <inheritdoc/>
    public void Input(in InputText value) => _destination.Input(in value);

    /// <inheritdoc/>
    public void Input(in Pointer value) => _destination.Input(in value);

    /// <inheritdoc/>
    public void Input(Paste value) => _destination.Input(value);

    /// <inheritdoc/>
    public void Input(in TerminalFocus value) => _destination.Input(in value);

    /// <inheritdoc/>
    public void Input(in Diagnostic value) => _destination.Input(in value);

    /// <inheritdoc/>
    public void Response(in XtermCapabilitiesResponse value)
    {
        _ = _negotiator.Accept(in value);
        _destination.Response(in value);
    }

    /// <inheritdoc/>
    public void Response(in PaletteResponse value)
    {
        _ = _negotiator.Accept(in value);
        _destination.Dispatch(in value);
    }

    /// <inheritdoc/>
    public void Response(in MetricsResponse value)
    {
        _ = _negotiator.Accept(in value);
        _destination.Dispatch(in value);
    }

    /// <inheritdoc/>
    public void Response(in StatusResponse value)
    {
        _ = _negotiator.Accept(in value);
        _destination.Dispatch(in value);
    }

    /// <inheritdoc/>
    public void Response(ItermCapabilitiesResponse value)
    {
        _ = _negotiator.Accept(value);
        _destination.Dispatch(value);
    }

    /// <inheritdoc/>
    public void Response(CapabilityResponse value)
    {
        _ = _negotiator.Accept(value);
        _destination.Dispatch(value);
    }

    /// <inheritdoc/>
    public void Response(Kitty.Graphics.KittyGraphicsResponse value)
    {
        _ = _negotiator.Accept(value);
        _destination.Dispatch(value);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Negotiation has no interest in clipboard replies - <see cref="Negotiator"/> exposes no
    /// <c>Accept</c> overload for them - so this exists solely to keep this sink transparent to
    /// <see cref="IClipboardReplySink"/>. Without it, wrapping a destination that implements the
    /// optional interface in this sink would make the dispatch site's <c>is IClipboardReplySink</c>
    /// check fail against this sink instead, silently downgrading every OSC 52 reply to the
    /// generic unsupported diagnostic regardless of what the destination actually supports.
    /// </remarks>
    public void Response(in Clipboard.ClipboardReply value) => _destination.Dispatch(in value);

    /// <inheritdoc/>
    /// <remarks>
    /// Kept for the same reason as <see cref="Response(in Clipboard.ClipboardReply)"/>, for the
    /// Kitty OSC 5522 clipboard family instead of OSC 52.
    /// </remarks>
    public void Response(Kitty.Clipboard.KittyClipboardPacket value) => _destination.Dispatch(value);

    /// <inheritdoc/>
    public void Sequence(ProtocolSequence value) => _destination.Sequence(value);
}
