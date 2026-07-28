// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines one complete intrinsic shadow appearance.</summary>
public readonly record struct Shadow
{
    /// <summary>Initializes a complete shadow appearance.</summary>
    /// <param name="isVisible">Whether shadow chrome is rendered.</param>
    /// <param name="mode">How shadow cells compose with their destination.</param>
    /// <param name="offset">The signed shadow translation in cells or half rows.</param>
    /// <param name="glyph">The block-glyph shadow Rune.</param>
    /// <param name="foreground">The shadow foreground.</param>
    /// <param name="background">The shadow background.</param>
    /// <param name="attributes">The shadow attributes.</param>
    /// <exception cref="ArgumentException">A paint channel is transparent or <paramref name="glyph"/> is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is unknown.</exception>
    public Shadow(
        bool isVisible,
        ShadowMode mode,
        Point offset,
        Rune glyph,
        ColorValue foreground,
        ColorValue background,
        AttributeValue attributes)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "The shadow mode is unknown.");
        }

        ColorValue.ValidatePaint(foreground, nameof(foreground));
        IsVisible = isVisible;
        Mode = mode;
        Offset = offset;
        Glyph = glyph.Value == 0
            ? ControlGlyphs.Chrome.Shadow.Value
            : CellGlyphResolver.ValidateSingleCell(glyph, nameof(glyph));
        Foreground = foreground;
        Background = background;
        Attributes = attributes;
    }

    /// <summary>Gets whether shadow chrome is rendered.</summary>
    public bool IsVisible { get; }

    /// <summary>Gets how shadow cells compose with their destination.</summary>
    public ShadowMode Mode { get; }

    /// <summary>Gets the signed shadow translation.</summary>
    public Point Offset { get; }

    /// <summary>Gets the block-glyph shadow Rune.</summary>
    public Rune Glyph { get; }

    /// <summary>Gets the shadow foreground.</summary>
    public ColorValue Foreground { get; }

    /// <summary>Gets the shadow background.</summary>
    public ColorValue Background { get; }

    /// <summary>Gets the shadow attributes.</summary>
    public AttributeValue Attributes { get; }
}
