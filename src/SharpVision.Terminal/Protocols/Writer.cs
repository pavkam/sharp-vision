// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;

/// <summary>
/// Writes validated ECMA-48 sequence grammar directly to a byte buffer.
/// </summary>
/// <remarks>
/// Each call validates the complete command before requesting output memory and
/// advances the destination once. Text payload spans are borrowed only for the
/// duration of the synchronous call. String commands use the canonical ST
/// terminator, ESC followed by backslash.
/// </remarks>
/// <example>
/// <code>
/// var destination = new ArrayBufferWriter&lt;byte&gt;();
/// var writer = new Writer(destination);
/// writer.Csi("2"u8, [], (byte)'J');
/// </code>
/// </example>
public readonly struct Writer
{
    private const byte _escape = 0x1b;
    private readonly IBufferWriter<byte> _destination;

    /// <summary>
    /// Initializes a writer targeting <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">The synchronously consumed byte destination.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="destination"/> is <see langword="null"/>.
    /// </exception>
    public Writer(IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        _destination = destination;
    }

    /// <summary>
    /// Writes an ESC sequence.
    /// </summary>
    /// <param name="intermediates">Bytes in the range 0x20 through 0x2f.</param>
    /// <param name="final">A final byte in the range 0x30 through 0x7e.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="intermediates"/> contains a byte outside its grammar.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="final"/> is outside its grammar.
    /// </exception>
    public void Escape(ReadOnlySpan<byte> intermediates, byte final)
    {
        ValidateIntermediates(intermediates, nameof(intermediates));
        ValidateFinal(final, 0x30, nameof(final));

        var length = checked(intermediates.Length + 2);
        Span<byte> destination = _destination.GetSpan(length);
        Debug.Assert(destination.Length >= length, "IBufferWriter returned less than its size hint.");

        destination[0] = _escape;
        intermediates.CopyTo(destination[1..]);
        destination[length - 1] = final;

        _destination.Advance(length);
    }

    /// <summary>
    /// Writes a control sequence introducer, header, and final byte.
    /// </summary>
    /// <param name="parameters">Bytes in the range 0x30 through 0x3f.</param>
    /// <param name="intermediates">Bytes in the range 0x20 through 0x2f.</param>
    /// <param name="final">A final byte in the range 0x40 through 0x7e.</param>
    /// <exception cref="ArgumentException">
    /// A parameter or intermediate byte is outside its grammar.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="final"/> is outside its grammar.
    /// </exception>
    public void Csi(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final)
    {
        ValidateParameters(parameters, nameof(parameters));
        ValidateIntermediates(intermediates, nameof(intermediates));
        ValidateFinal(final, 0x40, nameof(final));

        var length = checked(parameters.Length + intermediates.Length + 3);
        Span<byte> destination = _destination.GetSpan(length);
        Debug.Assert(destination.Length >= length, "IBufferWriter returned less than its size hint.");

        destination[0] = _escape;
        destination[1] = (byte) '[';
        parameters.CopyTo(destination[2..]);
        intermediates.CopyTo(destination[(parameters.Length + 2)..]);
        destination[length - 1] = final;

        _destination.Advance(length);
    }

    /// <summary>
    /// Writes an operating system command with a decimal selector and ST.
    /// </summary>
    /// <param name="selector">The non-negative command selector.</param>
    /// <param name="payload">UTF-8 or ASCII payload without control bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="selector"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="payload"/> contains a control byte.
    /// </exception>
    public void Osc(int selector, ReadOnlySpan<byte> payload)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(selector);
        ValidatePayload(payload, nameof(payload));

        var selectorLength = CountDecimalBytes(selector);
        var length = checked(selectorLength + payload.Length + 5);
        Span<byte> destination = _destination.GetSpan(length);
        Debug.Assert(destination.Length >= length, "IBufferWriter returned less than its size hint.");

        destination[0] = _escape;
        destination[1] = (byte) ']';
        var formatted = Utf8Formatter.TryFormat(selector, destination[2..], out var written);
        Debug.Assert(formatted && written == selectorLength, "Validated selector did not format exactly.");
        destination[selectorLength + 2] = (byte) ';';
        payload.CopyTo(destination[(selectorLength + 3)..]);
        WriteTerminator(destination[(length - 2)..]);

        _destination.Advance(length);
    }

    /// <summary>
    /// Writes an APC, PM, or SOS string with ST.
    /// </summary>
    /// <param name="kind">The generic string sequence family.</param>
    /// <param name="payload">UTF-8 or ASCII payload without control bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="kind"/> is not APC, PM, or SOS.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="payload"/> contains a control byte.
    /// </exception>
    public void Command(SequenceKind kind, ReadOnlySpan<byte> payload)
    {
        var introducer = kind switch
        {
            SequenceKind.Apc => (byte) '_',
            SequenceKind.Pm => (byte) '^',
            SequenceKind.Sos => (byte) 'X',
            SequenceKind.None or
            SequenceKind.Escape or
            SequenceKind.Csi or
            SequenceKind.Osc or
            SequenceKind.Dcs => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Only APC, PM, and SOS are generic string families."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Only APC, PM, and SOS are generic string families."),
        };
        ValidatePayload(payload, nameof(payload));

        var length = checked(payload.Length + 4);
        Span<byte> destination = _destination.GetSpan(length);
        Debug.Assert(destination.Length >= length, "IBufferWriter returned less than its size hint.");

        destination[0] = _escape;
        destination[1] = introducer;
        payload.CopyTo(destination[2..]);
        WriteTerminator(destination[(length - 2)..]);

        _destination.Advance(length);
    }

    /// <summary>
    /// Writes a device control string header, payload, and ST.
    /// </summary>
    /// <param name="parameters">Bytes in the range 0x30 through 0x3f.</param>
    /// <param name="intermediates">Bytes in the range 0x20 through 0x2f.</param>
    /// <param name="final">A final byte in the range 0x40 through 0x7e.</param>
    /// <param name="payload">UTF-8 or ASCII payload without control bytes.</param>
    /// <exception cref="ArgumentException">
    /// A header or payload byte is outside its grammar.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="final"/> is outside its grammar.
    /// </exception>
    public void Dcs(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        ReadOnlySpan<byte> payload)
    {
        ValidateParameters(parameters, nameof(parameters));
        ValidateIntermediates(intermediates, nameof(intermediates));
        ValidateFinal(final, 0x40, nameof(final));
        ValidatePayload(payload, nameof(payload));

        var length = checked(parameters.Length + intermediates.Length + payload.Length + 5);
        Span<byte> destination = _destination.GetSpan(length);
        Debug.Assert(destination.Length >= length, "IBufferWriter returned less than its size hint.");

        destination[0] = _escape;
        destination[1] = (byte) 'P';
        parameters.CopyTo(destination[2..]);
        var intermediateStart = parameters.Length + 2;
        intermediates.CopyTo(destination[intermediateStart..]);
        var finalIndex = intermediateStart + intermediates.Length;
        destination[finalIndex] = final;
        payload.CopyTo(destination[(finalIndex + 1)..]);
        WriteTerminator(destination[(length - 2)..]);

        _destination.Advance(length);
    }

    private static int CountDecimalBytes(int value)
    {
        var count = 1;

        while (value >= 10)
        {
            value /= 10;
            count++;
        }

        return count;
    }

    private static void ValidateParameters(ReadOnlySpan<byte> value, string parameterName)
    {
        foreach (var item in value)
        {
            if (item is < 0x30 or > 0x3f)
            {
                throw new ArgumentException(
                    "A parameter byte is outside the range 0x30 through 0x3f.",
                    parameterName);
            }
        }
    }

    private static void ValidateIntermediates(ReadOnlySpan<byte> value, string parameterName)
    {
        foreach (var item in value)
        {
            if (item is < 0x20 or > 0x2f)
            {
                throw new ArgumentException(
                    "An intermediate byte is outside the range 0x20 through 0x2f.",
                    parameterName);
            }
        }
    }

    private static void ValidateFinal(byte value, byte minimum, string parameterName)
    {
        if (value < minimum || value > 0x7e)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"The final byte must be in the range 0x{minimum:x2} through 0x7e.");
        }
    }

    private static void ValidatePayload(ReadOnlySpan<byte> value, string parameterName)
    {
        foreach (var item in value)
        {
            if (item is < 0x20 or 0x7f)
            {
                throw new ArgumentException(
                    "A terminal string payload cannot contain control bytes.",
                    parameterName);
            }
        }
    }

    private static void WriteTerminator(Span<byte> destination)
    {
        Debug.Assert(destination.Length >= 2, "The terminator destination is too short.");

        destination[0] = _escape;
        destination[1] = (byte) '\\';
    }
}
