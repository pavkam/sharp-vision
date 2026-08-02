// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Defines optional member-wise contributions to a complete progress-bar presentation.</summary>
[PublicAPI]
public readonly record struct ProgressBarStyleSet
{
    /// <summary>Initializes a partial progress-bar presentation contribution.</summary>
    /// <param name="fillColor">The optional replacement completed-progress foreground.</param>
    /// <param name="trackColor">The optional replacement incomplete-track foreground.</param>
    /// <param name="indeterminateColor">The optional replacement indeterminate-state foreground.</param>
    /// <param name="glyphs">The optional replacement fill, track, and indeterminate glyph family.</param>
    /// <param name="appearance">The optional partial normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentException">A supplied glyph is invalid or a supplied part foreground is transparent.</exception>
    public ProgressBarStyleSet(
        ColorValue? fillColor = null,
        ColorValue? trackColor = null,
        ColorValue? indeterminateColor = null,
        ProgressBarGlyphs? glyphs = null,
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

        if (indeterminateColor is { } indeterminateColorValue)
        {
            ColorValue.ValidatePaint(indeterminateColorValue, nameof(indeterminateColor));
        }

        if (glyphs is { } glyphValues)
        {
            ValidateGlyphs(glyphValues);
        }

        FillColor = fillColor;
        TrackColor = trackColor;
        IndeterminateColor = indeterminateColor;
        Glyphs = glyphs;
        Appearance = appearance;
    }

    /// <summary>Gets the optional replacement completed-progress foreground.</summary>
    public ColorValue? FillColor { get; }

    /// <summary>Gets the optional replacement incomplete-track foreground.</summary>
    public ColorValue? TrackColor { get; }

    /// <summary>Gets the optional replacement indeterminate-state foreground.</summary>
    public ColorValue? IndeterminateColor { get; }

    /// <summary>Gets the optional replacement fill, track, and indeterminate glyph family.</summary>
    public ProgressBarGlyphs? Glyphs { get; }

    /// <summary>Gets the optional partial normal and visual-state appearance profile.</summary>
    public AppearanceProfileSet? Appearance { get; }

    /// <summary>Applies this partial contribution to a complete progress-bar presentation.</summary>
    /// <param name="baseline">The complete presentation that supplies omitted members.</param>
    /// <returns>The validated complete composed presentation.</returns>
    /// <exception cref="ArgumentException">A composed part foreground is transparent.</exception>
    public ProgressBarStyle Apply(ProgressBarStyle baseline)
    {
        var appearance = Appearance is null
            ? baseline.Appearance
            : StyleResolution.Apply(baseline.Appearance, Appearance.Value);

        return new ProgressBarStyle(
            FillColor ?? baseline.FillColor,
            TrackColor ?? baseline.TrackColor,
            IndeterminateColor ?? baseline.IndeterminateColor,
            Glyphs ?? baseline.Glyphs,
            appearance);
    }

    private static void ValidateGlyphs(ProgressBarGlyphs glyphs)
    {
        _ = glyphs.Fill.ValidateSingleCell(nameof(glyphs));
        _ = glyphs.Track.ValidateSingleCell(nameof(glyphs));
        _ = glyphs.Indeterminate.ValidateSingleCell(nameof(glyphs));
    }
}
