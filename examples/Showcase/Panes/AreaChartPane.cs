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
        var requests = new ChartSeries("Requests", Wave(amplitude: 26, phase: 0.6, offset: 64));
        var latency = new ChartSeries("Latency", Wave(amplitude: 10, phase: 2.6, offset: 24))
        {
            Color = new ControlColor(SemanticColor.Warning)
        };
        var shared = new AreaChart
        {
            Width = Length.Percent(100),
            Height = Length.Cells(10),
            Series = [requests, latency],
            Selection = new ChartSelection(0, requests.Points.Count - 1)
        };
        var status = new Text();
        ChartSelectionPresenter.Connect(shared, status);
        var add = new Button { Text = "&Add data" };
        add.Click += (_, _) =>
        {
            requests.Points[^1].Value += 2;
            ChartSelectionPresenter.Refresh(shared, status);
        };

        var deltas = new ChartDataPoint[16];

        for (var index = 0; index < deltas.Length; index++)
        {
            deltas[index] = new ChartDataPoint(
                index % 4 == 0 ? FormattableString.Invariant($"Q{(index / 4) + 1}") : string.Empty,
                Math.Round(7.5 * Math.Sin((index * 0.85) + 0.4), 1));
        }

        var signed = new AreaChart
        {
            Width = Length.Percent(100),
            Height = Length.Cells(9),
            Series = [new ChartSeries("Delta", deltas)],
            Scale = new ChartScale(-8, 8, includeZero: true)
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
                    "Click the nearest marker or Tab to the chart; arrows move the selected point and series, while Esc clears. Add data mutates Requests.",
                    new DocColumn(shared, new ChartActionRow(add, status)),
                    "var chart = new AreaChart { Series = [requests, latency] };\nchart.SelectionChanged += ShowSelection;\nrequests.Points[^1].Value += 2;"),
                new DocExample(
                    "Signed delta",
                    "An explicit -8..8 scale makes the zero baseline and signed fill direction predictable.",
                    signed,
                    "chart.Scale = new ChartScale(-8, 8, includeZero: true);")),
            new DocSection(
                "🖌️",
                "Style customization",
                "<info>Style</info> assigns a complete <info>ChartStyle</info>. <info>FillMode</info> switches area extents from the default fractional-block rasterization to whole cells drawn with a custom fill glyph, and the color fields replace the axis, label, and series palette.",
                new DocExample(
                    "Glyph fill with a custom palette",
                    "A block-shaded glyph fill replaces the fractional default, paired with a warm series color.",
                    new AreaChart
                    {
                        Width = Length.Percent(100),
                        Height = Length.Cells(9),
                        Series = [new ChartSeries("Load", Wave(amplitude: 18, phase: 1.1, offset: 50))],
                        Style = new ChartStyle(
                            ControlStyle.Default.Face,
                            ControlStyle.Default.Border,
                            ControlStyle.Default.Shadow,
                            axisColor: Color.Rgb(0x80, 0x80, 0x80),
                            labelColor: Color.Rgb(0xe0, 0xe0, 0xe0),
                            primaryColor: Color.Rgb(0xff, 0x8c, 0x40),
                            secondaryColor: ChartStyle.Default.SecondaryColor,
                            tertiaryColor: ChartStyle.Default.TertiaryColor,
                            quaternaryColor: ChartStyle.Default.QuaternaryColor,
                            quinaryColor: ChartStyle.Default.QuinaryColor,
                            senaryColor: ChartStyle.Default.SenaryColor,
                            glyphs: ChartGlyphs.Default with { Area = new Rune('▓') })
                        {
                            FillMode = ChartFillMode.Glyph
                        }
                    },
                    "chart.Style = new ChartStyle(\n    face, border, shadow,\n    axisColor, labelColor, primaryColor, secondaryColor, tertiaryColor,\n    quaternaryColor, quinaryColor, senaryColor,\n    ChartGlyphs.Default with { Area = new Rune('▓') })\n{\n    FillMode = ChartFillMode.Glyph\n};"))));
    }

    // A 12-point waveform: sparse enough that the markers read as measurements, dense enough
    // that the interpolated fractional fill reads as a continuous mountain silhouette. Only
    // every third point is labeled to keep the category row clean.
    private static ChartDataPoint[] Wave(double amplitude, double phase, double offset)
    {
        var points = new ChartDataPoint[12];

        for (var index = 0; index < points.Length; index++)
        {
            points[index] = new ChartDataPoint(
                index % 3 == 0 ? FormattableString.Invariant($"{9 + (index / 3):00}") : string.Empty,
                offset + (amplitude * Math.Sin((index * 1.05) + phase)));
        }

        return points;
    }
}
