// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the List control with live, themed selection specimens.</summary>
internal sealed class ListPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "List";

    /// <summary>Initializes the retained List documentation page.</summary>
    internal ListPane() => InitializeContent(CreateContent());

    private static Stack CreateContent()
    {
        var status = new Text("Selected item: Beta");
        var active = new List()
        {
            Width = Length.Cells(18),
            Height = Length.Cells(6),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarChrome = ScrollBarChrome.Thin,
            ScrollBarFill = ScrollBarFill.Line,
            Items = new object?[]
            {
                "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta",
            },
            SelectedIndex = 1,
        };
        active.SelectionChanged += (_, _) =>
        {
            status.Content = active.SelectedIndex >= 0
                ? $"Selected item: {active.Items[active.SelectedIndex]}"
                : "No item selected.";
        };
        active.ItemInvoked += (_, eventArgs) =>
            status.Content = $"Activated {eventArgs.Item} via {eventArgs.Cause}.";

        var disabled = new List()
        {
            Width = Length.Cells(18),
            Height = Length.Cells(4),
            IsEnabled = false,
            Items = new object?[] { "Alpha", "Beta", "Gamma" },
        };

        return Doc.Page(
            Title,
            "Realizes selectable items with keyboard, pointer, activation, and automatic vertical scrolling behavior.",
            Doc.Example(
                "Selectable list",
                "The focused list accepts Up, Down, paging, Enter, and pointer clicks. The status line reports the current selection or activation.",
                active),
            Doc.Example(
                "Disabled list",
                "These rows stay visible so the data context is clear, but IsEnabled is false: the list cannot receive focus, change selection, or invoke an item.",
                disabled),
            status);
    }
}
