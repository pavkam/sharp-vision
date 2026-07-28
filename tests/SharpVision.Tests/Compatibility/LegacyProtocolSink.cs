// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;

using InputText = Terminal.Input.Text;

/// <summary>Models a consumer compiled against the original protocol-sink member set.</summary>
internal sealed class LegacyProtocolSink: IProtocolSink
{
    /// <summary>Gets the diagnostics received in callback order.</summary>
    internal List<Diagnostic> Diagnostics { get; } = [];

    /// <summary>Gets the legacy numeric responses received in callback order.</summary>
    internal List<Response> Responses { get; } = [];

    /// <inheritdoc/>
    public void Input(in Stroke value) => _ = value;

    /// <inheritdoc/>
    public void Input(in InputText value) => _ = value;

    /// <inheritdoc/>
    public void Input(in Pointer value) => _ = value;

    /// <inheritdoc/>
    public void Input(Paste value) => _ = value;

    /// <inheritdoc/>
    public void Input(in Focus value) => _ = value;

    /// <inheritdoc/>
    public void Input(in Diagnostic value) => Diagnostics.Add(value);

    /// <inheritdoc/>
    public void Response(in Response value) => Responses.Add(value);

    /// <inheritdoc/>
    public void Sequence(ProtocolSequence value) => ArgumentNullException.ThrowIfNull(value);
}
