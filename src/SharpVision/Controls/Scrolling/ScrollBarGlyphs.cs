// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Scrolling;

/// <summary>Defines the complete immutable scrollbar button, track, and thumb glyph family. Each
/// member is the theme-customizable primary glyph only - the portable ASCII repair value used
/// when a terminal cannot render it is permanently code-owned - this was already true
/// before the reflective Overlay engine, since the old registrable "scrollBar.glyphs" section
/// only ever let a theme author the primary Rune.</summary>
[PublicAPI]
public readonly record struct ScrollBarGlyphs: IAppearanceFragment
{
    /// <summary>Initializes the complete scrollbar glyph family.</summary>
    /// <param name="verticalDecrement">The vertical decrement button.</param>
    /// <param name="verticalIncrement">The vertical increment button.</param>
    /// <param name="horizontalDecrement">The horizontal decrement button.</param>
    /// <param name="horizontalIncrement">The horizontal increment button.</param>
    /// <param name="blockTrack">The block-fill track.</param>
    /// <param name="blockThumb">The block-fill thumb.</param>
    /// <param name="horizontalLineTrack">The horizontal line track.</param>
    /// <param name="horizontalLineThumb">The horizontal line thumb.</param>
    /// <param name="verticalLineTrack">The vertical line track.</param>
    /// <param name="verticalLineThumb">The vertical line thumb.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    public ScrollBarGlyphs(
        Rune verticalDecrement,
        Rune verticalIncrement,
        Rune horizontalDecrement,
        Rune horizontalIncrement,
        Rune blockTrack,
        Rune blockThumb,
        Rune horizontalLineTrack,
        Rune horizontalLineThumb,
        Rune verticalLineTrack,
        Rune verticalLineThumb)
    {
        // Validated here as well as in each init accessor, and deliberately so: an accessor cannot
        // know which constructor argument it came from, so its ArgumentException names "value".
        // For a family this wide that identifies nothing. Checking by name first means a caller
        // passing one bad glyph among ten is told which one.
        VerticalDecrement = verticalDecrement.ValidateSingleCell(nameof(verticalDecrement));
        VerticalIncrement = verticalIncrement.ValidateSingleCell(nameof(verticalIncrement));
        HorizontalDecrement = horizontalDecrement.ValidateSingleCell(nameof(horizontalDecrement));
        HorizontalIncrement = horizontalIncrement.ValidateSingleCell(nameof(horizontalIncrement));
        BlockTrack = blockTrack.ValidateSingleCell(nameof(blockTrack));
        BlockThumb = blockThumb.ValidateSingleCell(nameof(blockThumb));
        HorizontalLineTrack = horizontalLineTrack.ValidateSingleCell(nameof(horizontalLineTrack));
        HorizontalLineThumb = horizontalLineThumb.ValidateSingleCell(nameof(horizontalLineThumb));
        VerticalLineTrack = verticalLineTrack.ValidateSingleCell(nameof(verticalLineTrack));
        VerticalLineThumb = verticalLineThumb.ValidateSingleCell(nameof(verticalLineThumb));
    }

    /// <summary>Gets the established code-owned scrollbar glyph family.</summary>
    public static ScrollBarGlyphs Default { get; } = new(
        new Rune('▲'),
        new Rune('▼'),
        new Rune('◀'),
        new Rune('▶'),
        new Rune('░'),
        new Rune('▓'),
        new Rune('─'),
        new Rune('━'),
        new Rune('│'),
        new Rune('┃'));

    /// <summary>Gets the vertical decrement button.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune VerticalDecrement
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the vertical increment button.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune VerticalIncrement
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the horizontal decrement button.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune HorizontalDecrement
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the horizontal increment button.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune HorizontalIncrement
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the block-fill track.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune BlockTrack
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the block-fill thumb.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune BlockThumb
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the horizontal line track.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune HorizontalLineTrack
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the horizontal line thumb.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune HorizontalLineThumb
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the vertical line track.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune VerticalLineTrack
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the vertical line thumb.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune VerticalLineThumb
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the code-owned vertical decrement glyph and repair value.</summary>
    internal ControlGlyph VerticalDecrementGlyph => new(VerticalDecrement, new Rune('^'));

    /// <summary>Gets the code-owned vertical increment glyph and repair value.</summary>
    internal ControlGlyph VerticalIncrementGlyph => new(VerticalIncrement, new Rune('v'));

    /// <summary>Gets the code-owned horizontal decrement glyph and repair value.</summary>
    internal ControlGlyph HorizontalDecrementGlyph => new(HorizontalDecrement, new Rune('<'));

    /// <summary>Gets the code-owned horizontal increment glyph and repair value.</summary>
    internal ControlGlyph HorizontalIncrementGlyph => new(HorizontalIncrement, new Rune('>'));

    /// <summary>Gets the code-owned block track glyph and repair value.</summary>
    internal ControlGlyph BlockTrackGlyph => new(BlockTrack, new Rune('.'));

    /// <summary>Gets the code-owned block thumb glyph and repair value.</summary>
    internal ControlGlyph BlockThumbGlyph => new(BlockThumb, new Rune('#'));

    /// <summary>Gets the code-owned horizontal line track glyph and repair value.</summary>
    internal ControlGlyph HorizontalLineTrackGlyph => new(HorizontalLineTrack, new Rune('-'));

    /// <summary>Gets the code-owned horizontal line thumb glyph and repair value.</summary>
    internal ControlGlyph HorizontalLineThumbGlyph => new(HorizontalLineThumb, new Rune('='));

    /// <summary>Gets the code-owned vertical line track glyph and repair value.</summary>
    internal ControlGlyph VerticalLineTrackGlyph => new(VerticalLineTrack, new Rune('|'));

    /// <summary>Gets the code-owned vertical line thumb glyph and repair value.</summary>
    internal ControlGlyph VerticalLineThumbGlyph => new(VerticalLineThumb, new Rune('#'));

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };
}
