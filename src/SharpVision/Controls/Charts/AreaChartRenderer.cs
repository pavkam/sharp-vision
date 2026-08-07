// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Renders filled area series toward the visible zero baseline or nearest plot edge.</summary>
internal static class AreaChartRenderer
{
    /// <summary>Renders every filled area and its connected line.</summary>
    internal static void Render(IChartControl chart, TerminalCanvas canvas, TerminalStyle inheritedStyle)
    {
        var context = ChartRenderer.CreateContext(chart, canvas, inheritedStyle);
        var plot = ChartRenderer.ReserveHorizontalCategoryLabels(context, canvas);

        if (plot.Width == 0 || plot.Height == 0)
        {
            return;
        }

        var baseline = context.Range.Minimum <= 0 && context.Range.Maximum >= 0
            ? ChartRenderer.MapY(context.Range, 0, plot)
            : context.Range.Minimum > 0 ? plot.Bottom - 1 : plot.Y;

        for (var seriesIndex = 0; seriesIndex < chart.Series.Count; seriesIndex++)
        {
            var series = chart.Series[seriesIndex];
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
                        chart.ActualStyle.Glyphs.Area,
                        style);
                }
                else if (current.Y > baseline)
                {
                    canvas.DrawLine(
                        new Point(current.X, baseline),
                        new Point(current.X, current.Y - 1),
                        chart.ActualStyle.Glyphs.Area,
                        style);
                }

                if (previous is { } start)
                {
                    canvas.DrawLine(start, current, chart.ActualStyle.Glyphs.Line, style);
                }

                previous = current;
            }

            for (var pointIndex = 0; pointIndex < series.Points.Count; pointIndex++)
            {
                var point = series.Points[pointIndex];
                var current = new Point(
                    ChartRenderer.MapX(pointIndex, series.Points.Count, plot),
                    ChartRenderer.MapY(context.Range, point.Value, plot));
                canvas.DrawRune(
                    chart.ActualStyle.Glyphs.Point,
                    current,
                    ChartRenderer.ResolveSeriesStyle(context, series, point, seriesIndex),
                    BackgroundMode.Transparent);
                ChartRenderer.RenderValueLabel(context, canvas, point, current, plot);
            }
        }
    }
}
