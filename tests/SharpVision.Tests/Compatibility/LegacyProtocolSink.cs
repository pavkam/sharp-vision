// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;


/// <summary>Models a consumer compiled against the original protocol-sink member set.</summary>
internal sealed class LegacyProtocolSink: IProtocolSink
{
    /// <summary>Gets the diagnostics received in callback order.</summary>
    internal List<Diagnostic> Diagnostics { get; } = [];

    /// <summary>Gets the legacy numeric responses received in callback order.</summary>
    internal List<XtermCapabilitiesResponse> Responses { get; } = [];

    /// <inheritdoc/>
    public void Input(in Stroke value) => _ = value;

    /// <inheritdoc/>
    public void Input(in TerminalText value) => _ = value;

    /// <inheritdoc/>
    public void Input(in Pointer value) => _ = value;

    /// <inheritdoc/>
    public void Input(Paste value) => _ = value;

    /// <inheritdoc/>
    public void Input(in TerminalFocus value) => _ = value;

    /// <inheritdoc/>
    public void Input(in Diagnostic value) => Diagnostics.Add(value);

    /// <inheritdoc/>
    public void Response(in XtermCapabilitiesResponse value) => Responses.Add(value);

    /// <inheritdoc/>
    public void Sequence(ProtocolSequence value) => ArgumentNullException.ThrowIfNull(value);
}
