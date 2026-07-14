// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the ScrollView control with vertical, both-axis, and scrollbar-visibility specimens.</summary>
internal sealed class ScrollViewPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "ScrollView";

    /// <inheritdoc/>
    protected override Control Build()
    {
        Stack verticalContent = new() { Spacing = 1 };

        for (int index = 1; index <= 10; index++)
        {
            verticalContent.Children.Add(new Text($"Row {index:00}"));
        }

        ScrollView verticalOnly = new()
        {
            Width = Length.Cells(16),
            Height = Length.Cells(6),
            Content = verticalContent,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
        };

        Stack bothContent = new() { Spacing = 1 };

        for (int index = 1; index <= 14; index++)
        {
            bothContent.Children.Add(new Text($"Scrollable row {index:00} · wide content beyond the viewport"));
        }

        ScrollView both = new()
        {
            Width = Length.Cells(34),
            Height = Length.Cells(8),
            Content = bothContent,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
        };

        Stack alwaysContent = Doc.Column(new Text("Fits without overflow"));
        ScrollView alwaysVisible = new()
        {
            Width = Length.Cells(24),
            Height = Length.Cells(4),
            Content = alwaysContent,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Always,
        };

        Stack neverContent = new() { Spacing = 1 };

        for (int index = 1; index <= 8; index++)
        {
            neverContent.Children.Add(new Text($"Hidden-bar row {index:00}"));
        }

        ScrollView neverVisible = new()
        {
            Width = Length.Cells(24),
            Height = Length.Cells(4),
            Content = neverContent,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
        };

        Stack visibilityModes = Doc.Row(
            Doc.Column(new Text("Always"), alwaysVisible),
            Doc.Column(new Text("Never (still scrolls)"), neverVisible));

        return Doc.Page(
            Title,
            "Hosts one child in a cell viewport with automatic bars, nested wheel propagation, and bring-into-view.",
            Doc.Example(
                "Vertical-only scrolling",
                "ScrollBars.Vertical disables horizontal panning entirely; arrow keys, Page keys, Home, and End move only along the vertical extent.",
                verticalOnly),
            Doc.Example(
                "Both-axis scrolling",
                "ScrollBars.Both scrolls independently along each axis. Arrow keys and Page keys move the focused viewport by LineSize or a page distance, and the wheel scrolls over nested content; bars appear automatically only when content exceeds the viewport.",
                both),
            Doc.Example(
                "Scrollbar visibility modes",
                "ShowScrollBars.Always reserves and renders chrome even when content fits, while ShowScrollBars.Never hides the chrome entirely but leaves the enabled axis scrollable by keyboard or wheel.",
                visibilityModes));
    }
}
