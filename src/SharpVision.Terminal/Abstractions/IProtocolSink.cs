// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Abstractions;

using Xterm;

/// <summary>
/// Receives typed input, terminal replies, and owned extension strings. Vendor-specific
/// reply families (palette, metrics, status, capability, Kitty graphics and clipboard, iTerm2
/// capabilities, and OSC 52 clipboard) are not declared here; a sink opts into any of them by
/// additionally implementing the matching optional interface, for example
/// <see cref="IPaletteResponseSink"/>. A sink that implements only this interface still
/// observes every vendor reply, adapted through <see cref="Response(in XtermCapabilitiesResponse)"/> or
/// <see cref="IInputSink.Input(in Diagnostic)"/> by the dispatching decoder.
/// </summary>
[PublicAPI]
public interface IProtocolSink: IInputSink
{
    /// <summary>Receives one recognized immutable terminal response.</summary>
    /// <param name="value">The owned numeric response.</param>
    public void Response(in XtermCapabilitiesResponse value);

    /// <summary>Receives one completed owned terminal string.</summary>
    /// <param name="value">The non-null copied sequence.</param>
    public void Sequence(ProtocolSequence value);
}
