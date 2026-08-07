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
        var indeterminate = new ProgressBar { Width = Length.Cells(30), Indeterminate = true };

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
                        new DocRow(new Text("  0%")
                        {
                            Face = new Face(
                SemanticColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Dim,
                Underline.None,
                Color.Default)
                        }, empty),
                        new DocRow(new Text(" 42%")
                        {
                            Face = new Face(
                SemanticColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Dim,
                Underline.None,
                Color.Default)
                        }, partial),
                        new DocRow(new Text(" 50%")
                        {
                            Face = new Face(
                SemanticColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Dim,
                Underline.None,
                Color.Default)
                        }, half),
                        new DocRow(new Text("100%")
                        {
                            Face = new Face(
                SemanticColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Dim,
                Underline.None,
                Color.Default)
                        }, full),
                        new DocRow(new Text("  ??")
                        {
                            Face = new Face(
                SemanticColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Dim,
                Underline.None,
                Color.Default)
                        }, indeterminate)),
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
                    "Increase adds ten percent on each click. Reset returns the bar to zero.",
                    new DocColumn(interactiveBar, new DocRow(increase, reset), interactiveStatus),
                    "increase.Click += (_, _) =>\n{\n    bar.Value = Math.Min(100, bar.Value + 10);\n};")),
            new DocSection(
                "🔬",
                "Sub-cell resolution",
                "UseSubCellResolution renders with fractional block characters (▏▎▍▌▋▊▉█ horizontal, ▁▂▃▄▅▆▇█ vertical), providing 8 levels per cell for smoother progress indication.",
                new DocExample(
                    "Standard versus sub-cell",
                    "Both bars show the same 33% value. The sub-cell bar renders a partial fill in the fourth cell instead of rounding to whole cells.",
                    new DocColumn(
                        new DocRow(new Text("Standard ")
                        {
                            Face = new Face(
                SemanticColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Dim,
                Underline.None,
                Color.Default)
                        },
                            new ProgressBar { Width = Length.Cells(20), Maximum = 100, Value = 33 }),
                        new DocRow(new Text("Sub-cell ")
                        {
                            Face = new Face(
                SemanticColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Dim,
                Underline.None,
                Color.Default)
                        },
                            new ProgressBar
                            {
                                Width = Length.Cells(20),
                                Maximum = 100,
                                Value = 33,
                                UseSubCellResolution = true
                            })),
                    "bar.UseSubCellResolution = true;")));
    }
}
