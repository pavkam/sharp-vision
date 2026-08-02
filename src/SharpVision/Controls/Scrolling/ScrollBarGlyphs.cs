// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Scrolling;

/// <summary>Defines the complete immutable scrollbar button, track, and thumb glyph family.</summary>
[PublicAPI]
public readonly struct ScrollBarGlyphs: IEquatable<ScrollBarGlyphs>
{
    private readonly ControlGlyph? _verticalDecrement;
    private readonly ControlGlyph? _verticalIncrement;
    private readonly ControlGlyph? _horizontalDecrement;
    private readonly ControlGlyph? _horizontalIncrement;
    private readonly ControlGlyph? _blockTrack;
    private readonly ControlGlyph? _blockThumb;
    private readonly ControlGlyph? _horizontalLineTrack;
    private readonly ControlGlyph? _horizontalLineThumb;
    private readonly ControlGlyph? _verticalLineTrack;
    private readonly ControlGlyph? _verticalLineThumb;

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
        : this(
            WithFallback(verticalDecrement, '^', nameof(verticalDecrement)),
            WithFallback(verticalIncrement, 'v', nameof(verticalIncrement)),
            WithFallback(horizontalDecrement, '<', nameof(horizontalDecrement)),
            WithFallback(horizontalIncrement, '>', nameof(horizontalIncrement)),
            WithFallback(blockTrack, '.', nameof(blockTrack)),
            WithFallback(blockThumb, '#', nameof(blockThumb)),
            WithFallback(horizontalLineTrack, '-', nameof(horizontalLineTrack)),
            WithFallback(horizontalLineThumb, '=', nameof(horizontalLineThumb)),
            WithFallback(verticalLineTrack, '|', nameof(verticalLineTrack)),
            WithFallback(verticalLineThumb, '#', nameof(verticalLineThumb)))
    {
    }

    /// <summary>Initializes code-owned scrollbar glyphs with portable repair values.</summary>
    /// <param name="verticalDecrement">The vertical decrement button and repair glyph.</param>
    /// <param name="verticalIncrement">The vertical increment button and repair glyph.</param>
    /// <param name="horizontalDecrement">The horizontal decrement button and repair glyph.</param>
    /// <param name="horizontalIncrement">The horizontal increment button and repair glyph.</param>
    /// <param name="blockTrack">The block-fill track and repair glyph.</param>
    /// <param name="blockThumb">The block-fill thumb and repair glyph.</param>
    /// <param name="horizontalLineTrack">The horizontal line track and repair glyph.</param>
    /// <param name="horizontalLineThumb">The horizontal line thumb and repair glyph.</param>
    /// <param name="verticalLineTrack">The vertical line track and repair glyph.</param>
    /// <param name="verticalLineThumb">The vertical line thumb and repair glyph.</param>
    internal ScrollBarGlyphs(
        ControlGlyph verticalDecrement,
        ControlGlyph verticalIncrement,
        ControlGlyph horizontalDecrement,
        ControlGlyph horizontalIncrement,
        ControlGlyph blockTrack,
        ControlGlyph blockThumb,
        ControlGlyph horizontalLineTrack,
        ControlGlyph horizontalLineThumb,
        ControlGlyph verticalLineTrack,
        ControlGlyph verticalLineThumb)
    {
        _verticalDecrement = verticalDecrement;
        _verticalIncrement = verticalIncrement;
        _horizontalDecrement = horizontalDecrement;
        _horizontalIncrement = horizontalIncrement;
        _blockTrack = blockTrack;
        _blockThumb = blockThumb;
        _horizontalLineTrack = horizontalLineTrack;
        _horizontalLineThumb = horizontalLineThumb;
        _verticalLineTrack = verticalLineTrack;
        _verticalLineThumb = verticalLineThumb;
    }

    /// <summary>Gets the established code-owned scrollbar glyph family.</summary>
    public static ScrollBarGlyphs Default => default;

    /// <summary>Gets the vertical decrement button.</summary>
    public Rune VerticalDecrement => VerticalDecrementGlyph.Value;

    /// <summary>Gets the vertical increment button.</summary>
    public Rune VerticalIncrement => VerticalIncrementGlyph.Value;

    /// <summary>Gets the horizontal decrement button.</summary>
    public Rune HorizontalDecrement => HorizontalDecrementGlyph.Value;

    /// <summary>Gets the horizontal increment button.</summary>
    public Rune HorizontalIncrement => HorizontalIncrementGlyph.Value;

    /// <summary>Gets the block-fill track.</summary>
    public Rune BlockTrack => BlockTrackGlyph.Value;

    /// <summary>Gets the block-fill thumb.</summary>
    public Rune BlockThumb => BlockThumbGlyph.Value;

    /// <summary>Gets the horizontal line track.</summary>
    public Rune HorizontalLineTrack => HorizontalLineTrackGlyph.Value;

    /// <summary>Gets the horizontal line thumb.</summary>
    public Rune HorizontalLineThumb => HorizontalLineThumbGlyph.Value;

    /// <summary>Gets the vertical line track.</summary>
    public Rune VerticalLineTrack => VerticalLineTrackGlyph.Value;

    /// <summary>Gets the vertical line thumb.</summary>
    public Rune VerticalLineThumb => VerticalLineThumbGlyph.Value;

    /// <summary>Gets the code-owned vertical decrement glyph and repair value.</summary>
    internal ControlGlyph VerticalDecrementGlyph => _verticalDecrement ?? Glyph('▲', '^');

    /// <summary>Gets the code-owned vertical increment glyph and repair value.</summary>
    internal ControlGlyph VerticalIncrementGlyph => _verticalIncrement ?? Glyph('▼', 'v');

    /// <summary>Gets the code-owned horizontal decrement glyph and repair value.</summary>
    internal ControlGlyph HorizontalDecrementGlyph => _horizontalDecrement ?? Glyph('◀', '<');

    /// <summary>Gets the code-owned horizontal increment glyph and repair value.</summary>
    internal ControlGlyph HorizontalIncrementGlyph => _horizontalIncrement ?? Glyph('▶', '>');

    /// <summary>Gets the code-owned block track glyph and repair value.</summary>
    internal ControlGlyph BlockTrackGlyph => _blockTrack ?? Glyph('░', '.');

    /// <summary>Gets the code-owned block thumb glyph and repair value.</summary>
    internal ControlGlyph BlockThumbGlyph => _blockThumb ?? Glyph('▓', '#');

    /// <summary>Gets the code-owned horizontal line track glyph and repair value.</summary>
    internal ControlGlyph HorizontalLineTrackGlyph => _horizontalLineTrack ?? Glyph('─', '-');

    /// <summary>Gets the code-owned horizontal line thumb glyph and repair value.</summary>
    internal ControlGlyph HorizontalLineThumbGlyph => _horizontalLineThumb ?? Glyph('━', '=');

    /// <summary>Gets the code-owned vertical line track glyph and repair value.</summary>
    internal ControlGlyph VerticalLineTrackGlyph => _verticalLineTrack ?? Glyph('│', '|');

    /// <summary>Gets the code-owned vertical line thumb glyph and repair value.</summary>
    internal ControlGlyph VerticalLineThumbGlyph => _verticalLineThumb ?? Glyph('┃', '#');

    /// <summary>Determines whether every resolved primary and repair glyph equals another family.</summary>
    /// <param name="other">The other glyph family to compare.</param>
    /// <returns><see langword="true"/> when every resolved primary and repair glyph is equal.</returns>
    public bool Equals(ScrollBarGlyphs other) =>
        VerticalDecrementGlyph == other.VerticalDecrementGlyph &&
        VerticalIncrementGlyph == other.VerticalIncrementGlyph &&
        HorizontalDecrementGlyph == other.HorizontalDecrementGlyph &&
        HorizontalIncrementGlyph == other.HorizontalIncrementGlyph &&
        BlockTrackGlyph == other.BlockTrackGlyph &&
        BlockThumbGlyph == other.BlockThumbGlyph &&
        HorizontalLineTrackGlyph == other.HorizontalLineTrackGlyph &&
        HorizontalLineThumbGlyph == other.HorizontalLineThumbGlyph &&
        VerticalLineTrackGlyph == other.VerticalLineTrackGlyph &&
        VerticalLineThumbGlyph == other.VerticalLineThumbGlyph;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ScrollBarGlyphs other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(VerticalDecrementGlyph);
        hash.Add(VerticalIncrementGlyph);
        hash.Add(HorizontalDecrementGlyph);
        hash.Add(HorizontalIncrementGlyph);
        hash.Add(BlockTrackGlyph);
        hash.Add(BlockThumbGlyph);
        hash.Add(HorizontalLineTrackGlyph);
        hash.Add(HorizontalLineThumbGlyph);
        hash.Add(VerticalLineTrackGlyph);
        hash.Add(VerticalLineThumbGlyph);
        return hash.ToHashCode();
    }

    /// <summary>Determines whether two glyph families resolve equally.</summary>
    /// <param name="left">The first glyph family.</param>
    /// <param name="right">The second glyph family.</param>
    /// <returns><see langword="true"/> when the families resolve equally.</returns>
    public static bool operator ==(ScrollBarGlyphs left, ScrollBarGlyphs right) => left.Equals(right);

    /// <summary>Determines whether two glyph families resolve differently.</summary>
    /// <param name="left">The first glyph family.</param>
    /// <param name="right">The second glyph family.</param>
    /// <returns><see langword="true"/> when the families resolve differently.</returns>
    public static bool operator !=(ScrollBarGlyphs left, ScrollBarGlyphs right) => !left.Equals(right);

    private static ControlGlyph WithFallback(Rune value, char fallback, string parameterName)
    {
        var validated = value.ValidateSingleCell(parameterName);
        return new ControlGlyph(validated, new Rune(fallback));
    }

    private static ControlGlyph Glyph(char value, char fallback) =>
        new(new Rune(value), new Rune(fallback));
}
