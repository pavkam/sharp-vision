// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

using System.Globalization;

/// <summary>Represents a validated default, indexed, or RGB terminal color.</summary>
public readonly record struct Color
{
    /// <summary>Gets the terminal default color.</summary>
    public static Color Default { get; } = new(ColorKind.Default, 0, 0, 0);

    private Color(ColorKind kind, byte red, byte green, byte blue)
    {
        Kind = kind;
        Red = red;
        Green = green;
        Blue = blue;
    }

    /// <summary>Gets the representation used by this color.</summary>
    public ColorKind Kind { get; }

    /// <summary>Gets the palette index or RGB red component.</summary>
    public byte Red { get; }

    /// <summary>Gets the RGB green component.</summary>
    public byte Green { get; }

    /// <summary>Gets the RGB blue component.</summary>
    public byte Blue { get; }

    /// <summary>Creates a 256-color palette reference.</summary>
    /// <param name="index">The palette index from 0 through 255.</param>
    /// <returns>The validated indexed color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is outside 0 through 255.
    /// </exception>
    public static Color Indexed(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, byte.MaxValue);
        return new Color(ColorKind.Indexed, (byte) index, 0, 0);
    }

    /// <summary>Creates a 24-bit RGB color.</summary>
    /// <param name="red">The red component from 0 through 255.</param>
    /// <param name="green">The green component from 0 through 255.</param>
    /// <param name="blue">The blue component from 0 through 255.</param>
    /// <returns>The validated RGB color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A component is outside 0 through 255.
    /// </exception>
    public static Color Rgb(int red, int green, int blue)
    {
        ValidateComponent(red, nameof(red));
        ValidateComponent(green, nameof(green));
        ValidateComponent(blue, nameof(blue));
        return new Color(ColorKind.Rgb, (byte) red, (byte) green, (byte) blue);
    }

    /// <summary>Parses a hex RGB color string (<c>#rgb</c> or <c>#rrggbb</c>, case-insensitive, leading <c>#</c> optional).</summary>
    /// <param name="value">The hex color string.</param>
    /// <returns>The parsed 24-bit RGB color.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="FormatException">The string is not a 3- or 6-digit hex color.</exception>
    public static Color FromHex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return TryFromHex(value, out Color color)
            ? color
            : throw new FormatException($"'{value}' is not a valid #rgb or #rrggbb color.");
    }

    /// <summary>Attempts to parse a hex RGB color string without throwing.</summary>
    /// <param name="value">The candidate hex color string.</param>
    /// <param name="color">The parsed color, or <see cref="Default"/> when parsing fails.</param>
    /// <returns>Whether <paramref name="value"/> is a valid 3- or 6-digit hex color.</returns>
    public static bool TryFromHex(string value, out Color color)
    {
        color = Default;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        ReadOnlySpan<char> digits = value[0] == '#' ? value.AsSpan(1) : value.AsSpan();

        if (digits.Length == 3)
        {
            if (!TryNibble(digits[0], out int r) || !TryNibble(digits[1], out int g) || !TryNibble(digits[2], out int b))
            {
                return false;
            }

            color = Rgb((r << 4) | r, (g << 4) | g, (b << 4) | b);
            return true;
        }

        if (digits.Length == 6)
        {
            if (!TryByte(digits[..2], out int r) || !TryByte(digits[2..4], out int g) || !TryByte(digits[4..], out int b))
            {
                return false;
            }

            color = Rgb(r, g, b);
            return true;
        }

        return false;
    }

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
}
