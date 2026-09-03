// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the ProgressBar control with static, ranged, oriented, and interactive specimens.</summary>
internal sealed class ProgressBarPane: CompositeControlBase
{
    internal ProgressBarPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "ProgressBar";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        var empty = new ProgressBar { Width = Length.Cells(30), Maximum = 100, Value = 0 };
        var half = new ProgressBar { Width = Length.Cells(30), Maximum = 100, Value = 50 };
        var full = new ProgressBar { Width = Length.Cells(30), Maximum = 100, Value = 100 };
        var partial = new ProgressBar { Width = Length.Cells(30), Maximum = 100, Value = 42 };
        var indeterminate = new ProgressBar { Width = Length.Cells(30), IsIndeterminate = true };

        var rangeBar = new ProgressBar { Width = Length.Cells(30), Minimum = 0, Maximum = 200, Value = 150 };
        var rangeStatus = new Text($"Minimum: 0, Maximum: 200, Value: {rangeBar.Value:0}");

        var verticalBar = new ProgressBar
        {
            Orientation = Orientation.Vertical,
            Height = Length.Cells(8),
            Maximum = 100,
            Value = 65
        };
        var horizontalComparison = new ProgressBar { Width = Length.Cells(20), Maximum = 100, Value = 65 };

        var framed = new ProgressBar
        {
            Width = Length.Cells(30),
            Height = Length.Cells(3),
            Maximum = 100,
            Value = 62,
            Padding = new Thickness(1, 0),
            Style = ProgressBarStyle.Default with
            {
                Border = new Border(
                    BorderSide.All,
                    BorderGlyphStyle.Rounded,
                    SemanticColor.ControlBorder,
                    Color.Transparent,
                    SemanticDecoration.Border),
                FillColor = Color.Rgb(0x50, 0xd8, 0x90),
                TrackColor = Color.Rgb(0x3f, 0x4a, 0x54),
                Glyphs = new ProgressBarGlyphs(new Rune('#'), new Rune('.'), new Rune('?'))
            }
        };

        var interactiveBar = new ProgressBar { Width = Length.Cells(30), Maximum = 10, Value = 3 };
        var interactiveStatus = new Text($"Live progress: {interactiveBar.Value:0} / 10");
        var increase = new Button { Text = "&Advance progress" };
        var reset = new Button { Text = "&Reset" };
        increase.Click += (_, _) =>
        {
            interactiveBar.Value = Math.Min(10, interactiveBar.Value + 1);
            interactiveStatus.Content = $"Live progress: {interactiveBar.Value:0} / 10";
        };
        reset.Click += (_, _) =>
        {
            interactiveBar.Value = 0;
            interactiveStatus.Content = $"Live progress: {interactiveBar.Value:0} / 10";
        };

        return new DocPage(
            Title,
            "<info>ProgressBar</info> displays a non-interactive visual progress indicator using filled and empty block characters.",
            new DocSection(
                "📊",
                "Basic progress",
                "Five indicators at fixed values show the visual range from empty through indeterminate.",
                new DocExample(
                    "Zero, half, and full",
                    "Each bar uses the default 0..100 range with a different <info>Value</info>.",
                    new DocColumn(
                        new DocRow(ShowcasePaneHelpers.DimCaption("  0%"), empty),
                        new DocRow(ShowcasePaneHelpers.DimCaption(" 42%"), partial),
                        new DocRow(ShowcasePaneHelpers.DimCaption(" 50%"), half),
                        new DocRow(ShowcasePaneHelpers.DimCaption("100%"), full),
                        new DocRow(ShowcasePaneHelpers.DimCaption("  ??"), indeterminate)),
                    "var bar = new ProgressBar { Value = 50 };")),
            new DocSection(
                "📐",
                "Custom range",
                "Override the default <info>Minimum</info> and <info>Maximum</info> to map progress onto any numeric domain.",
                new DocExample(
                    "Extended range",
                    "A bar spanning 0..200 with a current value of 150 fills three quarters of its track.",
                    new DocColumn(rangeBar, rangeStatus),
                    "var bar = new ProgressBar\n{\n    Minimum = 0,\n    Maximum = 200,\n    Value = 150,\n};")),
            new DocSection(
                "↕️",
                "Vertical orientation",
                "Set <info>Orientation</info> to <info>Vertical</info> for a column-shaped indicator that fills from bottom to top.",
                new DocExample(
                    "Side-by-side comparison",
                    "The same <info>Value</info> renders vertically and horizontally for layout flexibility.",
                    new DocRow(verticalBar, horizontalComparison))),
            new DocSection(
                "▶️",
                "Interactive",
                "Wire buttons to the <info>Value</info> property and observe the bar update in real time.",
                new DocExample(
                    "Increment and reset",
                    "Increase adds one step on each click. Reset returns the bar to zero.",
                    new DocColumn(interactiveBar, new DocRow(increase, reset), interactiveStatus),
                    "increase.Click += (_, _) =>\n{\n    bar.Value = Math.Min(10, bar.Value + 1);\n};")),
            new DocSection(
                "🔬",
                "Sub-cell resolution",
                "UseSubCellResolution renders with fractional block characters (▏▎▍▌▋▊▉█ horizontal, ▁▂▃▄▅▆▇█ vertical), providing 8 levels per cell for smoother progress indication.",
                new DocExample(
                    "Standard versus sub-cell",
                    "Both bars show the same 33% value. The sub-cell bar renders a partial fill in the fourth cell instead of rounding to whole cells.",
                    new DocColumn(
                        new DocRow(ShowcasePaneHelpers.DimCaption("Standard "),
                            new ProgressBar { Width = Length.Cells(20), Maximum = 100, Value = 33 }),
                        new DocRow(ShowcasePaneHelpers.DimCaption("Sub-cell "),
                            new ProgressBar
                            {
                                Width = Length.Cells(20),
                                Maximum = 100,
                                Value = 33,
                                UseSubCellResolution = true
                            })),
                    "bar.UseSubCellResolution = true;")),
            new DocSection(
                "🎨",
                "Presentation",
                "A complete <info>ProgressBarStyle</info> customizes fill, track, unknown-state glyphs, colors, and intrinsic chrome. Border and padding reserve cells around the track.",
                new DocExample(
                    "Framed custom track",
                    "The ASCII track stays inside one-cell padding and a rounded border instead of painting underneath either.",
                    framed,
                    "bar.Padding = new Thickness(1, 0);\nbar.Style = ProgressBarStyle.Default with\n{\n    Border = new Border(\n        BorderSide.All,\n        BorderGlyphStyle.Rounded,\n        SemanticColor.ControlBorder,\n        Color.Transparent,\n        SemanticDecoration.Border),\n    FillColor = Color.Rgb(0x50, 0xD8, 0x90),\n    TrackColor = Color.Rgb(0x3F, 0x4A, 0x54),\n    Glyphs = new ProgressBarGlyphs(new Rune('#'), new Rune('.'), new Rune('?')),\n};")));
    }
}
