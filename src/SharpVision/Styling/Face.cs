// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines one complete control-face appearance.</summary>
public readonly record struct Face
{
    /// <summary>Initializes a complete face appearance.</summary>
    /// <param name="foreground">The text and glyph foreground.</param>
    /// <param name="background">The body background.</param>
    /// <param name="attributes">The complete terminal attributes.</param>
    /// <param name="underline">The typed underline style.</param>
    /// <param name="underlineColor">The underline color.</param>
    /// <exception cref="ArgumentException">A paint channel is transparent or decorations conflict.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum or flag value is unknown.</exception>
    public Face(
        ColorValue foreground,
        ColorValue background,
        AttributeValue attributes,
        Underline underline,
        ColorValue underlineColor)
    {
        ColorValue.ValidatePaint(foreground, nameof(foreground));
        ColorValue.ValidatePaint(underlineColor, nameof(underlineColor));

        if (!Enum.IsDefined(underline))
        {
            throw new ArgumentOutOfRangeException(nameof(underline), underline, "The underline style is unknown.");
        }

        if (attributes.IsLiteral && underlineColor.IsLiteral)
        {
            DecorationResolver.Validate(attributes.Literal, underline, underlineColor.Literal);
        }

        Foreground = foreground;
        Background = background;
        Attributes = attributes;
        Underline = underline;
        UnderlineColor = underlineColor;
    }

    /// <summary>Gets the text and glyph foreground.</summary>
    public ColorValue Foreground { get; }

    /// <summary>Gets the body background.</summary>
    public ColorValue Background { get; }

    /// <summary>Gets the complete terminal attributes.</summary>
    public AttributeValue Attributes { get; }

    /// <summary>Gets the typed underline style.</summary>
    public Underline Underline { get; }

    /// <summary>Gets the underline color.</summary>
    public ColorValue UnderlineColor { get; }
}
