// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines optional member-wise contributions to a complete slider presentation.</summary>
[PublicAPI]
public readonly record struct SliderStyleSet
{
    /// <summary>Initializes a partial slider presentation contribution.</summary>
    /// <param name="fillColor">The optional replacement filled-rail foreground.</param>
    /// <param name="trackColor">The optional replacement unfilled-rail foreground.</param>
    /// <param name="thumbColor">The optional replacement thumb foreground.</param>
    /// <param name="glyphs">The optional replacement track, fill, and thumb glyph family.</param>
    /// <param name="appearance">The optional partial normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentException">A supplied glyph is invalid or a supplied part foreground is transparent.</exception>
    public SliderStyleSet(
        ColorValue? fillColor = null,
        ColorValue? trackColor = null,
        ColorValue? thumbColor = null,
        SliderGlyphs? glyphs = null,
        AppearanceProfileSet? appearance = null)
    {
        if (fillColor is { } fillColorValue)
        {
            ColorValue.ValidatePaint(fillColorValue, nameof(fillColor));
        }

        if (trackColor is { } trackColorValue)
        {
            ColorValue.ValidatePaint(trackColorValue, nameof(trackColor));
        }

        if (thumbColor is { } thumbColorValue)
        {
            ColorValue.ValidatePaint(thumbColorValue, nameof(thumbColor));
        }

        if (glyphs is { } glyphValues)
        {
            ValidateGlyphs(glyphValues);
        }

        FillColor = fillColor;
        TrackColor = trackColor;
        ThumbColor = thumbColor;
        Glyphs = glyphs;
        Appearance = appearance;
    }

    /// <summary>Gets the optional replacement filled-rail foreground.</summary>
    public ColorValue? FillColor { get; }

    /// <summary>Gets the optional replacement unfilled-rail foreground.</summary>
    public ColorValue? TrackColor { get; }

    /// <summary>Gets the optional replacement thumb foreground.</summary>
    public ColorValue? ThumbColor { get; }

    /// <summary>Gets the optional replacement track, fill, and thumb glyph family.</summary>
    public SliderGlyphs? Glyphs { get; }

    /// <summary>Gets the optional partial normal and visual-state appearance profile.</summary>
    public AppearanceProfileSet? Appearance { get; }

    /// <summary>Applies this partial contribution to a complete slider presentation.</summary>
    /// <param name="baseline">The complete presentation that supplies omitted members.</param>
    /// <returns>The validated complete composed presentation.</returns>
    /// <exception cref="ArgumentException">A composed part foreground is transparent.</exception>
    public SliderStyle Apply(SliderStyle baseline)
    {
        var appearance = Appearance is null
            ? baseline.Appearance
            : StyleResolution.Apply(baseline.Appearance, Appearance.Value);

        return new SliderStyle(
            FillColor ?? baseline.FillColor,
            TrackColor ?? baseline.TrackColor,
            ThumbColor ?? baseline.ThumbColor,
            Glyphs ?? baseline.Glyphs,
            appearance);
    }

    private static void ValidateGlyphs(SliderGlyphs glyphs)
    {
        _ = glyphs.HorizontalTrack.ValidateSingleCell(nameof(glyphs));
        _ = glyphs.HorizontalFill.ValidateSingleCell(nameof(glyphs));
        _ = glyphs.VerticalTrack.ValidateSingleCell(nameof(glyphs));
        _ = glyphs.VerticalFill.ValidateSingleCell(nameof(glyphs));
        _ = glyphs.Thumb.ValidateSingleCell(nameof(glyphs));
    }
}
