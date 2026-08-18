// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Xterm;

/// <summary>Encodes and validates the finite DECRQSS status-query subset.</summary>
[PublicAPI]
public static class XtermDecrqss
{
    /// <summary>Writes one approved Request Status String command.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="name">The approved status name.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="name"/> is not queryable.</exception>
    public static void Query(ProtocolWriter writer, StatusName name) =>
        writer.Dcs([], "$"u8, (byte) 'q', Selector(name));

    /// <summary>Attempts to decode one DCS DECRQSS reply.</summary>
    /// <param name="parameters">Borrowed DCS parameter bytes.</param>
    /// <param name="intermediates">Borrowed DCS intermediate bytes.</param>
    /// <param name="final">The DCS final byte.</param>
    /// <param name="payload">The bounded borrowed reply payload.</param>
    /// <param name="response">Receives an owned response.</param>
    /// <returns>Whether the DCS belongs to DECRQSS and is structurally valid.</returns>
    public static bool TryParse(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        ReadOnlySpan<byte> payload,
        out StatusResponse response)
    {
        response = default;

        if (final != (byte) 'r' || !intermediates.SequenceEqual("$"u8) ||
            parameters.Length != 1 || parameters[0] is not (byte) '0' and not (byte) '1')
        {
            return false;
        }

        var valid = parameters[0] == (byte) '1';

        if (!valid && !payload.IsEmpty)
        {
            return false;
        }

        var name = StatusName.Unknown;

        if (valid && !TryIdentify(payload, out name))
        {
            return false;
        }

        response = valid
            ? new StatusResponse(name, true, payload)
            : new StatusResponse(StatusName.Unknown, false, []);
        return true;
    }

    private static ReadOnlySpan<byte> Selector(StatusName name) => name switch
    {
        StatusName.Rendition => "m"u8,
        StatusName.CursorStyle => " q"u8,
        StatusName.VerticalMargins => "r"u8,
        StatusName.HorizontalMargins => "s"u8,
        StatusName.ModifyOtherKeys => ">4m"u8,
        StatusName.FormatOtherKeys => ">4f"u8,
        StatusName.Unknown => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown status cannot be queried."),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "The status name is undefined.")
    };

    /// <summary>Validates one returned CSI body and identifies the approved subset.</summary>
    /// <param name="payload">The borrowed body without CSI.</param>
    /// <param name="name">Receives an approved name or unknown for another valid CSI body.</param>
    /// <returns>Whether the complete payload is structurally valid CSI grammar.</returns>
    internal static bool TryIdentify(ReadOnlySpan<byte> payload, out StatusName name)
    {
        name = StatusName.Unknown;

        if (!TryCsiBody(payload, out var parameters, out var intermediates, out var final))
        {
            return false;
        }

        if (intermediates.IsEmpty && final == (byte) 'm')
        {
            if (TryXtermKeyboard(parameters, maximumValue: 3))
            {
                name = StatusName.ModifyOtherKeys;
            }
            else if (IsSgr(parameters))
            {
                name = StatusName.Rendition;
            }

            return true;
        }

        if (intermediates.IsEmpty && final == (byte) 'f')
        {
            if (TryXtermKeyboard(parameters, maximumValue: 1))
            {
                name = StatusName.FormatOtherKeys;
            }

            return true;
        }

        if (intermediates.SequenceEqual(" "u8) && final == (byte) 'q')
        {
            if (TrySingleDigit(parameters, maximumValue: 6))
            {
                name = StatusName.CursorStyle;
            }

            return true;
        }

        if (intermediates.IsEmpty && final is (byte) 'r' or (byte) 's' &&
            TryMargins(parameters))
        {
            name = final == (byte) 'r'
                ? StatusName.VerticalMargins
                : StatusName.HorizontalMargins;
        }

        return true;
    }

    private static bool TryCsiBody(
        ReadOnlySpan<byte> payload,
        out ReadOnlySpan<byte> parameters,
        out ReadOnlySpan<byte> intermediates,
        out byte final)
    {
        parameters = [];
        intermediates = [];
        final = 0;

        if (payload.IsEmpty || payload[^1] is < 0x40 or > 0x7e)
        {
            return false;
        }

        final = payload[^1];
        var header = payload[..^1];
        var position = 0;

        while (position < header.Length && header[position] is >= 0x30 and <= 0x3f)
        {
            position++;
        }

        parameters = header[..position];
        var intermediateStart = position;

        while (position < header.Length && header[position] is >= 0x20 and <= 0x2f)
        {
            position++;
        }

        intermediates = header[intermediateStart..position];
        return position == header.Length;
    }

    private static bool IsSgr(ReadOnlySpan<byte> parameters)
    {
        foreach (var item in parameters)
        {
            if (item is not ((>= (byte) '0' and <= (byte) '9') or (byte) ';' or (byte) ':'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryXtermKeyboard(ReadOnlySpan<byte> parameters, int maximumValue) =>
        parameters.Length == 4 &&
        parameters[0] == (byte) '>' &&
        parameters[1] == (byte) '4' &&
        parameters[2] == (byte) ';' &&
        parameters[3] >= (byte) '0' &&
        parameters[3] <= (byte) ('0' + maximumValue);

    private static bool TrySingleDigit(ReadOnlySpan<byte> parameters, int maximumValue) =>
        parameters.Length == 1 &&
        parameters[0] >= (byte) '0' &&
        parameters[0] <= (byte) ('0' + maximumValue);

    private static bool TryMargins(ReadOnlySpan<byte> parameters)
    {
        var separator = parameters.IndexOf((byte) ';');
        return separator > 0 && separator < parameters.Length - 1 &&
               parameters[(separator + 1)..].IndexOf((byte) ';') < 0 &&
               TryPositive(parameters[..separator]) &&
               TryPositive(parameters[(separator + 1)..]);
    }

    private static bool TryPositive(ReadOnlySpan<byte> value)
    {
        var result = 0;

        foreach (var item in value)
        {
            if (item is < (byte) '0' or > (byte) '9' ||
                result > (int.MaxValue - (item - (byte) '0')) / 10)
            {
                return false;
            }

            result = (result * 10) + item - (byte) '0';
        }

        return result > 0;
    }
}
