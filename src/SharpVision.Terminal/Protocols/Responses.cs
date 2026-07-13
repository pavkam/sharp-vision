// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>
/// Decodes typed DA, DSR, DECRPM, and OSC color query responses.
/// </summary>
public static class Responses
{
    /// <summary>Attempts to decode one raw CSI parser callback.</summary>
    /// <param name="parameters">Borrowed raw parameter bytes.</param>
    /// <param name="intermediates">Borrowed intermediate bytes.</param>
    /// <param name="final">The CSI final byte.</param>
    /// <param name="response">Receives the typed response on success.</param>
    /// <returns>Whether the callback is a valid supported response.</returns>
    public static bool TryCsi(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        out Response response)
    {
        response = default;

        if (final == (byte) 'c' && intermediates.IsEmpty)
        {
            Parameters reader = new(parameters, maxCount: 32, maxValue: int.MaxValue);
            ResponseKind kind = reader.PrivateMarker switch
            {
                (byte) '?' => ResponseKind.PrimaryAttributes,
                (byte) '>' => ResponseKind.SecondaryAttributes,
                _ => ResponseKind.None,
            };

            if (kind == ResponseKind.None || !TryReadValues(ref reader, minimum: 1, out int[]? values))
            {
                return false;
            }

            response = new Response(kind, values);
            return true;
        }

        if (final == (byte) 'R' && intermediates.IsEmpty)
        {
            Parameters reader = new(parameters, maxCount: 2, maxValue: int.MaxValue);

            if (reader.PrivateMarker != 0 ||
                !TryReadValues(ref reader, minimum: 2, out int[]? values) ||
                values.Length != 2 ||
                values[0] <= 0 ||
                values[1] <= 0)
            {
                return false;
            }

            response = new Response(ResponseKind.CursorPosition, values);
            return true;
        }

        if (final == (byte) 'y' && intermediates.SequenceEqual("$"u8))
        {
            Parameters reader = new(parameters, maxCount: 2, maxValue: int.MaxValue);

            if (reader.PrivateMarker != (byte) '?' ||
                !TryReadValues(ref reader, minimum: 2, out int[]? values) ||
                values.Length != 2 ||
                values[0] <= 0 ||
                values[1] is < 0 or > 4)
            {
                return false;
            }

            response = new Response(
                ResponseKind.PrivateMode,
                values,
                isSupported: values[1] is 1 or 2);
            return true;
        }

        if (final == (byte) 'u' && intermediates.IsEmpty)
        {
            Parameters reader = new(parameters, maxCount: 1, maxValue: 31);

            if (reader.PrivateMarker != (byte) '?' ||
                !TryReadValues(ref reader, minimum: 1, out int[]? values) ||
                values.Length != 1)
            {
                return false;
            }

            response = new Response(ResponseKind.Keyboard, values);
            return true;
        }

        return false;
    }

    /// <summary>Attempts to decode an OSC 10 or OSC 11 RGB reply.</summary>
    /// <param name="value">Borrowed raw OSC callback bytes.</param>
    /// <param name="response">Receives the typed response on success.</param>
    /// <returns>Whether the callback is a valid supported color response.</returns>
    public static bool TryOsc(ReadOnlySpan<byte> value, out Response response)
    {
        response = default;
        ResponseKind kind;

        if (value.StartsWith("10;rgb:"u8))
        {
            kind = ResponseKind.ForegroundColor;
            value = value[7..];
        }
        else if (value.StartsWith("11;rgb:"u8))
        {
            kind = ResponseKind.BackgroundColor;
            value = value[7..];
        }
        else
        {
            return false;
        }

        int[] values = new int[3];

        for (int index = 0; index < values.Length; index++)
        {
            int separator = value.IndexOf((byte) '/');
            ReadOnlySpan<byte> field = separator < 0 ? value : value[..separator];

            if (!TryHex(field, out values[index]) ||
                (index < values.Length - 1 && separator < 0) ||
                (index == values.Length - 1 && separator >= 0))
            {
                return false;
            }

            value = separator < 0 ? [] : value[(separator + 1)..];
        }

        response = new Response(kind, values);
        return true;
    }

    private static bool TryHex(ReadOnlySpan<byte> field, out int value)
    {
        value = 0;

        if (field.IsEmpty || field.Length > 4)
        {
            return false;
        }

        foreach (byte item in field)
        {
            int digit;

            if (item is >= (byte) '0' and <= (byte) '9')
            {
                digit = item - (byte) '0';
            }
            else if (item is >= (byte) 'a' and <= (byte) 'f')
            {
                digit = item - (byte) 'a' + 10;
            }
            else if (item is >= (byte) 'A' and <= (byte) 'F')
            {
                digit = item - (byte) 'A' + 10;
            }
            else
            {
                return false;
            }

            value = (value * 16) + digit;
        }

        return true;
    }

    private static bool TryReadValues(
        ref Parameters reader,
        int minimum,
        out int[] values)
    {
        List<int> collected = [];

        while (true)
        {
            ParameterStatus status = reader.Read(out int value, out ParameterSeparator separator);

            if (status == ParameterStatus.End)
            {
                break;
            }

            if (status != ParameterStatus.Value || separator == ParameterSeparator.Colon)
            {
                values = [];
                return false;
            }

            collected.Add(value);
        }

        values = [.. collected];
        return values.Length >= minimum;
    }
}
