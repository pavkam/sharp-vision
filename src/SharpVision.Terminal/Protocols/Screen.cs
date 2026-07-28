// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>Writes one terminal sequence through GNU screen DCS passthrough framing.</summary>
/// <remarks>
/// GNU screen forwards a DCS payload to its host terminal without interpreting
/// it. The caller supplies an already validated outer-terminal sequence.
/// </remarks>
[PublicAPI]
public static class Screen
{
    private static ReadOnlySpan<byte> Header => "\u001bP"u8;
    private static ReadOnlySpan<byte> Terminator => "\u001b\\"u8;

    /// <summary>Writes one borrowed terminal sequence through one GNU screen DCS envelope.</summary>
    /// <param name="destination">The non-null synchronously consumed byte destination.</param>
    /// <param name="sequence">The complete already-validated sequence for the host terminal.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="OverflowException">The encoded DCS envelope exceeds the supported span length.</exception>
    public static void WritePassthrough(IBufferWriter<byte> destination, ReadOnlySpan<byte> sequence)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var length = checked(Header.Length + sequence.Length + Terminator.Length);
        var output = destination.GetSpan(length);
        Debug.Assert(output.Length >= length, "IBufferWriter returned less than its requested span.");
        Header.CopyTo(output);
        sequence.CopyTo(output[Header.Length..]);
        Terminator.CopyTo(output[(Header.Length + sequence.Length)..]);
        destination.Advance(length);
    }

    /// <summary>Unwraps one complete GNU screen DCS relay envelope.</summary>
    /// <param name="envelope">The complete borrowed outer envelope.</param>
    /// <param name="destination">The non-null synchronously consumed byte destination.</param>
    /// <returns>True when the envelope has the exact DCS and ST boundaries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    public static bool TryUnwrap(
        ReadOnlySpan<byte> envelope,
        IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (!envelope.StartsWith(Header) || !envelope.EndsWith(Terminator))
        {
            return false;
        }

        var payload = envelope[Header.Length..^Terminator.Length];
        var output = destination.GetSpan(payload.Length);
        payload.CopyTo(output);
        destination.Advance(payload.Length);
        return true;
    }
}
