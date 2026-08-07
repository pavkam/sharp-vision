// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents filled trends, multiple series, explicit scaling, and live mutation.</summary>
internal sealed class AreaChartPane: CompositeControlBase
{
    /// <summary>The exact catalog and page name.</summary>
    internal const string Title = "AreaChart";

    /// <summary>Initializes the retained filled-trend page.</summary>
    internal AreaChartPane()
    {
        var requests = new ChartSeries("Requests", [
            new ChartDataPoint("A", 102), new ChartDataPoint("B", 108),
            new ChartDataPoint("C", 105), new ChartDataPoint("D", 112)]);
        var latency = new ChartSeries("Latency", [
            new ChartDataPoint("A", 101), new ChartDataPoint("B", 104),
            new ChartDataPoint("C", 107), new ChartDataPoint("D", 109)])
        {
            Color = new ControlColor(SemanticColor.Warning)
        };
        var shared = new AreaChart
        {
            Width = Length.Cells(54),
            Height = Length.Cells(10),
            Series = [requests, latency]
        };
        var status = new Text("Latest requests: 112");
        var add = new Button { Text = "&Add data" };
        add.Click += (_, _) =>
        {
            requests.Points[^1].Value += 2;
            status.Content = FormattableString.Invariant($"Latest requests: {requests.Points[^1].Value:G}");
        };

        var signed = new AreaChart
        {
            Width = Length.Cells(54),
            Height = Length.Cells(9),
            Series = [new ChartSeries("Delta", [
                new ChartDataPoint("A", -3), new ChartDataPoint("B", 4),
                new ChartDataPoint("C", -1), new ChartDataPoint("D", 8)])],
            Scale = new ChartScale(-5, 10, includeZero: true),
            ShowValueLabels = true
        };

        InitializeContent(new DocPage(
            Title,
            "<info>AreaChart</info> fills ordered series toward zero or the nearest visible scale edge.",
            new DocSection(
                "▰",
                "Filled trends and baselines",
                "Theme-aware fills share a resolved scale and react to retained point mutations.",
                new DocExample(
                    "Requests and latency",
                    "Both series keep distinct fills and spaced legend markers; Add data mutates Requests.",
                    new DocColumn(shared, new ChartActionRow(add, status)),
                    "var chart = new AreaChart { Series = [requests, latency] };\nrequests.Points[^1].Value += 2;"),
                new DocExample(
                    "Signed delta",
                    "An explicit -5..10 scale makes the zero baseline and signed fill direction predictable.",
                    signed,
                    "chart.Scale = new ChartScale(-5, 10, includeZero: true);\nchart.ShowValueLabels = true;"))));
    }
}
