// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;


/// <summary>Documents and demonstrates the List control.</summary>
internal sealed class ListShowcasePane: ShowcasePane
{
    internal const string Title = "List";
    private const string _catalogSummary =
        "Realizes selectable items with keyboard, pointer, activation, and automatic vertical scrolling behavior.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Arrows", "Press Up or Down", "Selection moves by one eligible item and keeps the active row visible."),
        new InteractionDescription("Paging", "Press Home, End, Page Up, or Page Down", "Selection jumps to the corresponding endpoint or viewport page."),
        new InteractionDescription("Enter", "Press Enter on the active item", "ItemInvoked reports the selected value and activation cause."),
        new InteractionDescription("Pointer", "Click a row", "The clicked row becomes selected and remains visible."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Items", "IReadOnlyList<object?>", "empty", "Supplies borrowed item values realized through the current item template."),
        new PropertyDescription("ItemTemplate", "ItemTemplate", "text", "Creates one fresh control for each realized item and selection state."),
        new PropertyDescription("SelectionMode", "SelectionMode", "Single", "Chooses no selection, one selected item, or multiple selected items."),
        new PropertyDescription("SelectedIndex", "int", "-1", "Gets or selects the active zero-based item while keeping it visible."),
        new PropertyDescription("VerticalOffset", "int", "0", "Reports the first vertically visible item after navigation or pointer scrolling."),
        new PropertyDescription("ScrollBars / ShowScrollBars", "ScrollBars / ShowScrollBars", "Both / WhenNeeded", "Expose the same enabled-axis and visibility policy used by the canonical ScrollView."),
        new PropertyDescription("ScrollBarChrome / ScrollBarFill", "ScrollBarStyle / ScrollBarFill", "Full / Block", "Configure the actual composed rails as thin/full and line/block without a List-only style dialect."),
    ];

    /// <summary>Initializes the List showcase page and composes its specimens.</summary>
    internal ListShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        ControlText status = new("Selected item: Beta. Use Up or Down to move the selection.")
        {
        };
        ControlList active = new()
        {
            Width = Length.Cells(18),
            Height = Length.Cells(6),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarChrome = ScrollBarStyle.Thin,
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
                ? $"Selected item: {active.Items[active.SelectedIndex]}. Use Up or Down to move the selection."
                : "No item selected.";
        };
        active.ItemInvoked += (_, eventArgs) =>
            status.Content = $"Activated {eventArgs.Item} via {eventArgs.Cause}.";

        ControlList disabled = new()
        {
            Width = Length.Cells(18),
            Height = Length.Cells(4),
            IsEnabled = false,
            Items = new object?[] { "Alpha", "Beta", "Gamma" },
        };

        examples.Children.Add(PaneSupport.SampleSection(
            "Selectable list",
            "The focused list accepts Up, Down, paging, Enter, and pointer clicks. The status line reports the current selection or activation.",
            active));
        examples.Children.Add(PaneSupport.SampleSection(
            "Disabled list",
            "These rows stay visible so the data context is clear, but IsEnabled is false: the list cannot receive focus, change selection, or invoke an item.",
            disabled));
        examples.Children.Add(status);
    }
}
