// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Defines the complete immutable progress-bar fill, track, and indeterminate glyph family.</summary>
/// <remarks>
/// The nine-level sub-cell fraction ramp used by <see cref="ProgressBar.UseSubCellResolution"/>
/// remains code-owned: its endpoints must match <see cref="Fill"/> and <see cref="Track"/> exactly,
/// so it is not independently overridable through this family. Each member is the
/// theme-customizable primary glyph only - the portable ASCII repair value is permanently
/// code-owned (see ScrollBarGlyphs for the identical reasoning).
/// </remarks>
[PublicAPI]
public readonly record struct ProgressBarGlyphs: IAppearanceFragment
{
    /// <summary>Initializes the complete progress-bar glyph family.</summary>
    /// <param name="fill">The fully filled glyph.</param>
    /// <param name="track">The empty-track glyph.</param>
    /// <param name="indeterminate">The indeterminate-state glyph.</param>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    public ProgressBarGlyphs(Rune fill, Rune track, Rune indeterminate)
    {
        // Validated here as well as in each init accessor, and deliberately so: an accessor cannot
        // know which constructor argument it came from, so its ArgumentException names "value".
        // For a family this wide that identifies nothing. Checking by name first means a caller
        // passing one bad glyph among ten is told which one.
        Fill = fill.ValidateSingleCell(nameof(fill));
        Track = track.ValidateSingleCell(nameof(track));
        Indeterminate = indeterminate.ValidateSingleCell(nameof(indeterminate));
    }

    /// <summary>Gets the established code-owned progress-bar glyph family.</summary>
    public static ProgressBarGlyphs Default { get; } = new(
        ControlGlyphs.Progress.Full.Value,
        ControlGlyphs.Progress.Empty.Value,
        ControlGlyphs.Progress.Indeterminate.Value);

    /// <summary>Gets the fully filled glyph.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Fill
    {
        get => field.Value == 0 ? Default.Fill : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the empty-track glyph.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Track
    {
        get => field.Value == 0 ? Default.Track : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the indeterminate-state glyph.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Indeterminate
    {
        get => field.Value == 0 ? Default.Indeterminate : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the code-owned fill glyph and repair value.</summary>
    internal ControlGlyph FillGlyph => new(Fill, ControlGlyphs.Progress.Full.Fallback);

    /// <summary>Gets the code-owned track glyph and repair value.</summary>
    internal ControlGlyph TrackGlyph => new(Track, ControlGlyphs.Progress.Empty.Fallback);

    /// <summary>Gets the code-owned indeterminate glyph and repair value.</summary>
    internal ControlGlyph IndeterminateGlyph => new(Indeterminate, ControlGlyphs.Progress.Indeterminate.Fallback);

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };
}
