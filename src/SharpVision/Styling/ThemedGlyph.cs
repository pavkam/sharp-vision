// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines one semantic theme glyph and its terminal-safe repair value.</summary>
public readonly record struct ThemedGlyph
{
    /// <summary>Initializes a printable one-cell primary glyph and fallback.</summary>
    /// <param name="value">The preferred Unicode scalar.</param>
    /// <param name="fallback">The repair scalar used when the preferred value is unsuitable.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> or <paramref name="fallback"/> is a control or does not occupy exactly one cell.
    /// </exception>
    public ThemedGlyph(Rune value, Rune fallback)
    {
        Value = Validate(value, nameof(value));
        Fallback = Validate(fallback, nameof(fallback));
    }

    /// <summary>Gets the preferred Unicode scalar.</summary>
    public Rune Value { get; }

    /// <summary>Gets the terminal-safe repair scalar.</summary>
    public Rune Fallback { get; }

    private static Rune Validate(Rune value, string name)
    {
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);
        var measurement = Width.Measure(buffer[..length]);

        return measurement.Cells == 1 && measurement.Controls == 0
            ? value
            : throw new ArgumentException("A theme glyph must be printable and one cell wide.", name);
    }
}
