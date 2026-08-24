// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using SharpVision.Controls.Input;

/// <summary>Defines one <see cref="GlyphFamily"/>'s complete CheckBox contribution: the
/// mark-layout family paired with its unchecked, checked, and indeterminate glyph trio.</summary>
[PublicAPI]
public readonly record struct CheckBoxGlyphFamily
{
    /// <summary>Initializes and validates a mark style and its paired glyph trio.</summary>
    /// <param name="markStyle">The mark-layout family.</param>
    /// <param name="glyphs">The complete unchecked, checked, and indeterminate glyph trio.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="markStyle"/> is undefined.</exception>
    public CheckBoxGlyphFamily(CheckBoxMarkStyle markStyle, CheckBoxGlyphs glyphs)
    {
        MarkStyle = markStyle;
        Glyphs = glyphs;
    }

    /// <summary>Gets the mark-layout family.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is undefined.</exception>
    public CheckBoxMarkStyle MarkStyle
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);
            field = value;
        }
    }

    /// <summary>Gets the complete unchecked, checked, and indeterminate glyph trio.</summary>
    public CheckBoxGlyphs Glyphs { get; init; }
}
