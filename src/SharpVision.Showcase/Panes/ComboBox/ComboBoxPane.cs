using SharpVision.Layout;

namespace SharpVision.Showcase.Panes.ComboBox;

/// <summary>Documents and demonstrates the ComboBox control.</summary>
internal sealed class ComboBoxPane: ShowcasePane
{
    private const string _catalogSummary =
        "Displays one selected value and opens an owned popup-style List for keyboard or pointer choice.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        PaneMetadata.Interaction("Enter or Space", "Open the drop-down", "Focus moves into the owned list and the popup becomes interactive."),
        PaneMetadata.Interaction("Arrows", "Navigate while open", "The list selection changes without closing the popup."),
        PaneMetadata.Interaction("Enter", "Commit the active item", "SelectedIndex and SelectionChanged update, then the popup closes."),
        PaneMetadata.Interaction("Escape", "Dismiss while open", "The popup closes and the previous selection remains unchanged."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        PaneMetadata.Property("Items", "IReadOnlyList<object?>", "empty", "Copies borrowed choices into the owned List used by the popup field."),
        PaneMetadata.Property("SelectedIndex", "int", "-1", "Gets or sets the exclusive selected choice while keeping List active navigation synchronized."),
        PaneMetadata.Property("DropDownHeight", "int", "8", "Caps the visible popup list height in non-zero terminal cells."),
        PaneMetadata.Property("ScrollBars / ShowScrollBars", "ScrollBars / ShowScrollBars", "Both / WhenNeeded", "Use the common overflow policy for the popup List; vertical thin rails are ideal for long option sets."),
        PaneMetadata.Property("ScrollBarChrome / ScrollBarFill", "ScrollBarStyle / ScrollBarFill", "Full / Block", "Choose the same thin/full and line/block rail treatment used by Lists and ScrollViews."),
        PaneMetadata.Property("IsOpen", "bool", "false", "Controls list arrangement, rendering, hit testing, and focus transfer into the drop-down."),
        PaneMetadata.Property("SelectionChanged", "event", "null", "Reports List selection commits from direct assignment, pointer, or keyboard activation."),
    ];

    /// <summary>Initializes the ComboBox showcase page and composes its specimens.</summary>
    internal ComboBoxPane()
        : base("ComboBox", _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <summary>Gets the catalog entry for this pane.</summary>
    internal static Page Create() => new(
        "ComboBox",
        _catalogSummary,
        _catalogInteractions,
        _catalogProperties,
        static () => new ComboBoxPane());

    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        var status = new ControlText("Selected: Comfortable") { Foreground = Palette.Muted };
        var comboBox = new ControlComboBox
        {
            Width = Length.Cells(28),
            Items = ["Compact", "Comfortable", "Spacious"],
            SelectedIndex = 1,
            DropDownHeight = 4,
            Style = Palette.List(),
        };
        comboBox.SelectionChanged += (_, _) =>
        {
            status.Content = comboBox.SelectedIndex >= 0
                ? $"Selected: {comboBox.Items[comboBox.SelectedIndex]}."
                : "No selection.";
        };
        var stage = new ControlCanvas
        {
            Width = Length.Cells(30),
            Height = Length.Cells(6),
            ClipToBounds = false,
        };
        stage.Children.Add(comboBox);
        examples.Children.Add(PaneSupport.SampleSection(
            "Popup choice field",
            "Click or press Enter/Space to open. The popup list owns arrow navigation; Enter chooses and closes it, while Escape dismisses it.",
            stage));
        examples.Children.Add(status);
    }
}
