namespace SharpVision.Terminal.Protocols;

/// <summary>
/// Identifies a typed terminal query response.
/// </summary>
public enum ResponseKind
{
    /// <summary>No recognized response.</summary>
    None,

    /// <summary>Primary device attributes (DA1).</summary>
    PrimaryAttributes,

    /// <summary>Secondary device attributes (DA2).</summary>
    SecondaryAttributes,

    /// <summary>A one-based cursor position report.</summary>
    CursorPosition,

    /// <summary>A DEC private mode report (DECRPM).</summary>
    PrivateMode,

    /// <summary>An OSC 10 default foreground color reply.</summary>
    ForegroundColor,

    /// <summary>An OSC 11 default background color reply.</summary>
    BackgroundColor,

    /// <summary>Current Kitty progressive keyboard flags.</summary>
    Keyboard,
}

/// <summary>
/// Contains owned typed values from one terminal query response.
/// </summary>
public readonly record struct Response
{
    /// <summary>Initializes one recognized response.</summary>
    /// <param name="kind">The response family.</param>
    /// <param name="values">Owned numeric response values.</param>
    /// <param name="isSupported">Whether a mode report proves support.</param>
    internal Response(ResponseKind kind, int[] values, bool isSupported = false)
    {
        Kind = kind;
        Values = values;
        IsSupported = isSupported;
    }

    /// <summary>Gets the response family.</summary>
    public ResponseKind Kind { get; }

    /// <summary>Gets owned response values in wire order.</summary>
    public ReadOnlyMemory<int> Values { get; }

    /// <summary>Gets whether a private mode state 1 or 2 proves support.</summary>
    public bool IsSupported { get; }
}

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
            var reader = new Parameters(parameters, maxCount: 32, maxValue: int.MaxValue);
            var kind = reader.PrivateMarker switch
            {
                (byte) '?' => ResponseKind.PrimaryAttributes,
                (byte) '>' => ResponseKind.SecondaryAttributes,
                _ => ResponseKind.None,
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

        var values = new int[3];

        for (var index = 0; index < values.Length; index++)
        {
            var separator = value.IndexOf((byte) '/');
            var field = separator < 0 ? value : value[..separator];

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

            value = (value * 16) + digit;
        }

        return true;
    }

    private static bool TryReadValues(
        ref Parameters reader,
        int minimum,
        out int[] values)
    {
        var collected = new List<int>();

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
