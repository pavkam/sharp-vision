namespace SharpVision.Showcase.Panes;

using SharpVision.Controls;
using SharpVision.Layout;

using TerminalAttributes = SharpVision.Terminal.Rendering.Attributes;

/// <summary>Documents and demonstrates the Overlay control.</summary>
internal sealed class OverlayShowcasePane: ShowcasePane
{
    internal const string Title = "Overlay";
    private const string _catalogSummary =
        "Arranges children into one shared content box with stable attached z-order for rendering and hit testing.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Pointer", "Target an overlapping child", "The highest ZIndex receives the hit; equal values retain insertion order."),
        new InteractionDescription("Rendering", "Change ZIndex", "The child redraws in the new stable layer order without changing ownership."),
        new InteractionDescription("Resize", "Change the shared bounds", "Every child is rearranged into the same committed content box."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Children", "Children", "empty", "Owns layered controls and preserves stable insertion order inside equal z-index groups."),
        new PropertyDescription("ClipToBounds", "bool", "true", "Clips layered descendants and pointer hit testing to the Overlay content box."),
        new PropertyDescription("ZIndex", "int", "0", "Attaches a signed render and hit-test order to each child without changing ownership order."),
        new PropertyDescription("Padding", "Thickness", "0", "Deflates the shared content rectangle before every child is arranged."),
    ];

    /// <summary>Initializes the Overlay showcase page and composes its specimens.</summary>
    internal OverlayShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        var overlay = new ControlOverlay
        {
            Width = Length.Cells(32),
            Height = Length.Cells(7),
            ClipToBounds = true,
        };
        var back = new ControlText("Background layer")
        {
            Background = Palette.Highlight,
            Padding = new Thickness(1),
        };
        ControlOverlay.SetZIndex(back, -1);
        overlay.Children.Add(back);
        var middle = PaneSupport.Card(new ControlText("Middle layer"), Glyphs.Heavy);
        middle.Margin = new Thickness(4, 2, 4, 2);
        overlay.Children.Add(middle);
        var front = new ControlText("Front layer")
        {
            Foreground = Palette.Warning,
            Attributes = TerminalAttributes.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        ControlOverlay.SetZIndex(front, 10);
        overlay.Children.Add(front);
        examples.Children.Add(overlay);
    }
}
