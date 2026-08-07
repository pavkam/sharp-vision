// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Renders filled area series toward the visible zero baseline or nearest plot edge.</summary>
internal static class AreaChartRenderer
{
    /// <summary>Renders every filled area and its data-point markers.</summary>
    internal static void Render(IChartControl chart, TerminalCanvas canvas, TerminalStyle inheritedStyle)
    {
        var context = ChartRenderer.CreateContext(chart, canvas, inheritedStyle);
        var plot = ChartRenderer.ReserveHorizontalCategoryLabels(context, canvas);

        if (plot.Width == 0 || plot.Height == 0)
        {
            return;
        }

        var fractional = chart.ActualStyle.FillMode == ChartFillMode.Fractional;

        for (var seriesIndex = 0; seriesIndex < chart.Series.Count; seriesIndex++)
        {
            var series = chart.Series[seriesIndex];

            if (fractional)
            {
                RenderContinuousFill(context, canvas, plot, series, seriesIndex);
            }
            else
            {
                RenderGlyphColumns(context, canvas, plot, series, seriesIndex);
            }

            for (var pointIndex = 0; pointIndex < series.Points.Count; pointIndex++)
            {
                var point = series.Points[pointIndex];
                var half = ChartRenderer.MapHalf(context.Range, pointIndex, series.Points.Count, point.Value, plot);
                var current = new Point(half.X >> 1, half.Y >> 1);
                canvas.DrawRune(
                    chart.ActualStyle.Glyphs.Point,
                    current,
                    ChartRenderer.ResolveSeriesStyle(context, series, point, seriesIndex),
                    BackgroundMode.Transparent);
                ChartRenderer.RenderValueLabel(context, canvas, point, current, plot);
            }
        }
    }

    // The fill used to exist only in the columns that carried a data point, leaving every column
    // between them blank - a comb, not an area. Interpolating the series linearly across the
    // domain fills every column, and rasterizing each column through the fractional bar gives the
    // fill a smooth eighth-cell silhouette that IS the series' top edge, so no separate line pass
    // is drawn over it.
    private static void RenderContinuousFill(
        ChartRenderContext context,
        TerminalCanvas canvas,
        Rect plot,
        ChartSeries series,
        int seriesIndex)
    {
        var count = series.Points.Count;

        if (count == 0)
        {
            return;
        }

        var range = context.Range;
        var zeroRatio = Math.Clamp((0 - range.Minimum) / (range.Maximum - range.Minimum), 0, 1);
        var zeroCells = (int) Math.Round(zeroRatio * plot.Height, MidpointRounding.AwayFromZero);
        var halfX = new double[count];
        var values = new double[count];

        for (var index = 0; index < count; index++)
        {
            halfX[index] = ChartRenderer.MapHalf(range, index, count, series.Points[index].Value, plot).X;
            values[index] = series.Points[index].Value;
        }

        var segment = 0;

        for (var x = plot.X; x < plot.Right; x++)
        {
            var center = (x * 2) + 0.5;

            if (center < halfX[0] - 1 || center > halfX[count - 1] + 1)
            {
                continue;
            }

            double value;

            if (count == 1)
            {
                value = values[0];
            }
            else
            {
                while (segment < count - 2 && halfX[segment + 1] < center)
                {
                    segment++;
                }

                var span = halfX[segment + 1] - halfX[segment];
                var ratio = span <= 0 ? 0 : Math.Clamp((center - halfX[segment]) / span, 0, 1);
                value = values[segment] + ((values[segment + 1] - values[segment]) * ratio);
            }

            var valueRatio = Math.Clamp((value - range.Minimum) / (range.Maximum - range.Minimum), 0, 1);
            var valueEighths = (int) Math.Round(valueRatio * plot.Height * 8, MidpointRounding.AwayFromZero);
            var eighths = Math.Abs(valueEighths - (zeroCells * 8));
            var style = ChartRenderer.ResolveSeriesStyle(
                context,
                series,
                series.Points[Math.Min(count - 1, segment + (count == 1 ? 0 : 1))],
                seriesIndex);

            if (value >= 0)
            {
                canvas.DrawBar(new Point(x, plot.Bottom - zeroCells - 1), BarDirection.Up, eighths, style);
            }
            else
            {
                canvas.DrawBar(new Point(x, plot.Bottom - zeroCells), BarDirection.Down, eighths, style);
            }
        }
    }

    // The historical glyph path a theme opts into by replacing the fill glyphs: whole-cell columns
    // under each data point, connected by whole-cell line segments.
    private static void RenderGlyphColumns(
        ChartRenderContext context,
        TerminalCanvas canvas,
        Rect plot,
        ChartSeries series,
        int seriesIndex)
    {
        var baseline = context.Range.Minimum <= 0 && context.Range.Maximum >= 0
            ? ChartRenderer.MapY(context.Range, 0, plot)
            : context.Range.Minimum > 0 ? plot.Bottom - 1 : plot.Y;
        Point? previous = null;

        for (var pointIndex = 0; pointIndex < series.Points.Count; pointIndex++)
        {
            var point = series.Points[pointIndex];
            var current = new Point(
                ChartRenderer.MapX(pointIndex, series.Points.Count, plot),
                ChartRenderer.MapY(context.Range, point.Value, plot));
            var style = ChartRenderer.ResolveSeriesStyle(context, series, point, seriesIndex);
            if (current.Y < baseline)
            {
                canvas.DrawLine(
                    new Point(current.X, current.Y + 1),
                    new Point(current.X, baseline),
                    context.Chart.ActualStyle.Glyphs.Area,
                    style);
            }
            else if (current.Y > baseline)
            {
                canvas.DrawLine(
                    new Point(current.X, baseline),
                    new Point(current.X, current.Y - 1),
                    context.Chart.ActualStyle.Glyphs.Area,
                    style);
            }

            if (previous is { } start)
            {
                canvas.DrawLine(start, current, context.Chart.ActualStyle.Glyphs.Line, style);
            }

            previous = current;
        }
    }
}
