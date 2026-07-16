// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the ProgressBar control with static, ranged, oriented, and interactive specimens.</summary>
internal sealed class ProgressBarPane: CompositeControl
{

    internal ProgressBarPane() => InitializeContent(CreateContent());
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "ProgressBar";

    /// <inheritdoc/>
    private static Dock CreateContent()
    {
        var empty = new ProgressBar
        {
            Width = Length.Cells(30),
            Maximum = 100,
            Value = 0,
        };
        var half = new ProgressBar
        {
            Width = Length.Cells(30),
            Maximum = 100,
            Value = 50,
        };
        var full = new ProgressBar
        {
            Width = Length.Cells(30),
            Maximum = 100,
            Value = 100,
        };

        var rangeBar = new ProgressBar
        {
            Width = Length.Cells(30),
            Minimum = 0,
            Maximum = 200,
            Value = 150,
        };
        var rangeStatus = new Text($"Minimum: 0, Maximum: 200, Value: {rangeBar.Value:0}");

        var verticalBar = new ProgressBar
        {
            Orientation = Orientation.Vertical,
            Height = Length.Cells(8),
            Maximum = 100,
            Value = 65,
        };
        var horizontalComparison = new ProgressBar
        {
            Width = Length.Cells(20),
            Maximum = 100,
            Value = 65,
        };

        var interactiveBar = new ProgressBar
        {
            Width = Length.Cells(30),
            Maximum = 100,
            Value = 30,
        };
        var interactiveStatus = new Text($"Progress: {interactiveBar.Value:0}%");
        var increase = new Button { Content = new Text("Increase +10%") };
        var reset = new Button { Content = new Text("Reset") };
        increase.Click += (_, _) =>
        {
            interactiveBar.Value = Math.Min(100, interactiveBar.Value + 10);
            interactiveStatus.Content = $"Progress: {interactiveBar.Value:0}%";
        };
        reset.Click += (_, _) =>
        {
            interactiveBar.Value = 0;
            interactiveStatus.Content = $"Progress: {interactiveBar.Value:0}%";
        };

        return Doc.Page(
            Title,
            "Displays a non-interactive visual progress indicator using filled and empty block characters.",
            Doc.Section(
                "📊",
                "Basic progress",
                "Three bars at fixed percentages show the visual range from empty through full.",
                Doc.Example(
                    "Zero, half, and full",
                    "Each bar uses the default 0..100 range with a different Value.",
                    Doc.Column(
                        Doc.Row(new Text("  0%") { Attributes = TerminalAttributes.Dim }, empty),
                        Doc.Row(new Text(" 50%") { Attributes = TerminalAttributes.Dim }, half),
                        Doc.Row(new Text("100%") { Attributes = TerminalAttributes.Dim }, full)),
                    "var bar = new ProgressBar { Value = 50 };")),
            Doc.Section(
                "📊",
                "Custom range",
                "Override the default Minimum and Maximum to map progress onto any integer domain.",
                Doc.Example(
                    "Extended range",
                    "A bar spanning 0..200 with a current value of 150 fills three quarters of its track.",
                    Doc.Column(rangeBar, rangeStatus),
                    "var bar = new ProgressBar\n{\n    Minimum = 0,\n    Maximum = 200,\n    Value = 150,\n};")),
            Doc.Section(
                "📊",
                "Vertical orientation",
                "Set Orientation to Vertical for a column-shaped indicator that fills from bottom to top.",
                Doc.Example(
                    "Side-by-side comparison",
                    "The same Value renders vertically and horizontally for layout flexibility.",
                    Doc.Row(verticalBar, horizontalComparison))),
            Doc.Section(
                "📊",
                "Interactive",
                "Wire buttons to the Value property and observe the bar update in real time.",
                Doc.Example(
                    "Increment and reset",
                    "Increase adds ten percent on each click. Reset returns the bar to zero.",
                    Doc.Column(interactiveBar, Doc.Row(increase, reset), interactiveStatus),
                    "increase.Click += (_, _) =>\n{\n    bar.Value = Math.Min(100, bar.Value + 10);\n};")),
            Doc.Section(
                "📊",
                "Sub-cell resolution",
                "UseSubCellResolution renders with fractional block characters (▏▎▍▌▋▊▉█ horizontal, ▁▂▃▄▅▆▇█ vertical), providing 8 levels per cell for smoother progress indication.",
                Doc.Example(
                    "Standard versus sub-cell",
                    "Both bars show the same 33% value. The sub-cell bar renders a partial fill in the fourth cell instead of rounding to whole cells.",
                    Doc.Column(
                        Doc.Row(new Text("Standard ") { Attributes = TerminalAttributes.Dim }, new ProgressBar { Width = Length.Cells(20), Maximum = 100, Value = 33 }),
                        Doc.Row(new Text("Sub-cell ") { Attributes = TerminalAttributes.Dim }, new ProgressBar { Width = Length.Cells(20), Maximum = 100, Value = 33, UseSubCellResolution = true })),
                    "bar.UseSubCellResolution = true;")));
    }
}
