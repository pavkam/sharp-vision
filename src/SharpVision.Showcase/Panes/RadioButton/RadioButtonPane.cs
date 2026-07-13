using SharpVision.Controls;

namespace SharpVision.Showcase.Panes.RadioButton;

/// <summary>Documents and demonstrates the RadioButton control.</summary>
internal sealed class RadioButtonPane: ShowcasePane
{
    private const string _catalogSummary =
        "Selects one option from an ordinally named group scoped to the attached control root.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        PaneMetadata.Interaction("Space", "Press and release Space", "This member becomes checked and its checked peer is cleared."),
        PaneMetadata.Interaction("Pointer", "Click the primary pointer inside", "The member receives focus and selects within its group."),
        PaneMetadata.Interaction("Arrows", "Navigate among group members", "Focus moves through eligible members without selecting disabled entries."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        PaneMetadata.Property("IsChecked", "bool", "false", "Selects this member and atomically clears the previously selected peer."),
        PaneMetadata.Property("GroupName", "string?", "null", "Scopes mutual exclusion by ordinal name within the attached root."),
        PaneMetadata.Property("Content", "Control?", "null", "Owns the optional label arranged after the single-cell radio indicator."),
        PaneMetadata.Property("IsEnabled", "bool", "true", "Excludes the member from focus, pointer activation, and group keyboard navigation when false."),
    ];

    /// <summary>Initializes the RadioButton showcase page and composes its specimens.</summary>
    internal RadioButtonPane()
        : base("RadioButton", _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <summary>Gets the catalog entry for this pane.</summary>
    internal static Page Create() => new(
        "RadioButton",
        _catalogSummary,
        _catalogInteractions,
        _catalogProperties,
        static () => new RadioButtonPane());

    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        var group = PaneSupport.Vertical();
        group.Children.Add(new ControlRadioButton
        {
            Content = new ControlText("Fast"),
            GroupName = "quality",
            IsChecked = true,
            Style = Palette.Interactive(),
        });
        group.Children.Add(new ControlRadioButton
        {
            Content = new ControlText("Balanced"),
            GroupName = "quality",
            Style = Palette.Interactive(),
        });
        group.Children.Add(new ControlRadioButton
        {
            Content = new ControlText("Unavailable"),
            GroupName = "quality",
            IsEnabled = false,
            Style = Palette.Interactive(),
        });
        examples.Children.Add(PaneSupport.SampleSection(
            "Named quality group",
            "Pick one mode. Arrow keys move selection between available members; the disabled member remains visibly unavailable.",
            PaneSupport.Card(group, Glyphs.Rounded)));

        var independent = new ControlRadioButton
        {
            Content = new ControlText("Independent selection group"),
            GroupName = "delivery",
            IsChecked = true,
            Style = Palette.Interactive(),
        };
        examples.Children.Add(PaneSupport.SampleSection(
            "Separate group",
            "A different GroupName scopes selection independently, so this choice does not disturb the quality group.",
            PaneSupport.Card(independent, Glyphs.Light)));
    }
}
