// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Multiplexing;

using Input;

using Protocols;

/// <summary>Validates that an unwrapped envelope contains exactly one recognized typed reply.</summary>
internal sealed class ReplyValidationSink: IProtocolSink
{
    private int _responses;
    private bool Invalid { get; set; }

    /// <summary>Gets whether decoding produced exactly one typed reply and no other event.</summary>
    public bool IsValid => !Invalid && _responses == 1;

    /// <inheritdoc/>
    public void Input(in Stroke value) => Invalid = true;

    /// <inheritdoc/>
    public void Input(in Text value) => Invalid = true;

    /// <inheritdoc/>
    public void Input(in Pointer value) => Invalid = true;

    /// <inheritdoc/>
    public void Input(Paste value) => Invalid = true;

    /// <inheritdoc/>
    public void Input(in Focus value) => Invalid = true;

    /// <inheritdoc/>
    public void Input(in Diagnostic value) => Invalid = true;

    /// <inheritdoc/>
    public void Response(in Response value)
    {
        if (value.Kind is ResponseKind.PrimaryAttributes or
            ResponseKind.SecondaryAttributes or
            ResponseKind.PrivateMode or
            ResponseKind.Keyboard)
        {
            _responses++;
        }
        else
        {
            Invalid = true;
        }
    }

    /// <inheritdoc/>
    public void Response(in PaletteResponse value) => _responses++;

    /// <inheritdoc/>
    public void Response(in MetricsResponse value) => _responses++;

    /// <inheritdoc/>
    public void Response(in StatusResponse value) => _responses++;

    /// <inheritdoc/>
    public void Response(CapabilityResponse value) => _responses++;

    /// <inheritdoc/>
    public void Response(Kitty.Response value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.IsValid)
        {
            _responses++;
        }
        else
        {
            Invalid = true;
        }
    }

    /// <inheritdoc/>
    public void Sequence(ProtocolSequence value) => Invalid = true;
}
