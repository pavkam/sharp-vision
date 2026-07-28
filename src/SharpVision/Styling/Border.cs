// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines one complete intrinsic border appearance.</summary>
public readonly record struct Border
{
    /// <summary>Initializes a complete border appearance.</summary>
    /// <param name="sides">The enabled one-cell edges.</param>
    /// <param name="glyphStyle">The complete border glyph family.</param>
    /// <param name="foreground">The border foreground.</param>
    /// <param name="background">The border-cell background.</param>
    /// <param name="attributes">The border attributes.</param>
    /// <exception cref="ArgumentException"><paramref name="foreground"/> is transparent.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sides"/> contains unknown flags.</exception>
    public Border(
        BorderSide sides,
        BorderGlyphStyle glyphStyle,
        ColorValue foreground,
        ColorValue background,
        AttributeValue attributes)
    {
        if ((sides & ~BorderSide.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sides), sides, "The border sides contain unknown flags.");
        }

        ColorValue.ValidatePaint(foreground, nameof(foreground));
        Sides = sides;
        GlyphStyle = glyphStyle.TopLeft.Value == 0 ? BorderGlyphStyle.Default : glyphStyle;
        Foreground = foreground;
        Background = background;
        Attributes = attributes;
    }

    /// <summary>Gets the enabled one-cell edges.</summary>
    public BorderSide Sides { get; }

    /// <summary>Gets the complete border glyph family.</summary>
    public BorderGlyphStyle GlyphStyle { get; }

    /// <summary>Gets the border foreground.</summary>
    public ColorValue Foreground { get; }

    /// <summary>Gets the border-cell background.</summary>
    public ColorValue Background { get; }

    /// <summary>Gets the border attributes.</summary>
    public AttributeValue Attributes { get; }

}
