// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines the complete immutable slider track, fill, and thumb glyph family.</summary>
[PublicAPI]
public readonly struct SliderGlyphs: IEquatable<SliderGlyphs>
{
    private readonly ControlGlyph? _horizontalTrack;
    private readonly ControlGlyph? _horizontalFill;
    private readonly ControlGlyph? _verticalTrack;
    private readonly ControlGlyph? _verticalFill;
    private readonly ControlGlyph? _thumb;

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
        : this(
            WithFallback(horizontalTrack, '.', nameof(horizontalTrack)),
            WithFallback(horizontalFill, '=', nameof(horizontalFill)),
            WithFallback(verticalTrack, '.', nameof(verticalTrack)),
            WithFallback(verticalFill, '=', nameof(verticalFill)),
            WithFallback(thumb, '#', nameof(thumb)))
    {
    }

    /// <summary>Initializes code-owned slider glyphs with portable repair values.</summary>
    /// <param name="horizontalTrack">The horizontal unfilled rail and repair glyph.</param>
    /// <param name="horizontalFill">The horizontal filled rail and repair glyph.</param>
    /// <param name="verticalTrack">The vertical unfilled rail and repair glyph.</param>
    /// <param name="verticalFill">The vertical filled rail and repair glyph.</param>
    /// <param name="thumb">The draggable thumb and repair glyph.</param>
    internal SliderGlyphs(
        ControlGlyph horizontalTrack,
        ControlGlyph horizontalFill,
        ControlGlyph verticalTrack,
        ControlGlyph verticalFill,
        ControlGlyph thumb)
    {
        _horizontalTrack = horizontalTrack;
        _horizontalFill = horizontalFill;
        _verticalTrack = verticalTrack;
        _verticalFill = verticalFill;
        _thumb = thumb;
    }

    /// <summary>Gets the established code-owned slider glyph family.</summary>
    public static SliderGlyphs Default => default;

    /// <summary>Gets the horizontal unfilled rail.</summary>
    public Rune HorizontalTrack => HorizontalTrackGlyph.Value;

    /// <summary>Gets the horizontal filled rail.</summary>
    public Rune HorizontalFill => HorizontalFillGlyph.Value;

    /// <summary>Gets the vertical unfilled rail.</summary>
    public Rune VerticalTrack => VerticalTrackGlyph.Value;

    /// <summary>Gets the vertical filled rail.</summary>
    public Rune VerticalFill => VerticalFillGlyph.Value;

    /// <summary>Gets the draggable thumb.</summary>
    public Rune Thumb => ThumbGlyph.Value;

    /// <summary>Gets the code-owned horizontal unfilled rail glyph and repair value.</summary>
    internal ControlGlyph HorizontalTrackGlyph => _horizontalTrack ?? Glyph('─', '.');

    /// <summary>Gets the code-owned horizontal filled rail glyph and repair value.</summary>
    internal ControlGlyph HorizontalFillGlyph => _horizontalFill ?? Glyph('━', '=');

    /// <summary>Gets the code-owned vertical unfilled rail glyph and repair value.</summary>
    internal ControlGlyph VerticalTrackGlyph => _verticalTrack ?? Glyph('│', '.');

    /// <summary>Gets the code-owned vertical filled rail glyph and repair value.</summary>
    internal ControlGlyph VerticalFillGlyph => _verticalFill ?? Glyph('┃', '=');

    /// <summary>Gets the code-owned thumb glyph and repair value.</summary>
    internal ControlGlyph ThumbGlyph => _thumb ?? Glyph('◆', '#');

    /// <summary>Determines whether every resolved primary and repair glyph equals another family.</summary>
    /// <param name="other">The other glyph family to compare.</param>
    /// <returns><see langword="true"/> when every resolved primary and repair glyph is equal.</returns>
    public bool Equals(SliderGlyphs other) =>
        HorizontalTrackGlyph == other.HorizontalTrackGlyph &&
        HorizontalFillGlyph == other.HorizontalFillGlyph &&
        VerticalTrackGlyph == other.VerticalTrackGlyph &&
        VerticalFillGlyph == other.VerticalFillGlyph &&
        ThumbGlyph == other.ThumbGlyph;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SliderGlyphs other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(
        HorizontalTrackGlyph,
        HorizontalFillGlyph,
        VerticalTrackGlyph,
        VerticalFillGlyph,
        ThumbGlyph);

    /// <summary>Determines whether two glyph families resolve equally.</summary>
    /// <param name="left">The first glyph family.</param>
    /// <param name="right">The second glyph family.</param>
    /// <returns><see langword="true"/> when the families resolve equally.</returns>
    public static bool operator ==(SliderGlyphs left, SliderGlyphs right) => left.Equals(right);

    /// <summary>Determines whether two glyph families resolve differently.</summary>
    /// <param name="left">The first glyph family.</param>
    /// <param name="right">The second glyph family.</param>
    /// <returns><see langword="true"/> when the families resolve differently.</returns>
    public static bool operator !=(SliderGlyphs left, SliderGlyphs right) => !left.Equals(right);

    private static ControlGlyph WithFallback(Rune value, char fallback, string parameterName)
    {
        var validated = CellGlyphResolver.ValidateSingleCell(value, parameterName);
        return new ControlGlyph(validated, new Rune(fallback));
    }

    private static ControlGlyph Glyph(char value, char fallback) =>
        new(new Rune(value), new Rune(fallback));
}
