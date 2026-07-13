using SharpVision.Controls;
using SharpVision.Layout;

namespace SharpVision.Showcase.Panes.Stack;

/// <summary>Documents and demonstrates the Stack control.</summary>
internal sealed class StackPane: ShowcasePane
{
    private const string _catalogSummary =
        "Arranges children sequentially with fixed, automatic, percentage, or proportional lengths and stable spacing.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        PaneMetadata.Interaction("Layout", "Set Orientation, lengths, and Spacing", "Children receive deterministic sequential tracks along the selected axis."),
        PaneMetadata.Interaction("Resize", "Change the available bounds", "Automatic and proportional children recompute without exceeding the stack."),
        PaneMetadata.Interaction("Reverse", "Set Reverse to true", "Geometry, rendering, hit testing, and default focus traversal reverse together."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        PaneMetadata.Property("Children", "Children", "empty", "Owns the sequential controls whose box requests participate in track allocation."),
        PaneMetadata.Property("Orientation", "Orientation", "Vertical", "Chooses top-to-bottom or left-to-right sequential layout."),
        PaneMetadata.Property("Spacing", "int", "0", "Adds non-negative terminal cells between participating children."),
        PaneMetadata.Property("Reverse", "bool", "false", "Reverses geometry, rendering, hit testing, and default focus traversal consistently."),
        PaneMetadata.Property("Width / Height", "Length", "Auto", "Supports fixed, percentage, automatic, and proportional requests on child border boxes."),
    ];

    /// <summary>Initializes the Stack showcase page and composes its specimens.</summary>
    internal StackPane()
        : base("Stack", _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <summary>Gets the catalog entry for this pane.</summary>
    internal static Page Create() => new(
        "Stack",
        _catalogSummary,
        _catalogInteractions,
        _catalogProperties,
        static () => new StackPane());

    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        var horizontal = PaneSupport.Horizontal();
        var fixedCard = PaneSupport.Card(new ControlText("Fixed 10"), Glyphs.Light);
        fixedCard.Width = Length.Cells(10);
        horizontal.Children.Add(fixedCard);
        var percentCard = PaneSupport.Card(new ControlText("35%"), Glyphs.Heavy);
        percentCard.Width = Length.Percent(35);
        horizontal.Children.Add(percentCard);
        var starCard = PaneSupport.Card(new ControlText("1*"), Glyphs.Paired);
        starCard.Width = Length.Star(1);
        horizontal.Children.Add(starCard);
        horizontal.Width = Length.Cells(40);
        examples.Children.Add(PaneSupport.SampleSection(
            "Mixed horizontal tracks",
            "Fixed cells, percentage sizing, and proportional remainder can coexist in one horizontal ControlStack.",
            horizontal));
        var reversed = PaneSupport.Horizontal();
        reversed.Reverse = true;
        reversed.Children.Add(PaneSupport.Card(new ControlText("First"), Glyphs.Light));
        reversed.Children.Add(PaneSupport.Card(new ControlText("Second"), Glyphs.Heavy));
        reversed.Children.Add(PaneSupport.Card(new ControlText("Third"), Glyphs.Paired));
        examples.Children.Add(PaneSupport.SampleSection(
            "Reverse order",
            "Reverse changes visual and keyboard-navigation order without changing the source child collection.",
            reversed));

        var vertical = PaneSupport.Vertical();
        vertical.Children.Add(PaneSupport.Card(new ControlText("Top"), Glyphs.Rounded));
        vertical.Children.Add(PaneSupport.Card(new ControlText("Spacing = 1"), Glyphs.Light));
        vertical.Children.Add(PaneSupport.Card(new ControlText("Bottom"), Glyphs.Heavy));
        examples.Children.Add(PaneSupport.SampleSection(
            "Vertical spacing",
            "Vertical is the default orientation; explicit spacing is applied only between participating children.",
            vertical));
    }
}
