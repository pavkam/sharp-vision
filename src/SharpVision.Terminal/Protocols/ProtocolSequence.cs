// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>Owns one completed OSC, DCS, APC, PM, or SOS sequence.</summary>
[PublicAPI]
public sealed class ProtocolSequence
{
    /// <summary>Initializes an owned copy after parser validation.</summary>
    /// <param name="kind">The terminal string family.</param>
    /// <param name="parameters">DCS parameters, otherwise empty.</param>
    /// <param name="intermediates">DCS intermediates, otherwise empty.</param>
    /// <param name="final">The DCS final, otherwise zero.</param>
    /// <param name="payload">The bounded payload.</param>
    /// <param name="terminator">The observed terminator.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="kind"/> is not a terminal string family.
    /// </exception>
    internal ProtocolSequence(
        SequenceKind kind,
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        ReadOnlySpan<byte> payload,
        StringTerminator terminator)
    {
        if (kind is not SequenceKind.Osc and not SequenceKind.Dcs and
            not SequenceKind.Apc and not SequenceKind.Pm and not SequenceKind.Sos)
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "The value is not a terminal string family.");
        }

        Kind = kind;
        Parameters = parameters.ToArray();
        Intermediates = intermediates.ToArray();
        Final = final;
        Payload = payload.ToArray();
        Terminator = terminator;
    }

    /// <summary>Gets the terminal string family.</summary>
    public SequenceKind Kind { get; }

    /// <summary>Gets owned DCS parameter bytes, or empty memory.</summary>
    public ReadOnlyMemory<byte> Parameters { get; }

    /// <summary>Gets owned DCS intermediate bytes, or empty memory.</summary>
    public ReadOnlyMemory<byte> Intermediates { get; }

    /// <summary>Gets the DCS final byte, or zero for another string family.</summary>
    public byte Final { get; }

    /// <summary>Gets the owned bounded payload.</summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>Gets the observed terminator.</summary>
    public StringTerminator Terminator { get; }
}
