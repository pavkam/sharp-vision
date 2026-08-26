// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines the complete immutable slider track, fill, and thumb glyph family. Each
/// member is the theme-customizable primary glyph only - the portable ASCII repair value is
/// permanently code-owned (see ScrollBarGlyphs for the identical reasoning).</summary>
[PublicAPI]
public readonly record struct SliderGlyphs: IAppearanceFragment
{
    /// <summary>Initializes the complete slider glyph family.</summary>
    /// <param name="horizontalTrack">The horizontal unfilled rail.</param>
    /// <param name="horizontalFill">The horizontal filled rail.</param>
    /// <param name="verticalTrack">The vertical unfilled rail.</param>
    /// <param name="verticalFill">The vertical filled rail.</param>
    /// <param name="thumb">The draggable thumb.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    public SliderGlyphs(
        Rune horizontalTrack,
        Rune horizontalFill,
        Rune verticalTrack,
        Rune verticalFill,
        Rune thumb)
    {
        // Validated here as well as in each init accessor, and deliberately so: an accessor cannot
        // know which constructor argument it came from, so its ArgumentException names "value".
        // For a family this wide that identifies nothing. Checking by name first means a caller
        // passing one bad glyph among ten is told which one.
        HorizontalTrack = horizontalTrack.ValidateSingleCell(nameof(horizontalTrack));
        HorizontalFill = horizontalFill.ValidateSingleCell(nameof(horizontalFill));
        VerticalTrack = verticalTrack.ValidateSingleCell(nameof(verticalTrack));
        VerticalFill = verticalFill.ValidateSingleCell(nameof(verticalFill));
        Thumb = thumb.ValidateSingleCell(nameof(thumb));
    }

    /// <summary>Gets the established code-owned slider glyph family.</summary>
    public static SliderGlyphs Default { get; } = new(new Rune('─'), new Rune('━'), new Rune('│'), new Rune('┃'), new Rune('◆'));

    /// <summary>Gets the horizontal unfilled rail.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune HorizontalTrack
    {
        get => field.Value == 0 ? Default.HorizontalTrack : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the horizontal filled rail.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune HorizontalFill
    {
        get => field.Value == 0 ? Default.HorizontalFill : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the vertical unfilled rail.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune VerticalTrack
    {
        get => field.Value == 0 ? Default.VerticalTrack : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the vertical filled rail.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune VerticalFill
    {
        get => field.Value == 0 ? Default.VerticalFill : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the draggable thumb.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Thumb
    {
        get => field.Value == 0 ? Default.Thumb : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the code-owned horizontal unfilled rail glyph and repair value.</summary>
    internal ControlGlyph HorizontalTrackGlyph => new(HorizontalTrack, new Rune('.'));

    /// <summary>Gets the code-owned horizontal filled rail glyph and repair value.</summary>
    internal ControlGlyph HorizontalFillGlyph => new(HorizontalFill, new Rune('='));

    /// <summary>Gets the code-owned vertical unfilled rail glyph and repair value.</summary>
    internal ControlGlyph VerticalTrackGlyph => new(VerticalTrack, new Rune('.'));

    /// <summary>Gets the code-owned vertical filled rail glyph and repair value.</summary>
    internal ControlGlyph VerticalFillGlyph => new(VerticalFill, new Rune('='));

    /// <summary>Gets the code-owned thumb glyph and repair value.</summary>
    internal ControlGlyph ThumbGlyph => new(Thumb, new Rune('#'));

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };
}
