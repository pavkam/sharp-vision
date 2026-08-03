// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using Capabilities;

using Input;

using Xterm;

using InputText = Input.Text;

/// <summary>Updates capability negotiation before forwarding ordered protocol events.</summary>
internal sealed class NegotiationSink:
    IProtocolSink,
    IPaletteResponseSink,
    IMetricsResponseSink,
    IStatusResponseSink,
    ICapabilityResponseSink,
    IKittyGraphicsResponseSink,
    IItermCapabilitiesResponseSink
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
    public void Input(in Focus value) => _destination.Input(in value);

    /// <inheritdoc/>
    public void Input(in Diagnostic value) => _destination.Input(in value);

    /// <inheritdoc/>
    public void Response(in Response value)
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
    public void Response(Kitty.Graphics.Response value)
    {
        _ = _negotiator.Accept(value);
        _destination.Dispatch(value);
    }

    /// <inheritdoc/>
    public void Sequence(ProtocolSequence value) => _destination.Sequence(value);
}
