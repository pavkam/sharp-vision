// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Renders connected line-chart series through deterministic cell geometry.</summary>
internal static class LineChartRenderer
{
    /// <summary>Renders every line series and its visible point glyphs.</summary>
    internal static void Render(IChartControl chart, TerminalCanvas canvas, TerminalStyle inheritedStyle)
    {
        var context = ChartRenderer.CreateContext(chart, canvas, inheritedStyle);
        var plot = ChartRenderer.ReserveHorizontalCategoryLabels(context, canvas);

        if (plot.Width == 0 || plot.Height == 0)
        {
            return;
        }

        var quadrant = chart.ActualStyle.LineMode == ChartLineMode.Quadrant;

        for (var seriesIndex = 0; seriesIndex < chart.Series.Count; seriesIndex++)
        {
            var series = chart.Series[seriesIndex];
            Point? previous = null;
            Point? previousHalf = null;

            for (var pointIndex = 0; pointIndex < series.Points.Count; pointIndex++)
            {
                var point = series.Points[pointIndex];
                var half = ChartRenderer.MapHalf(context.Range, pointIndex, series.Points.Count, point.Value, plot);
                var current = new Point(half.X >> 1, half.Y >> 1);
                var style = ChartRenderer.ResolveSeriesStyle(context, series, point, seriesIndex);

                if (quadrant)
                {
                    if (previousHalf is { } startHalf)
                    {
                        canvas.DrawQuadrantLine(startHalf, half, style);
                    }
                }
                else if (previous is { } start)
                {
                    canvas.DrawLine(start, current, chart.ActualStyle.Glyphs.Line, style);
                }

                previous = current;
                previousHalf = half;
            }

            // Markers draw after every segment of the series so a point stays visible where
            // segments meet, whichever rasterization drew them.
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
}
