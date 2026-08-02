// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Scrolling;

/// <summary>Defines optional member-wise contributions to a complete scrollbar presentation.</summary>
[PublicAPI]
public readonly record struct ScrollBarStyleSet
{
    /// <summary>Initializes a partial scrollbar presentation contribution.</summary>
    /// <param name="chrome">The optional replacement chrome.</param>
    /// <param name="fill">The optional replacement fill treatment.</param>
    /// <param name="glyphs">The optional replacement glyph family.</param>
    /// <param name="trackColor">The optional replacement track foreground.</param>
    /// <param name="thumbColor">The optional replacement thumb foreground.</param>
    /// <param name="buttonColor">The optional replacement directional-button foreground.</param>
    /// <param name="appearance">The optional partial normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chrome"/> or <paramref name="fill"/> is undefined.</exception>
    /// <exception cref="ArgumentException">A supplied glyph is invalid or a supplied part foreground is transparent.</exception>
    public ScrollBarStyleSet(
        ScrollBarChrome? chrome = null,
        ScrollBarFill? fill = null,
        ScrollBarGlyphs? glyphs = null,
        ColorValue? trackColor = null,
        ColorValue? thumbColor = null,
        ColorValue? buttonColor = null,
        AppearanceProfileSet? appearance = null)
    {
        if (chrome is { } chromeValue)
        {
            chromeValue.ValidateDefined();
        }

        if (fill is { } fillValue)
        {
            fillValue.ValidateDefined();
        }

        if (glyphs is { } glyphValues)
        {
            ValidateGlyphs(glyphValues);
        }

        if (trackColor is { } trackColorValue)
        {
            ColorValue.ValidatePaint(trackColorValue, nameof(trackColor));
        }

        if (thumbColor is { } thumbColorValue)
        {
            ColorValue.ValidatePaint(thumbColorValue, nameof(thumbColor));
        }

        if (buttonColor is { } buttonColorValue)
        {
            ColorValue.ValidatePaint(buttonColorValue, nameof(buttonColor));
        }

        Chrome = chrome;
        Fill = fill;
        Glyphs = glyphs;
        TrackColor = trackColor;
        ThumbColor = thumbColor;
        ButtonColor = buttonColor;
        Appearance = appearance;
    }

    /// <summary>Gets the optional replacement chrome.</summary>
    public ScrollBarChrome? Chrome { get; }

    /// <summary>Gets the optional replacement fill treatment.</summary>
    public ScrollBarFill? Fill { get; }

    /// <summary>Gets the optional replacement glyph family.</summary>
    public ScrollBarGlyphs? Glyphs { get; }

    /// <summary>Gets the optional replacement track foreground.</summary>
    public ColorValue? TrackColor { get; }

    /// <summary>Gets the optional replacement thumb foreground.</summary>
    public ColorValue? ThumbColor { get; }

    /// <summary>Gets the optional replacement button foreground.</summary>
    public ColorValue? ButtonColor { get; }

    /// <summary>Gets the optional partial normal and visual-state appearance profile.</summary>
    public AppearanceProfileSet? Appearance { get; }

    /// <summary>Applies this partial contribution to a complete scrollbar presentation.</summary>
    /// <param name="baseline">The complete presentation that supplies omitted members.</param>
    /// <returns>The validated complete composed presentation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A composed enum value is undefined.</exception>
    /// <exception cref="ArgumentException">A composed part foreground is transparent.</exception>
    public ScrollBarStyle Apply(ScrollBarStyle baseline)
    {
        var appearance = Appearance is null
            ? baseline.Appearance
            : StyleResolution.Apply(baseline.Appearance, Appearance.Value);

        return new ScrollBarStyle(
            Chrome ?? baseline.Chrome,
            Fill ?? baseline.Fill,
            Glyphs ?? baseline.Glyphs,
            TrackColor ?? baseline.TrackColor,
            ThumbColor ?? baseline.ThumbColor,
            ButtonColor ?? baseline.ButtonColor,
            appearance);
    }

    private static void ValidateGlyphs(ScrollBarGlyphs glyphs)
    {
        _ = CellGlyphResolver.ValidateSingleCell(glyphs.VerticalDecrement, nameof(glyphs));
        _ = CellGlyphResolver.ValidateSingleCell(glyphs.VerticalIncrement, nameof(glyphs));
        _ = CellGlyphResolver.ValidateSingleCell(glyphs.HorizontalDecrement, nameof(glyphs));
        _ = CellGlyphResolver.ValidateSingleCell(glyphs.HorizontalIncrement, nameof(glyphs));
        _ = CellGlyphResolver.ValidateSingleCell(glyphs.BlockTrack, nameof(glyphs));
        _ = CellGlyphResolver.ValidateSingleCell(glyphs.BlockThumb, nameof(glyphs));
        _ = CellGlyphResolver.ValidateSingleCell(glyphs.HorizontalLineTrack, nameof(glyphs));
        _ = CellGlyphResolver.ValidateSingleCell(glyphs.HorizontalLineThumb, nameof(glyphs));
        _ = CellGlyphResolver.ValidateSingleCell(glyphs.VerticalLineTrack, nameof(glyphs));
        _ = CellGlyphResolver.ValidateSingleCell(glyphs.VerticalLineThumb, nameof(glyphs));
    }
}

