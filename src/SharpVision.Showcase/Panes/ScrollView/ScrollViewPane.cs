using SharpVision.Layout;

namespace SharpVision.Showcase.Panes.ScrollView;

/// <summary>Documents and demonstrates the ScrollView control.</summary>
internal sealed class ScrollViewPane: ShowcasePane
{
    private const string _catalogSummary =
        "Hosts one child in a cell viewport with automatic bars, nested wheel propagation, and bring-into-view.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        PaneMetadata.Interaction("Arrows and Page keys", "Move the focused viewport", "Offsets change by LineSize or page distance while remaining clamped."),
        PaneMetadata.Interaction("Home or End", "Jump to an extent endpoint", "The selected axis offset moves to its minimum or maximum."),
        PaneMetadata.Interaction("Wheel", "Scroll over nested content", "The nearest view consumes applicable delta and propagates only unused movement."),
        PaneMetadata.Interaction("Bring into view", "Focus or request a descendant rectangle", "Offsets adjust until the target is visible inside the committed viewport."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        PaneMetadata.Property("Content", "Control?", "null", "Owns the single scrollable child measured against the enabled unbounded axes."),
        PaneMetadata.Property("HorizontalBarVisibility", "ScrollBarVisibility", "Auto", "Shows, hides, disables, or automatically reserves the horizontal bar."),
        PaneMetadata.Property("VerticalBarVisibility", "ScrollBarVisibility", "Auto", "Shows, hides, disables, or automatically reserves the vertical bar."),
        PaneMetadata.Property("ConstrainContentToViewport", "bool", "false", "Supplies the finite viewport width during measure so word-wrapping reading content reflows instead of expanding horizontally."),
        PaneMetadata.Property("HorizontalOffset / VerticalOffset", "int", "0", "Store validated cell offsets clamped whenever extent or viewport changes."),
        PaneMetadata.Property("LineSize / PageOverlap", "int", "1 / 1", "Control keyboard line movement and retained overlap between page movements."),
    ];

    /// <summary>Initializes the ScrollView showcase page and composes its specimens.</summary>
    internal ScrollViewPane()
        : base("ScrollView", _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <summary>Gets the catalog entry for this pane.</summary>
    internal static Page Create() => new(
        "ScrollView",
        _catalogSummary,
        _catalogInteractions,
        _catalogProperties,
        static () => new ScrollViewPane());

    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        var content = PaneSupport.Vertical();

        for (var index = 1; index <= 14; index++)
        {
            content.Children.Add(new ControlText(
                $"Scrollable row {index:00} · wide content beyond the viewport"));
        }

        examples.Children.Add(new ControlScrollView
        {
            Width = Length.Cells(34),
            Height = Length.Cells(8),
            Content = content,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
        });
    }
}
