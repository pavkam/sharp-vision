using SharpVision.Controls;
using SharpVision.Layout;

using TerminalAttributes = SharpVision.Terminal.Rendering.Attributes;

namespace SharpVision.Showcase.Panes.Overlay;

/// <summary>Documents and demonstrates the Overlay control.</summary>
internal sealed class OverlayPane: ShowcasePane
{
    private const string _catalogSummary =
        "Arranges children into one shared content box with stable attached z-order for rendering and hit testing.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        PaneMetadata.Interaction("Pointer", "Target an overlapping child", "The highest ZIndex receives the hit; equal values retain insertion order."),
        PaneMetadata.Interaction("Rendering", "Change ZIndex", "The child redraws in the new stable layer order without changing ownership."),
        PaneMetadata.Interaction("Resize", "Change the shared bounds", "Every child is rearranged into the same committed content box."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        PaneMetadata.Property("Children", "Children", "empty", "Owns layered controls and preserves stable insertion order inside equal z-index groups."),
        PaneMetadata.Property("ClipToBounds", "bool", "true", "Clips layered descendants and pointer hit testing to the Overlay content box."),
        PaneMetadata.Property("ZIndex", "int", "0", "Attaches a signed render and hit-test order to each child without changing ownership order."),
        PaneMetadata.Property("Padding", "Thickness", "0", "Deflates the shared content rectangle before every child is arranged."),
    ];

    /// <summary>Initializes the Overlay showcase page and composes its specimens.</summary>
    internal OverlayPane()
        : base("Overlay", _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <summary>Gets the catalog entry for this pane.</summary>
    internal static Page Create() => new(
        "Overlay",
        _catalogSummary,
        _catalogInteractions,
        _catalogProperties,
        static () => new OverlayPane());

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
