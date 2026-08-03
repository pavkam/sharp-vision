// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Xterm;

/// <summary>
/// Decodes typed DA, DSR, DECRPM, and OSC color query responses.
/// </summary>
[PublicAPI]
public static class XtermResponses
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
            var reader = new Parameters(parameters, maxCount: 32, maxValue: int.MaxValue);
            var kind = reader.PrivateMarker switch
            {
                (byte) '?' => ResponseKind.PrimaryAttributes,
                (byte) '>' => ResponseKind.SecondaryAttributes,
                _ => ResponseKind.None
            };

            if (kind == ResponseKind.None || !TryReadValues(ref reader, minimum: 1, out var values))
            {
                return false;
            }

            response = new Response(kind, values);
            return true;
        }

        if (final == (byte) 'R' && intermediates.IsEmpty)
        {
            var reader = new Parameters(parameters, maxCount: 2, maxValue: int.MaxValue);

            if (reader.PrivateMarker != 0 ||
                !TryReadValues(ref reader, minimum: 2, out var values) ||
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
            var reader = new Parameters(parameters, maxCount: 2, maxValue: int.MaxValue);

            if (reader.PrivateMarker != (byte) '?' ||
                !TryReadValues(ref reader, minimum: 2, out var values) ||
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
            var reader = new Parameters(parameters, maxCount: 1, maxValue: 31);

            if (reader.PrivateMarker != (byte) '?' ||
                !TryReadValues(ref reader, minimum: 1, out var values) ||
                values.Length != 1)
            {
                return false;
            }

            response = new Response(ResponseKind.Keyboard, values);
            return true;
        }

        if (final == (byte) 'm' && intermediates.IsEmpty)
        {
            var reader = new Parameters(parameters, maxCount: 2, maxValue: 7);

            if (reader.PrivateMarker != (byte) '>' ||
                !TryReadValues(ref reader, minimum: 2, out var values) ||
                values.Length != 2 || values[0] != 4 || values[1] is < 0 or > 3)
            {
                return false;
            }

            response = new Response(ResponseKind.ModifyOtherKeys, values);
            return true;
        }

        return false;
    }

    /// <summary>Attempts to decode one OSC 4, OSC 10, or OSC 11 RGB reply.</summary>
    /// <param name="value">Borrowed raw OSC callback bytes.</param>
    /// <param name="response">Receives the typed response on success.</param>
    /// <returns>Whether the callback is a valid supported color response.</returns>
    public static bool TryOsc(ReadOnlySpan<byte> value, out PaletteResponse response)
    {
        response = default;
        ResponseKind kind;
        var paletteIndex = -1;

        if (value.StartsWith("4;"u8))
        {
            kind = ResponseKind.PaletteColor;
            value = value[2..];
            var separator = value.IndexOf((byte) ';');

            if (separator <= 0 ||
                !TryDecimal(value[..separator], byte.MaxValue, out paletteIndex) ||
                !value[(separator + 1)..].StartsWith("rgb:"u8))
            {
                return false;
            }

            value = value[(separator + 5)..];
        }
        else if (value.StartsWith("10;rgb:"u8))
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

        Span<ushort> values = stackalloc ushort[3];

        for (var component = 0; component < values.Length; component++)
        {
            var separator = value.IndexOf((byte) '/');
            var field = separator < 0 ? value : value[..separator];

            if (!TryHex(field, out values[component]) ||
                (component < values.Length - 1 && separator < 0) ||
                (component == values.Length - 1 && separator >= 0))
            {
                return false;
            }

            value = separator < 0 ? [] : value[(separator + 1)..];
        }

        response = new PaletteResponse(
            kind,
            kind == ResponseKind.PaletteColor ? paletteIndex : null,
            values[0],
            values[1],
            values[2]);
        return true;
    }

    /// <summary>Attempts to decode one iTerm2 OSC 1337 Capabilities feature-reporting reply.</summary>
    /// <param name="value">Borrowed raw OSC callback bytes.</param>
    /// <param name="response">Receives the typed response on success.</param>
    /// <returns>Whether the callback is a recognized Capabilities reply.</returns>
    /// <remarks>
    /// The feature string concatenates codes with no separator: each code is one or more
    /// uppercase-led letters optionally followed by a decimal value (for example <c>"U9"</c> for
    /// UNICODE_WIDTHS=9, or bare <c>"M"</c> for boolean MOUSE). This only tokenizes the string to
    /// test for the bare "F" code; it does not attempt to decode every documented feature.
    /// </remarks>
    public static bool TryOscItermCapabilities(ReadOnlySpan<byte> value, out ItermCapabilitiesResponse response)
    {
        response = null!;

        if (!value.StartsWith("1337;Capabilities="u8))
        {
            return false;
        }

        var featureString = value["1337;Capabilities="u8.Length..];
        response = new ItermCapabilitiesResponse(ContainsBareCode(featureString, (byte) 'F'));
        return true;
    }

    private static bool ContainsBareCode(ReadOnlySpan<byte> value, byte code)
    {
        var index = 0;

        while (index < value.Length)
        {
            var current = value[index];

            if (current is < (byte) 'A' or > (byte) 'Z')
            {
                break;
            }

            var nameStart = index++;

            while (index < value.Length && value[index] is >= (byte) 'a' and <= (byte) 'z')
            {
                index++;
            }

            var nameEnd = index;

            while (index < value.Length && value[index] is >= (byte) '0' and <= (byte) '9')
            {
                index++;
            }

            if (nameEnd - nameStart == 1 && value[nameStart] == code)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Attempts to decode an XTWINOPS pixel or cell-size report.</summary>
    /// <param name="parameters">Borrowed raw parameter bytes.</param>
    /// <param name="intermediates">Borrowed intermediate bytes.</param>
    /// <param name="final">The CSI final byte.</param>
    /// <param name="response">Receives the typed response on success.</param>
    /// <returns>Whether the callback is a valid bounded geometry response.</returns>
    public static bool TryMetricsCsi(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        out MetricsResponse response)
    {
        response = default;

        if (final != (byte) 't' || !intermediates.IsEmpty)
        {
            return false;
        }

        var reader = new Parameters(parameters, maxCount: 3, maxValue: ushort.MaxValue);

        if (reader.PrivateMarker != 0 ||
            !TryReadValues(ref reader, minimum: 3, out var values) ||
            values.Length != 3 ||
            values[1] <= 0 ||
            values[2] <= 0)
        {
            return false;
        }

        var kind = values[0] switch
        {
            4 => ResponseKind.WindowPixels,
            6 => ResponseKind.CellPixels,
            8 => ResponseKind.WindowCells,
            _ => ResponseKind.None
        };

        if (kind == ResponseKind.None)
        {
            return false;
        }

        response = new MetricsResponse(kind, new Size(values[2], values[1]));
        return true;
    }

    private static bool TryHex(ReadOnlySpan<byte> field, out ushort value)
    {
        value = 0;

        if (field.IsEmpty || field.Length > 4)
        {
            return false;
        }

        foreach (var item in field)
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

            value = (ushort) ((value * 16) + digit);
        }

        var maximum = (1u << (field.Length * 4)) - 1;
        value = (ushort) ((uint) value * ushort.MaxValue / maximum);
        return true;
    }

    private static bool TryDecimal(ReadOnlySpan<byte> field, int maximum, out int value)
    {
        value = 0;

        if (field.IsEmpty)
        {
            return false;
        }

        foreach (var item in field)
        {
            if (item is < (byte) '0' or > (byte) '9')
            {
                value = 0;
                return false;
            }

            var digit = item - (byte) '0';

            if (value > maximum / 10 ||
                (value == maximum / 10 && digit > maximum % 10))
            {
                value = 0;
                return false;
            }

            value = (value * 10) + digit;
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
            var status = reader.Read(out var value, out var separator);

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
