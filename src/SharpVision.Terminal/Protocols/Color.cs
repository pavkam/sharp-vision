// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>
/// A validated color value: default, transparent, or 24-bit RGB.
/// </summary>
[DebuggerDisplay("{ToString()}")]
[PublicAPI]
public readonly record struct Color
{
    private const int _transparent = -1;
    private const int _rgbOffset = 1;
    private const int _maximumRgb = 0x00ff_ffff + _rgbOffset;
    private readonly int _value;

    private Color(int value) => _value = value;

    /// <summary>The terminal's own default color.</summary>
    public static Color Default => default;

    /// <summary>Preserves the destination cell during composition.</summary>
    public static Color Transparent { get; } = new(_transparent);

    #region Factories

    /// <summary>Creates a 24-bit RGB color.</summary>
    /// <param name="red">The red component from 0 through 255.</param>
    /// <param name="green">The green component from 0 through 255.</param>
    /// <param name="blue">The blue component from 0 through 255.</param>
    /// <returns>The validated RGB color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A component is outside 0 through 255.</exception>
    [Pure]
    public static Color Rgb(
        [ValueRange(0, byte.MaxValue)] int red,
        [ValueRange(0, byte.MaxValue)] int green,
        [ValueRange(0, byte.MaxValue)] int blue)
    {
        ValidateComponent(red, nameof(red));
        ValidateComponent(green, nameof(green));
        ValidateComponent(blue, nameof(blue));

        return new Color(((red << 16) | (green << 8) | blue) + _rgbOffset);
    }

    /// <summary>Parses a hex RGB color string (<c>#rgb</c> or <c>#rrggbb</c>, case-insensitive, leading <c>#</c> optional).</summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="FormatException">The string is not a 3- or 6-digit hex color.</exception>
    [Pure]
    public static Color FromHex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return TryFromHex(value, out var color)
            ? color
            : throw new FormatException($"'{value}' is not a valid #rgb or #rrggbb color.");
    }

    /// <summary>Attempts to parse a hex RGB color string without throwing.</summary>
    public static bool TryFromHex(string value, out Color color)
    {
        color = Default;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var digits = value[0] == '#' ? value.AsSpan(1) : value.AsSpan();

        if (digits.Length == 3)
        {
            if (!TryNibble(digits[0], out var r) || !TryNibble(digits[1], out var g) ||
                !TryNibble(digits[2], out var b))
            {
                return false;
            }

            color = Rgb((r << 4) | r, (g << 4) | g, (b << 4) | b);
            return true;
        }

        if (digits.Length == 6)
        {
            if (!TryByte(digits[..2], out var r) || !TryByte(digits[2..4], out var g) ||
                !TryByte(digits[4..], out var b))
            {
                return false;
            }

            color = Rgb(r, g, b);
            return true;
        }

        return false;
    }

    /// <summary>Attempts to parse a named ANSI or status color without throwing.</summary>
    /// <param name="value">The case-insensitive color name.</param>
    /// <param name="color">The parsed RGB color, or <see cref="Default"/> when parsing fails.</param>
    /// <returns>Whether <paramref name="value"/> is a supported named color.</returns>
    public static bool TryFromName(string? value, out Color color)
    {
        color = Default;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var key = value.ToLowerInvariant();
        var ansi = key switch
        {
            "black" => 0,
            "red" => 1,
            "green" => 2,
            "yellow" => 3,
            "blue" => 4,
            "magenta" => 5,
            "cyan" => 6,
            "white" => 7,
            "brightblack" or "gray" or "grey" => 8,
            "brightred" => 9,
            "brightgreen" => 10,
            "brightyellow" => 11,
            "brightblue" => 12,
            "brightmagenta" => 13,
            "brightcyan" => 14,
            "brightwhite" => 15,
            "error" => 9,
            "warning" or "hotkey" => 11,
            "success" => 10,
            "info" or "accent" => 14,
            "muted" => 8,
            _ => -1
        };

        if (ansi < 0)
        {
            return false;
        }

        color = FromAnsi(ansi);
        return true;
    }

    #endregion

    #region Queries

    /// <summary>Whether this is the terminal default color.</summary>
    public bool IsDefault => _value == 0;

    /// <summary>Whether this preserves the destination cell during composition.</summary>
    public bool IsTransparent => _value == _transparent;

    /// <summary>Whether this carries a concrete 24-bit RGB value.</summary>
    public bool IsRgb => _value is >= _rgbOffset and <= _maximumRgb;

    /// <summary>Gets the RGB red component.</summary>
    public byte Red => IsRgb ? (byte) ((_value - _rgbOffset) >> 16) : (byte) 0;

    /// <summary>Gets the RGB green component.</summary>
    public byte Green => IsRgb ? (byte) ((_value - _rgbOffset) >> 8) : (byte) 0;

    /// <summary>Gets the RGB blue component.</summary>
    public byte Blue => IsRgb ? (byte) (_value - _rgbOffset) : (byte) 0;

    #endregion

    /// <inheritdoc/>
    public override string ToString() =>
        IsDefault
            ? "Color { Default }"
            : IsTransparent
                ? "Color { Transparent }"
                : IsRgb
                    ? $"Color {{ Rgb = ({Red}, {Green}, {Blue}) }}"
                    : "Color { Unknown }";

    private static void ValidateComponent(int value, string parameterName)
    {
        if (value is < byte.MinValue or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, value, "A color component must be from 0 through 255.");
        }
    }

    private static bool TryNibble(char c, out int value) =>
        int.TryParse([c], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);

    private static bool TryByte(ReadOnlySpan<char> pair, out int value) =>
        int.TryParse(pair, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);

    private static Color FromAnsi(int value) => value switch
    {
        0 => Rgb(0x00, 0x00, 0x00),
        1 => Rgb(0xcd, 0x00, 0x00),
        2 => Rgb(0x00, 0xcd, 0x00),
        3 => Rgb(0xcd, 0xcd, 0x00),
        4 => Rgb(0x00, 0x00, 0xee),
        5 => Rgb(0xcd, 0x00, 0xcd),
        6 => Rgb(0x00, 0xcd, 0xcd),
        7 => Rgb(0xe5, 0xe5, 0xe5),
        8 => Rgb(0x7f, 0x7f, 0x7f),
        9 => Rgb(0xff, 0x00, 0x00),
        10 => Rgb(0x00, 0xff, 0x00),
        11 => Rgb(0xff, 0xff, 0x00),
        12 => Rgb(0x5c, 0x5c, 0xff),
        13 => Rgb(0xff, 0x00, 0xff),
        14 => Rgb(0x00, 0xff, 0xff),
        15 => Rgb(0xff, 0xff, 0xff),
        _ => throw new UnreachableException()
    };
}
