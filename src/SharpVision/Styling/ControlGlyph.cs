// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines one code-owned control glyph and its one-cell repair value.</summary>
internal readonly record struct ControlGlyph
{
    /// <summary>Initializes a printable one-cell primary glyph and fallback.</summary>
    public ControlGlyph(Rune value, Rune fallback)
    {
        Value = Validate(value, nameof(value));
        Fallback = Validate(fallback, nameof(fallback));
    }

    /// <summary>Gets the preferred Unicode scalar.</summary>
    public Rune Value { get; }

    /// <summary>Gets the portable repair scalar.</summary>
    public Rune Fallback { get; }

    private static Rune Validate(Rune value, string name)
    {
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);
        var measurement = Width.Measure(buffer[..length]);

        return measurement is { Cells: 1, Controls: 0 }
            ? value
            : throw new ArgumentException("A control glyph must be printable and one cell wide.", name);
    }
}
