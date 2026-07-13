namespace SharpVision.Showcase.Panes;

using SharpVision.Controls;

/// <summary>Documents and demonstrates the CheckBox control.</summary>
internal sealed class CheckBoxShowcasePane: ShowcasePane
{
    internal const string Title = "CheckBox";
    private const string _catalogSummary =
        "Toggles an optional label through two-state or three-state selection with explicit events.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Space", "Press and release Space", "The control advances unchecked, checked, and optional indeterminate states."),
        new InteractionDescription("Pointer", "Click the primary pointer inside", "Focus moves to the box and one state transition is committed."),
        new InteractionDescription("Disabled", "Set IsEnabled to false", "The current state remains visible while keyboard and pointer activation are ignored."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("IsChecked", "bool?", "false", "Stores unchecked, checked, or indeterminate state when three-state mode permits null."),
        new PropertyDescription("IsThreeState", "bool", "false", "Adds indeterminate to the activation cycle and normalizes null when later disabled."),
        new PropertyDescription("Content", "Control?", "null", "Owns the optional label after the fixed-width active mark family."),
        new PropertyDescription("MarkStyle", "CheckBoxStyle", "Square", "Chooses square, [x] bracket, or Unicode tick marks without label movement."),
        new PropertyDescription("Marks", "Marks", "Unicode defaults", "Selects validated printable one-cell Runes for the square mark family."),
        new PropertyDescription("IsEnabled", "bool", "true", "Prevents focus and state transitions while preserving the current mark when false."),
    ];

    /// <summary>Initializes the CheckBox showcase page and composes its specimens.</summary>
    internal CheckBoxShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        var brackets = PaneSupport.Vertical();
        brackets.Children.Add(new ControlCheckBox
        {
            Content = new ControlText("Unchecked brackets"),
            MarkStyle = CheckBoxStyle.Brackets,
            Style = Palette.Interactive(),
        });
        brackets.Children.Add(new ControlCheckBox
        {
            Content = new ControlText("Checked brackets"),
            IsChecked = true,
            MarkStyle = CheckBoxStyle.Brackets,
            Style = Palette.Interactive(),
        });
        brackets.Children.Add(new ControlCheckBox
        {
            Content = new ControlText("Indeterminate brackets"),
            IsThreeState = true,
            IsChecked = null,
            MarkStyle = CheckBoxStyle.Brackets,
            Style = Palette.Interactive(),
        });
        examples.Children.Add(PaneSupport.SampleSection(
            "Bracket marks",
            "Classic [ ] / [x] marks reserve three cells, so toggling and indeterminate state never shift the label.",
            PaneSupport.Card(brackets, Glyphs.Rounded)));
        examples.Children.Add(PaneSupport.SampleSection(
            "Disabled bracket state",
            "The familiar [x] mark stays structurally recognizable while the disabled palette deliberately recedes from interactive choices.",
            PaneSupport.Card(new ControlCheckBox
            {
                Content = new ControlText("Disabled bracket"),
                IsChecked = true,
                IsEnabled = false,
                MarkStyle = CheckBoxStyle.Brackets,
                Style = Palette.Interactive(),
            }, Glyphs.Light)));
    }
}
