// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Xterm;

using SharpVision.Terminal.Capabilities;

/// <summary>Encodes and validates the finite xterm XTGETTCAP boundary.</summary>
[PublicAPI]
public static class XtermGetCap
{
    /// <summary>Writes one bounded request for approved terminfo capability names.</summary>
    /// <param name="writer">The validated protocol writer.</param>
    /// <param name="names">One or more approved names.</param>
    /// <exception cref="ArgumentException">The list is empty or contains a duplicate.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A name is undefined.</exception>
    public static void Query(Writer writer, ReadOnlySpan<CapabilityName> names)
    {
        if (names.IsEmpty)
        {
            throw new ArgumentException("At least one capability name is required.", nameof(names));
        }

        var seen = new HashSet<CapabilityName>();
        var destination = new ArrayBufferWriter<byte>();

        foreach (var name in names)
        {
            if (!Enum.IsDefined(name))
            {
                throw new ArgumentOutOfRangeException(nameof(names), name, "A capability name is undefined.");
            }

            if (!seen.Add(name))
            {
                throw new ArgumentException("Capability names must be unique.", nameof(names));
            }

            if (destination.WrittenCount > 0)
            {
                WriteByte(destination, (byte) ';');
            }

            WriteHex(destination, WireName(name));
        }

        writer.Dcs([], "+"u8, (byte) 'q', destination.WrittenSpan);
    }

    /// <summary>Attempts to parse one strict bounded XTGETTCAP response.</summary>
    /// <param name="parameters">Borrowed DCS parameter bytes.</param>
    /// <param name="intermediates">Borrowed DCS intermediate bytes.</param>
    /// <param name="final">The DCS final byte.</param>
    /// <param name="payload">The borrowed bounded hex payload.</param>
    /// <param name="limits">Finite parsing limits, or null for defaults.</param>
    /// <param name="response">Receives the owned approved response.</param>
    /// <returns>Whether the DCS belongs to XTGETTCAP and is strictly valid.</returns>
    public static bool TryParse(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        ReadOnlySpan<byte> payload,
        QueryLimits? limits,
        out CapabilityResponse? response)
    {
        response = null;
        var policy = limits ?? QueryLimits.Default;

        if (final != (byte) 'r' || !intermediates.SequenceEqual("+"u8) ||
            parameters.Length != 1 || parameters[0] is not (byte) '0' and not (byte) '1')
        {
            return false;
        }

        var valid = parameters[0] == (byte) '1';
        var items = new Dictionary<CapabilityName, byte[]>();

        if (!valid)
        {
            if (!payload.IsEmpty)
            {
                return false;
            }

            response = new CapabilityResponse(false, items);
            return true;
        }

        if (payload.IsEmpty)
        {
            return false;
        }

        while (!payload.IsEmpty)
        {
            if (items.Count == policy.MaxCapabilityItems)
            {
                return false;
            }

            var separator = payload.IndexOf((byte) ';');
            var item = separator < 0 ? payload : payload[..separator];
            var equals = item.IndexOf((byte) '=');

            if (equals <= 0 || item[(equals + 1)..].Length > checked(policy.MaxCapabilityValueBytes * 2) ||
                !TryDecodeName(item[..equals], out var name) || items.ContainsKey(name) ||
                !TryDecode(item[(equals + 1)..], policy.MaxCapabilityValueBytes, out var value))
            {
                return false;
            }

            items.Add(name, value);
            payload = separator < 0 ? [] : payload[(separator + 1)..];

            if (separator >= 0 && payload.IsEmpty)
            {
                return false;
            }
        }

        response = new CapabilityResponse(true, items);
        return true;
    }

    private static void WriteHex(ArrayBufferWriter<byte> destination, ReadOnlySpan<byte> value)
    {
        const string digits = "0123456789ABCDEF";
        var output = destination.GetSpan(checked(value.Length * 2));
        var position = 0;

        foreach (var item in value)
        {
            output[position++] = (byte) digits[item >> 4];
            output[position++] = (byte) digits[item & 0x0f];
        }

        destination.Advance(position);
    }

    private static void WriteByte(ArrayBufferWriter<byte> destination, byte value)
    {
        var output = destination.GetSpan(1);
        output[0] = value;
        destination.Advance(1);
    }

    private static bool TryDecodeName(ReadOnlySpan<byte> hex, out CapabilityName name)
    {
        name = default;

        foreach (var candidate in Enum.GetValues<CapabilityName>())
        {
            if (MatchesHex(hex, WireName(candidate)))
            {
                name = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool MatchesHex(ReadOnlySpan<byte> hex, ReadOnlySpan<byte> value)
    {
        if (hex.Length != value.Length * 2)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (!TryHex(hex[index * 2], out var high) ||
                !TryHex(hex[(index * 2) + 1], out var low) ||
                (byte) ((high << 4) | low) != value[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryDecode(ReadOnlySpan<byte> hex, int maximum, out byte[] value)
    {
        value = [];

        if ((hex.Length & 1) != 0 || hex.Length / 2 > maximum)
        {
            return false;
        }

        value = new byte[hex.Length / 2];

        for (var index = 0; index < value.Length; index++)
        {
            if (!TryHex(hex[index * 2], out var high) ||
                !TryHex(hex[(index * 2) + 1], out var low))
            {
                value = [];
                return false;
            }

            value[index] = (byte) ((high << 4) | low);
        }

        return true;
    }

    private static bool TryHex(byte value, out int digit)
    {
        if (value is >= (byte) '0' and <= (byte) '9')
        {
            digit = value - (byte) '0';
            return true;
        }

        if (value is >= (byte) 'A' and <= (byte) 'F')
        {
            digit = value - (byte) 'A' + 10;
            return true;
        }

        if (value is >= (byte) 'a' and <= (byte) 'f')
        {
            digit = value - (byte) 'a' + 10;
            return true;
        }

        digit = 0;
        return false;
    }

    private static ReadOnlySpan<byte> WireName(CapabilityName name) => name switch
    {
        CapabilityName.Colors => "Co"u8,
        CapabilityName.TerminalName => "TN"u8,
        CapabilityName.DirectColor => "RGB"u8,
        CapabilityName.Backspace => "kbs"u8,
        CapabilityName.Enter => "kent"u8,
        CapabilityName.Up => "kcuu1"u8,
        CapabilityName.Down => "kcud1"u8,
        CapabilityName.Left => "kcub1"u8,
        CapabilityName.Right => "kcuf1"u8,
        CapabilityName.Home => "khome"u8,
        CapabilityName.End => "kend"u8,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "The capability name is undefined.")
    };
}
