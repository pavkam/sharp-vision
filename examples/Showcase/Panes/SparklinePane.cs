// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using System.Globalization;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents compact responsive trends, explicit scales, and caller-driven mutation.</summary>
internal sealed class SparklinePane: CompositeControlBase
{
    /// <summary>The exact catalog and page name.</summary>
    internal const string Title = "Sparkline";

    /// <summary>Initializes the retained compact trend page.</summary>
    internal SparklinePane()
    {
        var series = new ChartSeries("Load", Samples(48));
        var compact = new Sparkline
        {
            Width = Length.Cells(18),
            Height = Length.Cells(2),
            Series = [series],
            Selection = new ChartSelection(0, series.Points.Count - 1)
        };
        var wide = new Sparkline { Width = Length.Cells(42), Height = Length.Cells(4), Series = [series] };
        var loadKey = new Text("<blue>■</blue> Load");
        var status = new Text();
        ChartSelectionPresenter.Connect(compact, status);
        var add = new Button { Text = "&Add data" };
        add.Click += (_, _) =>
        {
            var next = series.Points[^1].Value >= 10 ? 1 : series.Points[^1].Value + 1;
            series.Points.Add(new ChartDataPoint((series.Points.Count + 1).ToString(CultureInfo.InvariantCulture), next));
            compact.Selection = new ChartSelection(0, series.Points.Count - 1);
            ChartSelectionPresenter.Refresh(compact, status);
        };

        var bounded = new Sparkline
        {
            Width = Length.Cells(42),
            Height = Length.Cells(4),
            Series = [new ChartSeries("Availability", [
                new ChartDataPoint("1", 98), new ChartDataPoint("2", 95),
                new ChartDataPoint("3", 99), new ChartDataPoint("4", 97)])
            {
                Color = new ControlColor(SemanticColor.Warning)
            }],
            Scale = new ChartScale(90, 100, includeZero: false)
        };
        var availabilityKey = new Text("<warning>■</warning> Availability");

        InitializeContent(new DocPage(
            Title,
            "<info>Sparkline</info> compresses one recent trend into fractional block cells without built-in axes or a legend.",
            new DocSection(
                "▁▃▆█",
                "Responsive compact trends",
                "The same series adapts to available width, while explicit ranges highlight narrow operational bands.",
                new DocExample(
                    "Two responsive widths",
                    "The 18- and 42-cell views prove recent-window clipping. Click a compact column or Tab to it; Left/Right moves selection and Esc clears. Add data shifts both windows.",
                    new DocColumn(loadKey, compact, wide, new ChartActionRow(add, status)),
                    "var sparkline = new Sparkline { Series = [load] };\nsparkline.SelectionChanged += ShowSelection;\nload.Points.Add(nextPoint);"),
                new DocExample(
                    "Availability band",
                    "A 90..100 scale and warning color magnify small availability changes.",
                    new DocColumn(availabilityKey, bounded),
                    "sparkline.Scale = new ChartScale(90, 100, includeZero: false);\nseries.Color = new ControlColor(SemanticColor.Warning);"))));
    }

    /// <summary>Creates enough deterministic samples to exercise both recent-window widths.</summary>
    private static ChartDataPoint[] Samples(int count)
    {
        var points = new ChartDataPoint[count];

        for (var index = 0; index < points.Length; index++)
        {
            var value = 5.5 + (3.2 * Math.Sin(index * 0.62)) + (1.1 * Math.Cos(index * 0.19));
            points[index] = new ChartDataPoint(
                (index + 1).ToString(CultureInfo.InvariantCulture),
                Math.Round(Math.Clamp(value, 0, 10), 1));
        }

        return points;
    }
}
