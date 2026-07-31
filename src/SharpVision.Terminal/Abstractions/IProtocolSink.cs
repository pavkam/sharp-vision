// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Abstractions;

using Xterm;

/// <summary>Receives typed input, terminal replies, and owned extension strings.</summary>
[PublicAPI]
public interface IProtocolSink: IInputSink
{
    /// <summary>Receives one recognized immutable terminal response.</summary>
    /// <param name="value">The owned numeric response.</param>
    public void Response(in Response value);

    /// <summary>
    /// Receives one validated palette or dynamic-color response. The default
    /// implementation adapts the value through the numeric response callback:
    /// palette values are index, red, green, blue; dynamic colors are red,
    /// green, blue. Color components retain their normalized 16-bit values.
    /// </summary>
    /// <param name="value">The immutable color response.</param>
    public void Response(in PaletteResponse value)
    {
        var values = value.Kind == ResponseKind.PaletteColor
            ? new[] { value.Index.GetValueOrDefault(), value.Red, value.Green, value.Blue }
            : [value.Red, value.Green, value.Blue];
        var response = new Response(value.Kind, values);
        Response(in response);
    }

    /// <summary>
    /// Receives one validated pixel or cell metrics response. The default
    /// implementation adapts the value through the numeric response callback
    /// as width followed by height.
    /// </summary>
    /// <param name="value">The immutable metrics response.</param>
    public void Response(in MetricsResponse value)
    {
        int[] values = [value.Size.Width, value.Size.Height];
        var response = new Response(value.Kind, values);
        Response(in response);
    }

    /// <summary>
    /// Receives one validated bounded DECRQSS response. The default implementation
    /// reports a synthetic unsupported DCS diagnostic through <see cref="IInputSink.Input(in Diagnostic)"/>.
    /// The diagnostic has zero offset and discarded bytes because this compatibility
    /// callback receives no raw wire payload.
    /// </summary>
    /// <param name="value">The immutable owned status response.</param>
    public void Response(in StatusResponse value)
    {
        var diagnostic = new Diagnostic(
            DiagnosticCode.Unsupported,
            SequenceKind.Dcs,
            offset: 0,
            discardedBytes: 0);
        Input(in diagnostic);
    }

    /// <summary>
    /// Receives one validated bounded XTGETTCAP response. The default implementation
    /// reports a synthetic unsupported DCS diagnostic through <see cref="IInputSink.Input(in Diagnostic)"/>.
    /// The diagnostic has zero offset and discarded bytes because this compatibility
    /// callback receives no raw wire payload.
    /// </summary>
    /// <param name="value">The non-null immutable capability response.</param>
    public void Response(CapabilityResponse value)
    {
        var diagnostic = new Diagnostic(
            DiagnosticCode.Unsupported,
            SequenceKind.Dcs,
            offset: 0,
            discardedBytes: 0);
        Input(in diagnostic);
    }

    /// <summary>
    /// Receives one bounded Kitty graphics APC response. The default implementation
    /// reports a synthetic unsupported APC diagnostic without exposing terminal text.
    /// </summary>
    /// <param name="value">The non-null owned graphics response.</param>
    public void Response(Kitty.Graphics.Response value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var diagnostic = value.Diagnostic ?? new Diagnostic(
            DiagnosticCode.Unsupported,
            SequenceKind.Apc,
            offset: 0,
            discardedBytes: 0);
        Input(in diagnostic);
    }

    /// <summary>Receives one completed owned terminal string.</summary>
    /// <param name="value">The non-null copied sequence.</param>
    public void Sequence(ProtocolSequence value);
}
