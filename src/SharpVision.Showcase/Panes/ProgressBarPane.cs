// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents determinate, vertical, indeterminate, and mutable ProgressBar states.</summary>
internal sealed class ProgressBarPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "ProgressBar";

    /// <summary>Initializes the retained ProgressBar documentation page.</summary>
    internal ProgressBarPane() => InitializeContent(CreateContent());

    private static Dock CreateContent()
    {
        var partial = new ProgressBar
        {
            Width = Length.Cells(28),
            Height = Length.Cells(1),
            Maximum = 100,
            Value = 42,
        };
        var empty = new ProgressBar
        {
            Width = Length.Cells(28),
            Height = Length.Cells(1),
        };
        var full = new ProgressBar
        {
            Width = Length.Cells(28),
            Height = Length.Cells(1),
            Maximum = 100,
            Value = 100,
        };
        var vertical = new ProgressBar
        {
            Width = Length.Cells(1),
            Height = Length.Cells(6),
            Maximum = 100,
            Value = 50,
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var verticalStage = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children =
            {
                vertical,
                new Text("Vertical progress fills from the bottom."),
            },
        };
        var indeterminate = new ProgressBar
        {
            Width = Length.Cells(28),
            Height = Length.Cells(1),
            IsIndeterminate = true,
        };
        var live = new ProgressBar
        {
            Width = Length.Cells(28),
            Height = Length.Cells(1),
            Maximum = 10,
            Value = 3,
        };
        var status = new Text("Live progress: 3 / 10");
        var advance = new Button { Content = new Text("Advance progress") };
        advance.Click += (_, _) =>
        {
            live.Value = Math.Min(live.Maximum, live.Value + 1);
            status.Content = $"Live progress: {live.Value:0} / {live.Maximum:0}";
        };

        return Doc.Page(
            Title,
            "Presents finite determinate progress or a deterministic unknown-duration state without accepting input.",
            Doc.Section(
                "▰",
                "Determinate range",
                "Whole terminal cells fill left-to-right from the normalized finite range.",
                Doc.Example(
                    "Partial progress",
                    "Forty-two percent fills floor(0.42 × 28) complete cells.",
                    Doc.Card(partial),
                    "var progress = new ProgressBar { Maximum = 100, Value = 42 };"),
                Doc.Example(
                    "Endpoints",
                    "Minimum draws only track cells; maximum fills every cell.",
                    Doc.Column(Doc.Card(empty), Doc.Card(full)))),
            Doc.Section(
                "▥",
                "Orientation and uncertainty",
                "Vertical and indeterminate states remain deterministic under resize.",
                Doc.Example(
                    "Vertical fill",
                    "The filled half begins at the bottom edge.",
                    Doc.Card(verticalStage)),
                Doc.Example(
                    "Indeterminate",
                    "A distinct static glyph communicates unknown duration without claiming animation.",
                    Doc.Card(indeterminate))),
            Doc.Section(
                "＋",
                "Live mutation",
                "Changing Value redraws the same retained control and clamps at Maximum.",
                Doc.Example(
                    "Advance a retained bar",
                    "Activate the button and compare the exact committed value with the updated cells.",
                    Doc.Column(live, advance, status),
                    "progress.Value = Math.Min(progress.Maximum, progress.Value + 1);")));
    }
}
