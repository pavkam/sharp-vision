// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents horizontal grouped bars, mixed signs, colors, and live point mutation.</summary>
internal sealed class HorizontalBarChartPane: CompositeControlBase
{
    /// <summary>The exact catalog and page name.</summary>
    internal const string Title = "HorizontalBarChart";

    /// <summary>Initializes the retained live chart page.</summary>
    internal HorizontalBarChartPane()
    {
        var revenue = new ChartSeries("Revenue") { Color = new ControlColor(SemanticColor.Success) };
        revenue.Points.Add(new ChartDataPoint("North", 8));
        revenue.Points.Add(new ChartDataPoint("South", 3));
        revenue.Points.Add(new ChartDataPoint("West", 5));
        var positive = new HorizontalBarChart
        {
            Width = Length.Cells(46),
            Height = Length.Cells(5),
            Series = [revenue],
            ShowValueLabels = true,
            Selection = new ChartSelection(0, 0)
        };
        var status = new Text();
        ChartSelectionPresenter.Connect(positive, status);
        var add = new Button { Text = "&Add data" };
        add.Click += (_, _) =>
        {
            revenue.Points[0].Value = revenue.Points[0].Value >= 12 ? 2 : revenue.Points[0].Value + 2;
            ChartSelectionPresenter.Refresh(positive, status);
        };

        var signed = new ChartSeries("Balance", [
            new ChartDataPoint("North", 8),
            new ChartDataPoint("South", -3),
            new ChartDataPoint("West", 5)])
        {
            Color = new ControlColor(SemanticColor.Warning)
        };
        var mixed = new HorizontalBarChart
        {
            Width = Length.Cells(46),
            Height = Length.Cells(5),
            Series = [signed],
            Scale = new ChartScale(-5, 10, includeZero: true)
        };

        var current = new ChartSeries("Current", [
            new ChartDataPoint("North", 8),
            new ChartDataPoint("South", 6),
            new ChartDataPoint("West", 5)]);
        var target = new ChartSeries("Target", [
            new ChartDataPoint("North", 10),
            new ChartDataPoint("South", 8),
            new ChartDataPoint("West", 7)]);
        var grouped = new HorizontalBarChart
        {
            Width = Length.Cells(46),
            Height = Length.Cells(7),
            Series = [current, target],
            LegendPlacement = ChartLegendPlacement.Bottom
        };

        InitializeContent(new DocPage(
            Title,
            "<info>HorizontalBarChart</info> compares labeled values around an automatically scaled zero baseline.",
            new DocSection(
                "📊",
                "Axes, scales, and grouping",
                "Labels have a visible axis boundary; bars start at the plot origin or grow away from a mixed-sign zero baseline.",
                new DocExample(
                    "Positive regional revenue",
                    "Click a bar or Tab to the chart; Up/Down moves between regions, Home/End jumps, and Esc clears. Add data mutates the retained point.",
                    new DocColumn(positive, new ChartActionRow(add, status)),
                    "var chart = new HorizontalBarChart\n{\n    Series = [revenue],\n    ShowValueLabels = true,\n};\nchart.SelectionChanged += ShowSelection;"),
                new DocExample(
                    "Mixed signs",
                    "Negative and positive values grow in opposite directions from the visible zero baseline.",
                    mixed,
                    "chart.Scale = new ChartScale(-5, 10, includeZero: true);\nchart.ShowZeroAxis = true;"),
                new DocExample(
                    "Grouped targets",
                    "Explicit series colors and a spaced legend distinguish current values from targets.",
                    grouped,
                    "chart.Series = [current, target];\nchart.LegendPlacement = ChartLegendPlacement.Bottom;"))));
    }
}
