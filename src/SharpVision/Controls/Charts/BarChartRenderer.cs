// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Renders grouped horizontal and vertical bar-chart cells.</summary>
internal static class BarChartRenderer
{
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

        if (zero > plot.X && zero < plot.Right)
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

            for (var seriesIndex = 0; seriesIndex < context.Chart.Series.Count; seriesIndex++)
            {
                var series = context.Chart.Series[seriesIndex];

                if (category >= series.Points.Count || seriesIndex >= band.Length)
                {
                    continue;
                }

                var point = series.Points[category];
                var y = PlaceInBand(band, seriesIndex, context.Chart.Series.Count);
                var value = BoundaryX(range, point.Value, plot);
                var left = Math.Min(zero, value);
                var right = Math.Max(zero, value);

                if (right > left)
                {
                    canvas.DrawLine(
                        new Point(left, y),
                        new Point(right - 1, y),
                        context.Chart.ActualStyle.Glyphs.Bar,
                        ChartRenderer.ResolveSeriesStyle(context, series, point, seriesIndex));
                }

                RenderHorizontalValueLabel(context, canvas, point, left, right, y, plot);
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

        for (var category = 0; category < categoryCount; category++)
        {
            var band = CategoryBand(category, categoryCount, plot.X, plot.Width);

            for (var seriesIndex = 0; seriesIndex < context.Chart.Series.Count; seriesIndex++)
            {
                var series = context.Chart.Series[seriesIndex];

                if (category >= series.Points.Count || seriesIndex >= band.Length)
                {
                    continue;
                }

                var point = series.Points[category];
                var x = PlaceInBand(band, seriesIndex, context.Chart.Series.Count);
                var value = BoundaryY(range, point.Value, plot);
                var top = Math.Min(zero, value);
                var bottom = Math.Max(zero, value);

                if (bottom > top)
                {
                    canvas.DrawLine(
                        new Point(x, top),
                        new Point(x, bottom - 1),
                        context.Chart.ActualStyle.Glyphs.Bar,
                        ChartRenderer.ResolveSeriesStyle(context, series, point, seriesIndex));
                }

                RenderValueLabel(context, canvas, point, new Point(x, Math.Max(plot.Y, top - 1)), plot);
            }
        }
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

                    var y = band.Start + (band.Length / 2);
                    _ = canvas.Clip(new Rect(plot.X, y, labelWidth, 1)).Draw(
                        context.Chart.Series[0].Points[index].Label.AsSpan(),
                        new Point(plot.X, y),
                        ChartRenderer.ResolveLabelStyle(context),
                        background: BackgroundMode.Transparent);
                }

                var axisX = plot.X + labelWidth;
                canvas.DrawLine(
                    new Point(axisX, plot.Y),
                    new Point(axisX, plot.Bottom - 1),
                    ChartRenderer.ResolveVerticalAxisGlyph(context),
                    ChartRenderer.ResolveAxisStyle(context));
                return new Rect(plot.X + labelWidth + 1, plot.Y, Math.Max(0, plot.Width - labelWidth - 1), plot.Height);
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
                    new Rect(plot.X, plot.Bottom - 1, plot.Width, 1));
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

    private static int GetCategoryCount(IReadOnlyList<ChartSeries> series)
    {
        var count = 0;

        foreach (var item in series)
        {
            count = Math.Max(count, item.Points.Count);
        }

        return count;
    }

    private static int CenterSlot(int slot, int slotCount, int origin, int extent) =>
        extent <= 1
            ? origin
            : origin + Math.Min(extent - 1, ((slot * 2) + 1) * extent / (slotCount * 2));

    // Each category owns a contiguous, disjoint cell band, so one category's bars can never land
    // on another's rows or columns and a category label always sits inside its own group. The old
    // global slot spread mapped two adjacent slots to one cell whenever the plot was shorter than
    // the slot count, and the later series then silently overdrew the earlier one - with the
    // labels placed independently, the label could end up naming a bar from a different group.
    private static (int Start, int Length) CategoryBand(int category, int categoryCount, int origin, int extent)
    {
        var start = origin + (category * extent / categoryCount);
        var end = origin + ((category + 1) * extent / categoryCount);
        return (start, end - start);
    }

    // Within a roomy band the series keep the historical centered spread; within a squeezed band
    // they pack adjacently and the caller drops the series past the band instead of overdrawing.
    private static int PlaceInBand((int Start, int Length) band, int seriesIndex, int seriesCount) =>
        band.Length >= seriesCount
            ? band.Start + CenterSlot(seriesIndex, seriesCount, 0, band.Length)
            : band.Start + seriesIndex;

    private static int BoundaryX(ChartScaleRange range, double value, Rect plot)
    {
        var ratio = Math.Clamp((value - range.Minimum) / (range.Maximum - range.Minimum), 0, 1);
        return plot.X + (int) Math.Round(ratio * plot.Width, MidpointRounding.AwayFromZero);
    }

    private static int BoundaryY(ChartScaleRange range, double value, Rect plot)
    {
        var ratio = Math.Clamp((value - range.Minimum) / (range.Maximum - range.Minimum), 0, 1);
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

        var value = point.Value.ToString("G", CultureInfo.InvariantCulture);
        _ = canvas.Clip(plot).Draw(
            value.AsSpan(),
            origin,
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

        var value = point.Value.ToString("G", CultureInfo.InvariantCulture);
        var width = context.Chart.Control.MeasureCells(value.AsSpan());
        var x = point.Value < 0 ? left - width - 1 : right + 1;

        if (width == 0 || x < plot.X || x + width > plot.Right)
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
