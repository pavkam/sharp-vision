// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Defines the complete immutable progress-bar fill, track, and indeterminate glyph family.</summary>
/// <remarks>
/// The nine-level sub-cell fraction ramp used by <see cref="ProgressBar.UseSubCellResolution"/>
/// remains code-owned: its endpoints must match <see cref="Fill"/> and <see cref="Track"/> exactly,
/// so it is not independently overridable through this family.
/// </remarks>
[PublicAPI]
public readonly struct ProgressBarGlyphs: IEquatable<ProgressBarGlyphs>
{
    private readonly ControlGlyph? _fill;
    private readonly ControlGlyph? _track;
    private readonly ControlGlyph? _indeterminate;

    /// <summary>Initializes the complete progress-bar glyph family.</summary>
    /// <param name="fill">The fully filled glyph.</param>
    /// <param name="track">The empty-track glyph.</param>
    /// <param name="indeterminate">The indeterminate-state glyph.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    public ProgressBarGlyphs(Rune fill, Rune track, Rune indeterminate)
        : this(
            WithFallback(fill, '#', nameof(fill)),
            WithFallback(track, '.', nameof(track)),
            WithFallback(indeterminate, '?', nameof(indeterminate)))
    {
    }

    /// <summary>Initializes code-owned progress-bar glyphs with portable repair values.</summary>
    /// <param name="fill">The fully filled glyph and repair value.</param>
    /// <param name="track">The empty-track glyph and repair value.</param>
    /// <param name="indeterminate">The indeterminate-state glyph and repair value.</param>
    internal ProgressBarGlyphs(ControlGlyph fill, ControlGlyph track, ControlGlyph indeterminate)
    {
        _fill = fill;
        _track = track;
        _indeterminate = indeterminate;
    }

    /// <summary>Gets the established code-owned progress-bar glyph family.</summary>
    public static ProgressBarGlyphs Default => default;

    /// <summary>Gets the fully filled glyph.</summary>
    public Rune Fill => FillGlyph.Value;

    /// <summary>Gets the empty-track glyph.</summary>
    public Rune Track => TrackGlyph.Value;

    /// <summary>Gets the indeterminate-state glyph.</summary>
    public Rune Indeterminate => IndeterminateGlyph.Value;

    /// <summary>Gets the code-owned fill glyph and repair value.</summary>
    internal ControlGlyph FillGlyph => _fill ?? ControlGlyphs.Progress.Full;

    /// <summary>Gets the code-owned track glyph and repair value.</summary>
    internal ControlGlyph TrackGlyph => _track ?? ControlGlyphs.Progress.Empty;

    /// <summary>Gets the code-owned indeterminate glyph and repair value.</summary>
    internal ControlGlyph IndeterminateGlyph => _indeterminate ?? ControlGlyphs.Progress.Indeterminate;

    /// <summary>Determines whether every resolved primary and repair glyph equals another family.</summary>
    /// <param name="other">The other glyph family to compare.</param>
    /// <returns><see langword="true"/> when every resolved primary and repair glyph is equal.</returns>
    public bool Equals(ProgressBarGlyphs other) =>
        FillGlyph == other.FillGlyph &&
        TrackGlyph == other.TrackGlyph &&
        IndeterminateGlyph == other.IndeterminateGlyph;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ProgressBarGlyphs other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(FillGlyph, TrackGlyph, IndeterminateGlyph);

    /// <summary>Determines whether two glyph families resolve equally.</summary>
    /// <param name="left">The first glyph family.</param>
    /// <param name="right">The second glyph family.</param>
    /// <returns><see langword="true"/> when the families resolve equally.</returns>
    public static bool operator ==(ProgressBarGlyphs left, ProgressBarGlyphs right) => left.Equals(right);

    /// <summary>Determines whether two glyph families resolve differently.</summary>
    /// <param name="left">The first glyph family.</param>
    /// <param name="right">The second glyph family.</param>
    /// <returns><see langword="true"/> when the families resolve differently.</returns>
    public static bool operator !=(ProgressBarGlyphs left, ProgressBarGlyphs right) => !left.Equals(right);

    private static ControlGlyph WithFallback(Rune value, char fallback, string parameterName)
    {
        var validated = CellGlyphResolver.ValidateSingleCell(value, parameterName);
        return new ControlGlyph(validated, new Rune(fallback));
    }
}
