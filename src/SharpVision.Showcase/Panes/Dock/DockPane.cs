using SharpVision.Controls;
using SharpVision.Layout;

namespace SharpVision.Showcase.Panes.Dock;

/// <summary>Documents and demonstrates the Dock control.</summary>
internal sealed class DockPane: ShowcasePane
{
    private const string _catalogSummary =
        "Consumes remaining physical edges in child order and optionally gives the final child all remaining space.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        PaneMetadata.Interaction("Layout", "Attach each child to a Side", "Children consume the remaining rectangle in insertion order."),
        PaneMetadata.Interaction("Focus", "Move focus with Tab or Shift+Tab", "Focus follows stable child order rather than changing with docked edges."),
        PaneMetadata.Interaction("Resize", "Change the available bounds", "Edge sizes recompute and the filling child receives the remainder."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        PaneMetadata.Property("Children", "Children", "empty", "Owns controls whose attached Side values consume the remaining rectangle in order."),
        PaneMetadata.Property("LastChildFills", "bool", "true", "Lets the final child fill the remaining content box regardless of its attached side."),
        PaneMetadata.Property("Spacing", "int", "0", "Adds non-negative terminal cells after each consuming edge without overflowing tiny layouts."),
        PaneMetadata.Property("Side", "Side", "Left", "Attaches Left, Top, Right, or Bottom placement to each child."),
    ];

    /// <summary>Initializes the Dock showcase page and composes its specimens.</summary>
    internal DockPane()
        : base("Dock", _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <summary>Gets the catalog entry for this pane.</summary>
    internal static Page Create() => new(
        "Dock",
        _catalogSummary,
        _catalogInteractions,
        _catalogProperties,
        static () => new DockPane());

    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        var dock = new ControlDock
        {
            Width = Length.Cells(38),
            Height = Length.Cells(9),
            LastChildFills = true,
            Spacing = 1,
        };
        var left = PaneSupport.Card(new ControlText("Left"), Glyphs.Light);
        left.Width = Length.Cells(7);
        ControlDock.SetSide(left, Side.Left);
        dock.Children.Add(left);
        var top = PaneSupport.Card(new ControlText("Top"), Glyphs.Heavy);
        top.Height = Length.Cells(2);
        ControlDock.SetSide(top, Side.Top);
        dock.Children.Add(top);
        var right = PaneSupport.Card(new ControlText("Right"), Glyphs.Paired);
        right.Width = Length.Cells(8);
        ControlDock.SetSide(right, Side.Right);
        dock.Children.Add(right);
        var bottom = PaneSupport.Card(new ControlText("Bottom"), Glyphs.Ascii);
        bottom.Height = Length.Cells(2);
        ControlDock.SetSide(bottom, Side.Bottom);
        dock.Children.Add(bottom);
        dock.Children.Add(PaneSupport.Card(new ControlText("Fill"), Glyphs.Rounded));
        examples.Children.Add(dock);
    }
}
