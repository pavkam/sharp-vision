// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines an optional member-wise shadow contribution.</summary>
public readonly record struct ShadowSet
{
    /// <summary>Initializes a partial shadow contribution.</summary>
    public ShadowSet(
        bool? isVisible = null,
        ShadowMode? mode = null,
        Point? offset = null,
        Rune? glyph = null,
        ColorValue? foreground = null,
        ColorValue? background = null,
        AttributeValue? attributes = null)
    {
        if (mode is { } modeValue && !Enum.IsDefined(modeValue))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "The shadow mode is unknown.");
        }

        if (glyph is { } glyphValue)
        {
            _ = glyphValue.ValidateSingleCell(nameof(glyph));
        }

        if (foreground is { } foregroundValue)
        {
            ColorValue.ValidatePaint(foregroundValue, nameof(foreground));
        }

        IsVisible = isVisible;
        Mode = mode;
        Offset = offset;
        Glyph = glyph;
        Foreground = foreground;
        Background = background;
        Attributes = attributes;
    }

    /// <summary>Gets the optional visibility contribution.</summary>
    public bool? IsVisible { get; }

    /// <summary>Gets the optional composition-mode contribution.</summary>
    public ShadowMode? Mode { get; }

    /// <summary>Gets the optional translation contribution.</summary>
    public Point? Offset { get; }

    /// <summary>Gets the optional block-glyph contribution.</summary>
    public Rune? Glyph { get; }

    /// <summary>Gets the optional foreground contribution.</summary>
    public ColorValue? Foreground { get; }

    /// <summary>Gets the optional background contribution.</summary>
    public ColorValue? Background { get; }

    /// <summary>Gets the optional attribute contribution.</summary>
    public AttributeValue? Attributes { get; }

    /// <summary>Applies this contribution to a complete shadow.</summary>
    /// <param name="shadow">The earlier complete shadow.</param>
    /// <returns>The composed complete shadow.</returns>
    public Shadow Apply(Shadow shadow) => new(
        IsVisible ?? shadow.IsVisible,
        Mode ?? shadow.Mode,
        Offset ?? shadow.Offset,
        Glyph ?? shadow.Glyph,
        Foreground ?? shadow.Foreground,
        Background ?? shadow.Background,
        Attributes ?? shadow.Attributes);

    /// <summary>Overlays a later partial contribution over this contribution.</summary>
    /// <param name="later">The later contribution whose supplied members win.</param>
    /// <returns>The combined partial contribution.</returns>
    public ShadowSet Overlay(ShadowSet later) => new(
        later.IsVisible ?? IsVisible,
        later.Mode ?? Mode,
        later.Offset ?? Offset,
        later.Glyph ?? Glyph,
        later.Foreground ?? Foreground,
        later.Background ?? Background,
        later.Attributes ?? Attributes);
}
