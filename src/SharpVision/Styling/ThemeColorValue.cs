// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Globalization;

using SharpVision.Terminal.Protocols;

/// <summary>Parses the theme color-value grammar: <c>#hex</c>, <c>idx:N</c>, or (elsewhere) a palette key.</summary>
internal static class ThemeColorValue
{
    private const string _indexPrefix = "idx:";

    /// <summary>Gets whether the value is an inline literal (<c>#hex</c> or <c>idx:N</c>) rather than a palette key.</summary>
    /// <param name="value">The candidate value; must be non-null.</param>
    /// <returns>Whether the value is an inline color literal.</returns>
    public static bool IsLiteral(string value) =>
        value.StartsWith('#') || value.StartsWith(_indexPrefix, StringComparison.Ordinal);

    /// <summary>Parses an inline literal into a color.</summary>
    /// <param name="value">A <c>#hex</c> or <c>idx:N</c> literal.</param>
    /// <returns>The parsed color.</returns>
    /// <exception cref="FormatException">The literal is malformed or the index is outside 0-255.</exception>
    public static Color ParseLiteral(string value)
    {
        if (value.StartsWith(_indexPrefix, StringComparison.Ordinal))
        {
            ReadOnlySpan<char> digits = value.AsSpan(_indexPrefix.Length);

            return !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int index) ||
                index is < 0 or > 255
                ? throw new FormatException($"'{value}' is not a valid idx:0-255 color.")
                : Color.Indexed(index);
        }

        return Color.FromHex(value);
    }
}
