// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Contains the resolved plot layout, numeric range, and coordinate and style mapping a
/// chart authored directly against <see cref="ChartControlBase"/> needs to render its own content,
/// created once per frame by <see cref="ChartControlBase.CreateRenderContext(TerminalCanvas)"/>.
/// </summary>
[PublicAPI]
public readonly struct ChartRenderContext
{
    /// <summary>Initializes one chart render context.</summary>
    internal ChartRenderContext(
        IChartControl chart,
        ChartPlotLayout layout,
        ChartScaleRange range,
        TerminalStyle inheritedStyle)
    {
        Chart = chart;
        Layout = layout;
        Range = range;
        InheritedStyle = inheritedStyle;
    }

    /// <summary>Gets the chart contract.</summary>
    internal IChartControl Chart { get; }

    /// <summary>Gets the resolved plot layout.</summary>
    public ChartPlotLayout Layout { get; }

    /// <summary>Gets the resolved numeric range.</summary>
    public ChartScaleRange Range { get; }

    /// <summary>Gets the inherited terminal cell style.</summary>
    public TerminalStyle InheritedStyle { get; }

    /// <summary>Maps a finite value to its clamped 0-1 position within <see cref="Range"/>.</summary>
    /// <param name="value">The value to normalize; need not itself lie within the range.</param>
    /// <returns>The clamped position, where 0 is <see cref="ChartScaleRange.Minimum"/> and 1 is
    /// <see cref="ChartScaleRange.Maximum"/>.</returns>
    [Pure]
    public double Ratio(double value) => ChartRenderer.Ratio(Range, value);

    /// <summary>Maps one ordered point index to an inclusive horizontal cell within
    /// <see cref="Layout"/>'s plot rectangle.</summary>
    /// <param name="index">The zero-based ordinal position of the point along its series.</param>
    /// <param name="count">The total number of points sharing this horizontal domain.</param>
    /// <returns>The plot-relative absolute column. Categories are ordinal, not scaled, so the
    /// mapping divides the plot width evenly by <paramref name="count"/> rather than resolving a
    /// numeric range the way <see cref="MapY(double)"/> does.</returns>
    [Pure]
    public int MapX(int index, int count) => ChartRenderer.MapX(index, count, Layout.Plot);

    /// <summary>Maps a finite value to an inclusive vertical cell within <see cref="Layout"/>'s
    /// plot rectangle, using <see cref="Range"/>.</summary>
    /// <param name="value">The value to map; need not itself lie within the range.</param>
    /// <returns>The plot-relative absolute row.</returns>
    [Pure]
    public int MapY(double value) => ChartRenderer.MapY(Range, value, Layout.Plot);

    /// <summary>Creates the terminal style one series' point renders with, resolving its color
    /// precedence and, when the point is the current selection, its selection decoration.</summary>
    /// <param name="series">The series the point belongs to.</param>
    /// <param name="point">The point whose resolved color and selection state are painted.</param>
    /// <param name="seriesIndex">The zero-based index of <paramref name="series"/> within
    /// <see cref="IChartControl.Series"/>.</param>
    /// <param name="pointIndex">The zero-based index of <paramref name="point"/> within
    /// <paramref name="series"/>, or -1 to skip selection-decoration resolution entirely - for
    /// example, a continuous fill style that paints between points rather than at one.</param>
    /// <returns>The resolved style, including selection decoration when selected.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="series"/> or <paramref name="point"/>
    /// is null.</exception>
    [Pure]
    public TerminalStyle ResolveSeriesStyle(
        ChartSeries series,
        ChartDataPoint point,
        int seriesIndex,
        int pointIndex = -1)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(point);
        return ChartRenderer.ResolveSeriesStyle(this, series, point, seriesIndex, pointIndex);
    }
}
