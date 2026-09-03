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

        ChartRenderer.RenderHorizontalZeroAxis(context, canvas, plot);

        var quadrant = chart.ActualStyle.LineMode == ChartLineMode.Quadrant;

        for (var seriesIndex = 0; seriesIndex < chart.Series.Count; seriesIndex++)
        {
            var series = chart.Series[seriesIndex];

            // The dash pattern resolves once per series, not per point, so its on/off run-length
            // phase can advance continuously across every segment of the series polyline instead
            // of restarting (and potentially misaligning) at each point.
            var pattern = ChartRenderer.ResolveSeriesPattern(chart.ActualStyle, series);
            var patternStep = 0;
            Point? previous = null;
            Point? previousHalf = null;

            for (var pointIndex = 0; pointIndex < series.Points.Count; pointIndex++)
            {
                var point = series.Points[pointIndex];
                var half = ChartRenderer.MapHalf(context.Range, pointIndex, series.Points.Count, point.Value, plot);
                var current = new Point(
                    ChartRenderer.MapX(pointIndex, series.Points.Count, plot),
                    ChartRenderer.MapY(context.Range, point.Value, plot));
                var style = ChartRenderer.ResolveSeriesStyle(context, series, point, seriesIndex);

                if (quadrant)
                {
                    if (previousHalf is { } startHalf)
                    {
                        patternStep = canvas.DrawQuadrantLine(startHalf, half, pattern, patternStep, style);
                    }
                }
                else if (previous is { } start)
                {
                    // Glyph mode is deliberately unaffected by the resolved pattern - a theme
                    // that replaces the line glyph already owns this mode's appearance.
                    canvas.DrawLine(start, current, chart.ActualStyle.Glyphs.Line, style);
                }

                previous = current;
                previousHalf = half;
            }

            // Markers draw after every segment of the series so a point stays visible where
            // segments meet, whichever rasterization drew them.
            ChartRenderer.RenderPointMarkers(context, canvas, plot, series, seriesIndex);
        }
    }
}
