// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

using System.Buffers;
using System.Diagnostics;

/// <summary>Writes one terminal sequence through tmux DCS passthrough framing.</summary>
/// <remarks>
/// The caller supplies an already validated terminal sequence. tmux requires
/// every embedded ESC byte to be doubled before the outer DCS ST terminator.
/// </remarks>
public static class Tmux
{
    private static ReadOnlySpan<byte> Header => "\u001bPtmux;"u8;
    private static ReadOnlySpan<byte> Prefix => "tmux;"u8;
    private static ReadOnlySpan<byte> Terminator => "\u001b\\"u8;

    /// <summary>Writes one borrowed terminal sequence through one tmux passthrough envelope.</summary>
    /// <param name="destination">The non-null synchronously consumed byte destination.</param>
    /// <param name="sequence">The complete already-validated sequence for the outer terminal.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="OverflowException">The encoded DCS envelope exceeds the supported span length.</exception>
    public static void WritePassthrough(IBufferWriter<byte> destination, ReadOnlySpan<byte> sequence)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var escapeCount = 0;

        foreach (var value in sequence)
        {
            if (value == 0x1b)
            {
                escapeCount++;
            }
        }

        var length = checked(Header.Length + sequence.Length + escapeCount + Terminator.Length);
        Span<byte> output = destination.GetSpan(length);
        Debug.Assert(output.Length >= length, "IBufferWriter returned less than its requested span.");
        var written = 0;
        Header.CopyTo(output);
        written += Header.Length;

        foreach (var value in sequence)
        {
            output[written++] = value;

            if (value == 0x1b)
            {
                output[written++] = value;
            }
        }

        Terminator.CopyTo(output[written..]);
        destination.Advance(length);
    }

    /// <summary>Unwraps one completed tmux DCS payload into a caller-owned byte destination.</summary>
    /// <param name="payload">The bounded DCS payload delivered by the terminal parser.</param>
    /// <param name="destination">The non-null synchronously consumed byte destination.</param>
    /// <returns>True when <paramref name="payload"/> has a valid tmux prefix and ESC doubling.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    public static bool TryUnwrap(ReadOnlySpan<byte> payload, IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (!payload.StartsWith(Prefix))
        {
            return false;
        }

        ReadOnlySpan<byte> source = payload[Prefix.Length..];
        var length = 0;

        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] != 0x1b)
            {
                length++;
                continue;
            }

            if (++index >= source.Length || source[index] != 0x1b)
            {
                return false;
            }

            length++;
        }

        Span<byte> output = destination.GetSpan(length);
        Debug.Assert(output.Length >= length, "IBufferWriter returned less than its requested span.");
        var written = 0;

        for (var index = 0; index < source.Length; index++)
        {
            output[written++] = source[index];

            if (source[index] == 0x1b)
            {
                index++;
            }
        }

        destination.Advance(length);
        return true;
    }
}
