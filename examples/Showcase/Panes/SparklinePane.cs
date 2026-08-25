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
        var series = new ChartSeries("Load", [
            new ChartDataPoint("1", 2), new ChartDataPoint("2", 5),
            new ChartDataPoint("3", 3), new ChartDataPoint("4", 7),
            new ChartDataPoint("5", 6), new ChartDataPoint("6", 9)]);
        var compact = new Sparkline { Width = Length.Cells(18), Height = Length.Cells(2), Series = [series] };
        var wide = new Sparkline { Width = Length.Cells(42), Height = Length.Cells(4), Series = [series] };
        var loadKey = new Text("<blue>■</blue> Load");
        var status = new Text("Latest sample: 9");
        var add = new Button { Text = "&Add data" };
        add.Click += (_, _) =>
        {
            var next = series.Points[^1].Value >= 10 ? 1 : series.Points[^1].Value + 1;
            series.Points.Add(new ChartDataPoint((series.Points.Count + 1).ToString(CultureInfo.InvariantCulture), next));
            status.Content = FormattableString.Invariant($"Latest sample: {next:G}");
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
                    "Only recent points that fit are drawn; Add data appends through the observed collection.",
                    new DocColumn(loadKey, compact, wide, new ChartActionRow(add, status)),
                    "var sparkline = new Sparkline { Series = [load] };\nload.Points.Add(nextPoint);"),
                new DocExample(
                    "Availability band",
                    "A 90..100 scale and warning color magnify small availability changes.",
                    new DocColumn(availabilityKey, bounded),
                    "sparkline.Scale = new ChartScale(90, 100, includeZero: false);\nseries.Color = new ControlColor(SemanticColor.Warning);"))));
    }
}
