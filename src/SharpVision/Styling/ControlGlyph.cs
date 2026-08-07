// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines one code-owned control glyph and its one-cell repair value.</summary>
internal readonly record struct ControlGlyph
{
    /// <summary>Initializes a printable one-cell primary glyph and fallback.</summary>
    public ControlGlyph(Rune value, Rune fallback)
    {
        Value = value.ValidateSingleCell(nameof(value));
        Fallback = fallback.ValidateSingleCell(nameof(fallback));
    }

    /// <summary>Gets the preferred Unicode scalar.</summary>
    public Rune Value { get; }

    /// <summary>Gets the portable repair scalar.</summary>
    public Rune Fallback { get; }
}
