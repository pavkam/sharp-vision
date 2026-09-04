// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Renders grouped horizontal and vertical bar-chart cells.</summary>
internal static class BarChartRenderer
{
    /// <summary>Maps one plot cell to the nearest visible bar lane.</summary>
    internal static bool TryHitTestSelection(
        IChartControl chart,
        Point position,
        Orientation orientation,
        out ChartSelection selection)
    {
        var plot = ResolvePlot(chart, orientation);
        var categoryCount = GetCategoryCount(chart.Series);

        if (!plot.Contains(position) || categoryCount == 0)
        {
            selection = default;
            return false;
        }

        var category = FindCategory(
            orientation == Orientation.Horizontal ? position.Y : position.X,
            categoryCount,
            orientation == Orientation.Horizontal ? plot.Y : plot.X,
            orientation == Orientation.Horizontal ? plot.Height : plot.Width);
        var band = CategoryBand(
            category,
            categoryCount,
            orientation == Orientation.Horizontal ? plot.Y : plot.X,
            orientation == Orientation.Horizontal ? plot.Height : plot.Width);
        var fractional = chart.ActualStyle.FillMode == ChartFillMode.Fractional;
        var thickness = LaneThickness(band, chart.Series.Count, fractional);
        var coordinate = orientation == Orientation.Horizontal ? position.Y : position.X;
        var bestDistance = int.MaxValue;
        selection = default;
        var found = false;

        for (var seriesIndex = 0; seriesIndex < chart.Series.Count; seriesIndex++)
        {
            if (category >= chart.Series[seriesIndex].Points.Count || seriesIndex >= band.Length)
            {
                continue;
            }

            var lane = fractional
                ? band.Start + (seriesIndex * thickness) + (thickness / 2)
                : PlaceInBand(band, seriesIndex, chart.Series.Count);
            var distance = Math.Abs(coordinate - lane);

            if (found && distance >= bestDistance)
            {
                continue;
            }

            found = true;
            bestDistance = distance;
            selection = new ChartSelection(seriesIndex, category);
        }

        return found;
    }

    /// <summary>Renders one grouped bar chart in the requested orientation.</summary>
    internal static void Render(
        IChartControl chart,
        TerminalCanvas canvas,
        TerminalStyle inheritedStyle,
        Orientation orientation)
    {
        var context = ChartRenderer.CreateContext(chart, canvas, inheritedStyle);
        var plot = ReserveLabels(context, canvas, orientation);

        if (plot.Width == 0 || plot.Height == 0 || chart.Series.Count == 0)
        {
            return;
        }

        var categoryCount = GetCategoryCount(chart.Series);

        if (categoryCount == 0)
        {
            return;
        }

        if (orientation == Orientation.Horizontal)
        {
            RenderHorizontal(context, canvas, plot, categoryCount);
        }
        else
        {
            RenderVertical(context, canvas, plot, categoryCount);
        }
    }

    private static void RenderHorizontal(
        ChartRenderContext context,
        TerminalCanvas canvas,
        Rect plot,
        int categoryCount)
    {
        var range = context.Range;
        var zero = BoundaryX(range, 0, plot);
        var fractional = context.Chart.ActualStyle.FillMode == ChartFillMode.Fractional;

        if (context.Chart.ShowZeroAxis && zero > plot.X && zero < plot.Right)
        {
            canvas.DrawLine(
                new Point(zero, plot.Y),
                new Point(zero, plot.Bottom - 1),
                ChartRenderer.ResolveVerticalAxisGlyph(context),
                ChartRenderer.ResolveAxisStyle(context));
        }

        for (var category = 0; category < categoryCount; category++)
        {
            var band = CategoryBand(category, categoryCount, plot.Y, plot.Height);
            var thickness = LaneThickness(band, context.Chart.Series.Count, fractional);

            for (var seriesIndex = 0; seriesIndex < context.Chart.Series.Count; seriesIndex++)
            {
                var series = context.Chart.Series[seriesIndex];

                if (category >= series.Points.Count || seriesIndex >= band.Length)
                {
                    continue;
                }

                var point = series.Points[category];
                var style = ChartRenderer.ResolveSeriesStyle(context, series, point, seriesIndex, category);
                var value = BoundaryX(range, point.Value, plot);
                var left = Math.Min(zero, value);
                var right = Math.Max(zero, value);

                if (fractional)
                {
                    var eighths = ChartRenderer.ExtentEighths(range, point.Value, plot.Width, zero - plot.X);
                    var lane = band.Start + (seriesIndex * thickness);

                    for (var row = lane; row < lane + thickness && row < band.Start + band.Length; row++)
                    {
                        if (point.Value >= 0)
                        {
                            canvas.DrawBar(new Point(zero, row), BarDirection.Right, eighths, style);
                        }
                        else
                        {
                            canvas.DrawBar(new Point(zero - 1, row), BarDirection.Left, eighths, style);
                        }
                    }

                    left = point.Value >= 0 ? zero : zero - ChartRenderer.CeilEighthsToCells(eighths);
                    right = point.Value >= 0 ? zero.Add(ChartRenderer.CeilEighthsToCells(eighths)) : zero;
                    RenderHorizontalValueLabel(
                        context,
                        canvas,
                        point,
                        left,
                        right,
                        Math.Min(lane + (thickness / 2), band.Start + band.Length - 1),
                        plot);
                    RenderSelectionMarker(
                        context,
                        canvas,
                        seriesIndex,
                        category,
                        new Point(
                            HorizontalEndpoint(point.Value, left, right, zero, plot),
                            Math.Min(lane + (thickness / 2), band.Start + band.Length - 1)),
                        style);
                    continue;
                }

                var y = PlaceInBand(band, seriesIndex, context.Chart.Series.Count);

                if (right > left)
                {
                    canvas.DrawLine(
                        new Point(left, y),
                        new Point(right - 1, y),
                        context.Chart.ActualStyle.Glyphs.Bar,
                        style);
                }

                RenderHorizontalValueLabel(context, canvas, point, left, right, y, plot);
                RenderSelectionMarker(
                    context,
                    canvas,
                    seriesIndex,
                    category,
                    new Point(HorizontalEndpoint(point.Value, left, right, zero, plot), y),
                    style);
            }
        }
    }

    private static void RenderVertical(
        ChartRenderContext context,
        TerminalCanvas canvas,
        Rect plot,
        int categoryCount)
    {
        var range = context.Range;
        var zero = BoundaryY(range, 0, plot);
        var fractional = context.Chart.ActualStyle.FillMode == ChartFillMode.Fractional;

        if (context.Chart.ShowZeroAxis &&
            range.Minimum < 0 &&
            range.Maximum > 0 &&
            zero >= plot.Y &&
            zero < plot.Bottom)
        {
            canvas.DrawLine(
                new Point(plot.X, zero),
                new Point(plot.Right - 1, zero),
                ChartRenderer.ResolveHorizontalAxisGlyph(context),
                ChartRenderer.ResolveAxisStyle(context));
        }

        for (var category = 0; category < categoryCount; category++)
        {
            var band = CategoryBand(category, categoryCount, plot.X, plot.Width);
            var thickness = LaneThickness(band, context.Chart.Series.Count, fractional);

            for (var seriesIndex = 0; seriesIndex < context.Chart.Series.Count; seriesIndex++)
            {
                var series = context.Chart.Series[seriesIndex];

                if (category >= series.Points.Count || seriesIndex >= band.Length)
                {
                    continue;
                }

                var point = series.Points[category];
                var style = ChartRenderer.ResolveSeriesStyle(context, series, point, seriesIndex, category);
                var value = BoundaryY(range, point.Value, plot);
                var top = Math.Min(zero, value);

                if (fractional)
                {
                    var eighths = ChartRenderer.ExtentEighths(range, point.Value, plot.Height, plot.Bottom - zero);
                    var lane = band.Start + (seriesIndex * thickness);

                    for (var column = lane; column < lane + thickness && column < band.Start + band.Length; column++)
                    {
                        if (point.Value >= 0)
                        {
                            canvas.DrawBar(new Point(column, zero - 1), BarDirection.Up, eighths, style);
                        }
                        else
                        {
                            canvas.DrawBar(new Point(column, zero), BarDirection.Down, eighths, style);
                        }
                    }

                    top = point.Value >= 0 ? zero - ChartRenderer.CeilEighthsToCells(eighths) : zero;
                    RenderValueLabel(
                        context,
                        canvas,
                        point,
                        new Point(Math.Min(lane + (thickness / 2), band.Start + band.Length - 1), Math.Max(plot.Y, top - 1)),
                        plot);
                    RenderSelectionMarker(
                        context,
                        canvas,
                        seriesIndex,
                        category,
                        new Point(
                            Math.Min(lane + (thickness / 2), band.Start + band.Length - 1),
                            VerticalEndpoint(point.Value, top, zero, ChartRenderer.CeilEighthsToCells(eighths), plot)),
                        style);
                    continue;
                }

                var x = PlaceInBand(band, seriesIndex, context.Chart.Series.Count);
                var bottom = Math.Max(zero, value);

                if (bottom > top)
                {
                    canvas.DrawLine(
                        new Point(x, top),
                        new Point(x, bottom - 1),
                        context.Chart.ActualStyle.Glyphs.Bar,
                        style);
                }

                RenderValueLabel(context, canvas, point, new Point(x, Math.Max(plot.Y, top - 1)), plot);
                RenderSelectionMarker(
                    context,
                    canvas,
                    seriesIndex,
                    category,
                    new Point(x, VerticalEndpoint(point.Value, top, zero, bottom - top, plot)),
                    style);
            }
        }
    }

    [Pure]
    private static int HorizontalEndpoint(double value, int left, int right, int zero, Rect plot)
    {
        var endpoint = value switch
        {
            > 0 => right - 1,
            < 0 => left,
            _ => zero
        };

        return Math.Clamp(endpoint, plot.X, plot.Right - 1);
    }

    [Pure]
    private static int VerticalEndpoint(double value, int top, int zero, int extent, Rect plot)
    {
        var endpoint = value switch
        {
            > 0 => top,
            < 0 => zero + Math.Max(0, extent - 1),
            _ => zero
        };

        return Math.Clamp(endpoint, plot.Y, plot.Bottom - 1);
    }

    private static void RenderSelectionMarker(
        ChartRenderContext context,
        TerminalCanvas canvas,
        int seriesIndex,
        int pointIndex,
        Point position,
        TerminalStyle style)
    {
        if (context.Chart.Selection != new ChartSelection(seriesIndex, pointIndex))
        {
            return;
        }

        canvas.DrawRune(
            context.Chart.ActualStyle.Glyphs.Point,
            position,
            style,
            BackgroundMode.Transparent);
    }

    // Fractional bars fill their band instead of occupying one centered cell: the series divide
    // the band evenly, and a band wide enough to afford it keeps its final cell as a gutter so
    // adjacent categories stay visually separate. The glyph mode keeps the historical one-cell
    // lanes, whose geometry existing themes and tests rely on.
    [Pure]
    private static int LaneThickness((int Start, int Length) band, int seriesCount, bool fractional)
    {
        if (!fractional)
        {
            return 1;
        }

        var usable = band.Length > seriesCount ? band.Length - 1 : band.Length;
        return Math.Max(1, usable / Math.Max(1, seriesCount));
    }

    private static Rect ReserveLabels(
        ChartRenderContext context,
        TerminalCanvas canvas,
        Orientation orientation)
    {
        var plot = context.Layout.Plot;

        if (!context.Chart.ShowCategoryLabels || context.Chart.Series.Count == 0 ||
            context.Chart.Series[0].Points.Count == 0)
        {
            return plot;
        }

        if (orientation == Orientation.Horizontal && plot.Width >= 8)
        {
            var labelWidth = 0;

            foreach (var point in context.Chart.Series[0].Points)
            {
                labelWidth = Math.Max(labelWidth, context.Chart.Control.MeasureCells(point.Label.AsSpan()));
            }

            labelWidth = Math.Min(labelWidth, Math.Max(0, plot.Width / 3));

            if (labelWidth > 0)
            {
                var count = context.Chart.Series[0].Points.Count;

                for (var index = 0; index < count; index++)
                {
                    var band = CategoryBand(index, count, plot.Y, plot.Height);

                    if (band.Length == 0)
                    {
                        continue;
                    }

                    var y = HorizontalCategoryLabelY(
                        band,
                        context.Chart.Series.Count,
                        context.Chart.ActualStyle.FillMode == ChartFillMode.Fractional);
                    _ = canvas.Clip(new Rect(plot.X, y, labelWidth, 1)).Draw(
                        context.Chart.Series[0].Points[index].Label.AsSpan(),
                        new Point(plot.X, y),
                        ChartRenderer.ResolveLabelStyle(context),
                        background: BackgroundMode.Transparent);
                }

                var axisX = plot.X.Add(labelWidth);
                canvas.DrawLine(
                    new Point(axisX, plot.Y),
                    new Point(axisX, plot.Bottom - 1),
                    ChartRenderer.ResolveVerticalAxisGlyph(context),
                    ChartRenderer.ResolveAxisStyle(context));
                return new Rect(plot.X.Add(labelWidth + 1), plot.Y, Math.Max(0, plot.Width - labelWidth - 1), plot.Height);
            }
        }

        if (orientation == Orientation.Vertical && plot.Height >= 2)
        {
            var count = context.Chart.Series[0].Points.Count;

            for (var index = 0; index < count; index++)
            {
                var band = CategoryBand(index, count, plot.X, plot.Width);

                if (band.Length == 0)
                {
                    continue;
                }

                var x = band.Start + (band.Length / 2);
                ChartRenderer.RenderCenteredLabel(
                    context,
                    canvas,
                    context.Chart.Series[0].Points[index].Label,
                    x,
                    plot.Bottom - 1,
                    new Rect(band.Start, plot.Bottom - 1, band.Length, 1));
            }

            if (plot.Height >= 3)
            {
                canvas.DrawLine(
                    new Point(plot.X, plot.Bottom - 2),
                    new Point(plot.Right - 1, plot.Bottom - 2),
                    ChartRenderer.ResolveHorizontalAxisGlyph(context),
                    ChartRenderer.ResolveAxisStyle(context));
                return new Rect(plot.X, plot.Y, plot.Width, plot.Height - 2);
            }

            return new Rect(plot.X, plot.Y, plot.Width, plot.Height - 1);
        }

        return plot;
    }

    [Pure]
    private static Rect ResolvePlot(IChartControl chart, Orientation orientation)
    {
        var plot = ChartRenderer.ResolveLayout(chart).Plot;

        if (!chart.ShowCategoryLabels || chart.Series.Count == 0 || chart.Series[0].Points.Count == 0)
        {
            return plot;
        }

        if (orientation == Orientation.Horizontal && plot.Width >= 8)
        {
            var labelWidth = 0;

            foreach (var point in chart.Series[0].Points)
            {
                labelWidth = Math.Max(labelWidth, chart.Control.MeasureCells(point.Label.AsSpan()));
            }

            labelWidth = Math.Min(labelWidth, Math.Max(0, plot.Width / 3));
            return labelWidth > 0
                ? new Rect(plot.X.Add(labelWidth + 1), plot.Y, Math.Max(0, plot.Width - labelWidth - 1), plot.Height)
                : plot;
        }

        return orientation == Orientation.Vertical && plot.Height >= 2
            ? plot.Height >= 3
                ? new Rect(plot.X, plot.Y, plot.Width, plot.Height - 2)
                : new Rect(plot.X, plot.Y, plot.Width, plot.Height - 1)
            : plot;
    }

    [Pure]
    private static int FindCategory(int coordinate, int count, int origin, int extent)
    {
        return extent <= 1
            ? 0
            : Math.Min(count - 1, (int) ((long) (coordinate - origin) * count / extent));
    }

    [Pure]
    private static int GetCategoryCount(IReadOnlyList<ChartSeries> series)
    {
        var count = 0;

        foreach (var item in series)
        {
            count = Math.Max(count, item.Points.Count);
        }

        return count;
    }

    // Fractional bars deliberately leave a trailing gutter in roomy category bands. Centering the
    // label on the whole band can therefore put it on blank space, so its coordinate follows the
    // occupied lane span instead. Glyph bars remain centered across the whole category band.
    [Pure]
    private static int HorizontalCategoryLabelY(
        (int Start, int Length) band,
        int seriesCount,
        bool fractional)
    {
        if (!fractional)
        {
            return band.Start + (band.Length / 2);
        }

        var visibleSeries = Math.Min(seriesCount, band.Length);
        var occupied = Math.Min(band.Length, LaneThickness(band, seriesCount, fractional) * visibleSeries);
        return band.Start + Math.Min(band.Length - 1, occupied / 2);
    }

    /// <summary>Maps one slot to its centered cell using overflow-safe proportional arithmetic.</summary>
    [Pure]
    internal static int CenterSlot(int slot, int slotCount, int origin, int extent) =>
        extent <= 1
            ? origin
            : origin.Add((int) Math.Min(
                (long) extent - 1,
                (((long) slot * 2) + 1) * extent / ((long) slotCount * 2)));

    // Each category owns a contiguous, disjoint cell band, so one category's bars can never land
    // on another's rows or columns and a category label always sits inside its own group. The old
    // global slot spread mapped two adjacent slots to one cell whenever the plot was shorter than
    // the slot count, and the later series then silently overdrew the earlier one - with the
    // labels placed independently, the label could end up naming a bar from a different group.
    /// <summary>Partitions an extent into one contiguous category band without intermediate overflow.</summary>
    [Pure]
    internal static (int Start, int Length) CategoryBand(int category, int categoryCount, int origin, int extent)
    {
        var start = origin.Add((int) ((long) category * extent / categoryCount));
        var end = origin.Add((int) ((long) (category + 1) * extent / categoryCount));
        return (start, Math.Max(0, end - start));
    }

    // Within a roomy band the series keep the historical centered spread; within a squeezed band
    // they pack adjacently and the caller drops the series past the band instead of overdrawing.
    [Pure]
    private static int PlaceInBand((int Start, int Length) band, int seriesIndex, int seriesCount) =>
        band.Length >= seriesCount
            ? band.Start + CenterSlot(seriesIndex, seriesCount, 0, band.Length)
            : band.Start + seriesIndex;

    [Pure]
    internal static int BoundaryX(ChartScaleRange range, double value, Rect plot)
    {
        var ratio = ChartRenderer.Ratio(range, value);
        return plot.X.Add((int) Math.Round(ratio * plot.Width, MidpointRounding.AwayFromZero));
    }

    [Pure]
    internal static int BoundaryY(ChartScaleRange range, double value, Rect plot)
    {
        var ratio = ChartRenderer.Ratio(range, value);
        return plot.Bottom - (int) Math.Round(ratio * plot.Height, MidpointRounding.AwayFromZero);
    }

    private static void RenderValueLabel(
        ChartRenderContext context,
        TerminalCanvas canvas,
        ChartDataPoint point,
        Point origin,
        Rect plot)
    {
        if (!context.Chart.ShowValueLabels || !plot.Contains(origin))
        {
            return;
        }

        var value = point.Value.ToString(context.Chart.ValueLabelFormat, CultureInfo.InvariantCulture);
        var width = context.Chart.Control.MeasureCells(value.AsSpan());

        // A numeric label that is merely clipped at the plot edge reads as a different number
        // ("10" cut to "1"), so a label wider than the plot is dropped outright and a narrower one
        // slides left just far enough to stay whole - the same rule the horizontal bars apply.
        if (width == 0 || width > plot.Width)
        {
            return;
        }

        var x = Math.Clamp(origin.X, plot.X, plot.Right - width);
        _ = canvas.Clip(plot).Draw(
            value.AsSpan(),
            new Point(x, origin.Y),
            ChartRenderer.ResolveLabelStyle(context),
            background: BackgroundMode.Transparent);
    }

    private static void RenderHorizontalValueLabel(
        ChartRenderContext context,
        TerminalCanvas canvas,
        ChartDataPoint point,
        int left,
        int right,
        int y,
        Rect plot)
    {
        if (!context.Chart.ShowValueLabels)
        {
            return;
        }

        var value = point.Value.ToString(context.Chart.ValueLabelFormat, CultureInfo.InvariantCulture);
        var width = context.Chart.Control.MeasureCells(value.AsSpan());

        if (width == 0 || width > plot.Width)
        {
            return;
        }

        // Labels never replace data cells: a clipped number can read as another value, while a
        // number painted over a bar destroys the very extent the chart exists to compare.
        var x = point.Value < 0 ? left - width - 1 : right.Add(1);

        if (x < plot.X || x.Add(width) > plot.Right)
        {
            return;
        }

        _ = canvas.Clip(plot).Draw(
            value.AsSpan(),
            new Point(x, y),
            ChartRenderer.ResolveLabelStyle(context),
            background: BackgroundMode.Transparent);
    }
}
