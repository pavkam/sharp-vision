using SharpVision.Layout;

namespace SharpVision.Showcase.Panes.List;

/// <summary>Documents and demonstrates the List control.</summary>
internal sealed class ListPane: ShowcasePane
{
    private const string _catalogSummary =
        "Realizes selectable items with keyboard, pointer, activation, and automatic vertical scrolling behavior.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        PaneMetadata.Interaction("Arrows", "Press Up or Down", "Selection moves by one eligible item and keeps the active row visible."),
        PaneMetadata.Interaction("Paging", "Press Home, End, Page Up, or Page Down", "Selection jumps to the corresponding endpoint or viewport page."),
        PaneMetadata.Interaction("Enter", "Press Enter on the active item", "ItemInvoked reports the selected value and activation cause."),
        PaneMetadata.Interaction("Pointer", "Click a row", "The clicked row becomes selected and remains visible."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        PaneMetadata.Property("Items", "IReadOnlyList<object?>", "empty", "Supplies borrowed item values realized through the current item template."),
        PaneMetadata.Property("ItemTemplate", "ItemTemplate", "text", "Creates one fresh control for each realized item and selection state."),
        PaneMetadata.Property("SelectionMode", "SelectionMode", "Single", "Chooses no selection, one selected item, or multiple selected items."),
        PaneMetadata.Property("SelectedIndex", "int", "-1", "Gets or selects the active zero-based item while keeping it visible."),
        PaneMetadata.Property("VerticalOffset", "int", "0", "Reports the first vertically visible item after navigation or pointer scrolling."),
        PaneMetadata.Property("ScrollBars / ShowScrollBars", "ScrollBars / ShowScrollBars", "Both / WhenNeeded", "Expose the same enabled-axis and visibility policy used by the canonical ScrollView."),
        PaneMetadata.Property("ScrollBarChrome / ScrollBarFill", "ScrollBarStyle / ScrollBarFill", "Full / Block", "Configure the actual composed rails as thin/full and line/block without a List-only style dialect."),
    ];

    /// <summary>Initializes the List showcase page and composes its specimens.</summary>
    internal ListPane()
        : base("List", _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <summary>Gets the catalog entry for this pane.</summary>
    internal static Page Create() => new(
        "List",
        _catalogSummary,
        _catalogInteractions,
        _catalogProperties,
        static () => new ListPane());

    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        var status = new ControlText("Selected item: Beta. Use Up or Down to move the selection.")
        {
            Foreground = Palette.Muted,
        };
        var active = new ControlList
        {
            Width = Length.Cells(18),
            Height = Length.Cells(6),
            Style = Palette.List(),
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

        var disabled = new ControlList
        {
            Width = Length.Cells(18),
            Height = Length.Cells(4),
            IsEnabled = false,
            Style = Palette.List(),
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
