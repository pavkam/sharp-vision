// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Defines the complete immutable narrow-glyph family used by chart renderers.</summary>
/// <remarks>
/// A record struct implementing <see cref="IAppearanceFragment"/>, like every other glyph family.
/// It was previously a plain struct with get-only members, which meant <c>Theme.Overlay</c> did not
/// recurse into it and its JSON fell through to <c>JsonSerializer</c> - so the <c>glyphs</c> member
/// of the <c>chart</c> key could never be applied by any theme.
/// </remarks>
[PublicAPI]
public readonly record struct ChartGlyphs: IAppearanceFragment
{
    /// <summary>Initializes a complete chart glyph family.</summary>
    /// <param name="bar">The solid bar glyph.</param>
    /// <param name="point">The visible line-point glyph.</param>
    /// <param name="line">The rasterized line-segment glyph.</param>
    /// <param name="area">The filled area glyph.</param>
    /// <param name="legendMarker">The legend color marker.</param>
    /// <param name="verticalAxis">The vertical axis rule.</param>
    /// <param name="horizontalAxis">The horizontal axis and legend-divider rule.</param>
    /// <exception cref="ArgumentException">A glyph is not printable and one cell wide.</exception>
    public ChartGlyphs(
        Rune bar,
        Rune point,
        Rune line,
        Rune area,
        Rune legendMarker,
        Rune verticalAxis,
        Rune horizontalAxis)
    {
        // Validated by parameter name here as well as in each init accessor, since an accessor only
        // ever knows the value as "value".
        Bar = bar.ValidateSingleCell(nameof(bar));
        Point = point.ValidateSingleCell(nameof(point));
        Line = line.ValidateSingleCell(nameof(line));
        Area = area.ValidateSingleCell(nameof(area));
        LegendMarker = legendMarker.ValidateSingleCell(nameof(legendMarker));
        VerticalAxis = verticalAxis.ValidateSingleCell(nameof(verticalAxis));
        HorizontalAxis = horizontalAxis.ValidateSingleCell(nameof(horizontalAxis));
    }

    /// <summary>Gets the standard Unicode chart glyph family.</summary>
    public static ChartGlyphs Default { get; } = new(
        new Rune('█'),
        new Rune('●'),
        new Rune('•'),
        new Rune('█'),
        new Rune('■'),
        new Rune('│'),
        new Rune('─'));

    /// <summary>Gets the bar fill glyph.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Bar
    {
        get => field.Value == 0 ? Default.Bar : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the visible point glyph.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Point
    {
        get => field.Value == 0 ? Default.Point : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the rasterized connection glyph.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Line
    {
        get => field.Value == 0 ? Default.Line : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the area fill glyph.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Area
    {
        get => field.Value == 0 ? Default.Area : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the legend marker glyph.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune LegendMarker
    {
        get => field.Value == 0 ? Default.LegendMarker : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the vertical axis rule.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune VerticalAxis
    {
        get => field.Value == 0 ? Default.VerticalAxis : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the horizontal axis rule, also used for the legend divider.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune HorizontalAxis
    {
        get => field.Value == 0 ? Default.HorizontalAxis : field;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };
}
