// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the ScrollView control with a scrollable viewport specimen.</summary>
internal sealed class ScrollViewPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "ScrollView";

    /// <inheritdoc/>
    protected override Control Build()
    {
        Stack content = new() { Spacing = 1 };

        for (int index = 1; index <= 14; index++)
        {
            content.Children.Add(new Text($"Scrollable row {index:00} · wide content beyond the viewport"));
        }

        ScrollView scrollView = new()
        {
            Width = Length.Cells(34),
            Height = Length.Cells(8),
            Content = content,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
        };

        return Doc.Page(
            Title,
            "Hosts one child in a cell viewport with automatic bars, nested wheel propagation, and bring-into-view.",
            Doc.Example(
                "Scrollable viewport",
                "Arrow keys and Page keys move the focused viewport by LineSize or a page distance, Home and End jump to an extent endpoint, and the wheel scrolls over nested content. Bars appear automatically only when content exceeds the viewport.",
                scrollView));
    }
}
