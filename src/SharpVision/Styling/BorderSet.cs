// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines an optional member-wise border contribution.</summary>
public readonly record struct BorderSet
{
    /// <summary>Initializes a partial border contribution.</summary>
    public BorderSet(
        BorderSide? sides = null,
        BorderGlyphStyle? glyphStyle = null,
        ColorValue? foreground = null,
        ColorValue? background = null,
        AttributeValue? attributes = null)
    {
        if (sides is { } sideValue && (sideValue & ~BorderSide.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sides), sides, "The border sides contain unknown flags.");
        }

        if (foreground is { } foregroundValue)
        {
            ColorValue.ValidatePaint(foregroundValue, nameof(foreground));
        }

        Sides = sides;
        GlyphStyle = glyphStyle;
        Foreground = foreground;
        Background = background;
        Attributes = attributes;
    }

    /// <summary>Gets the optional edge contribution.</summary>
    public BorderSide? Sides { get; }

    /// <summary>Gets the optional glyph-family contribution.</summary>
    public BorderGlyphStyle? GlyphStyle { get; }

    /// <summary>Gets the optional foreground contribution.</summary>
    public ColorValue? Foreground { get; }

    /// <summary>Gets the optional background contribution.</summary>
    public ColorValue? Background { get; }

    /// <summary>Gets the optional attribute contribution.</summary>
    public AttributeValue? Attributes { get; }

    /// <summary>Applies this contribution to a complete border.</summary>
    /// <param name="border">The earlier complete border.</param>
    /// <returns>The composed complete border.</returns>
    public Border Apply(Border border) => new(
        Sides ?? border.Sides,
        GlyphStyle ?? border.GlyphStyle,
        Foreground ?? border.Foreground,
        Background ?? border.Background,
        Attributes ?? border.Attributes);

    /// <summary>Overlays a later partial contribution over this contribution.</summary>
    /// <param name="later">The later contribution whose supplied members win.</param>
    /// <returns>The combined partial contribution.</returns>
    public BorderSet Overlay(BorderSet later) => new(
        later.Sides ?? Sides,
        later.GlyphStyle ?? GlyphStyle,
        later.Foreground ?? Foreground,
        later.Background ?? Background,
        later.Attributes ?? Attributes);
}
