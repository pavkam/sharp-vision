// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using SharpVision.Controls.Input;

/// <summary>Defines one <see cref="GlyphFamily"/>'s complete RadioButton contribution: the
/// mark-layout family paired with its unchecked and checked glyph pair.</summary>
[PublicAPI]
public readonly record struct RadioButtonGlyphFamily
{
    /// <summary>Initializes and validates a mark style and its paired glyph pair.</summary>
    /// <param name="markStyle">The mark-layout family.</param>
    /// <param name="glyphs">The complete unchecked and checked glyph pair.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="markStyle"/> is undefined.</exception>
    public RadioButtonGlyphFamily(RadioButtonMarkStyle markStyle, RadioButtonGlyphs glyphs)
    {
        MarkStyle = markStyle;
        Glyphs = glyphs;
    }

    /// <summary>Gets the mark-layout family.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is undefined.</exception>
    public RadioButtonMarkStyle MarkStyle
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);
            field = value;
        }
    }

    /// <summary>Gets the complete unchecked and checked glyph pair.</summary>
    public RadioButtonGlyphs Glyphs { get; init; }
}
