namespace SharpVision.Showcase.Panes;

using SharpVision.Controls;
using SharpVision.Layout;

/// <summary>Documents and demonstrates the Popup control.</summary>
internal sealed class PopupShowcasePane: ShowcasePane
{
    internal const string Title = "Popup";
    private const string _catalogSummary =
        "Displays one owned child on an opaque bordered surface relative to an optional anchor.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Enter or pointer", "Activate the owning trigger", "IsOpen becomes true, the child is arranged, and focus enters the popup when possible."),
        new InteractionDescription("Arrows and Enter", "Navigate or activate the popup child", "The child handles its own routed interaction without leaking through the overlay."),
        new InteractionDescription("Escape", "Press Escape while open", "CloseOnEscape closes the popup and restores focus according to the owner policy."),
        new InteractionDescription("Resize", "Move the anchor or change the viewport", "Preferred placement flips and clamps inside the available terminal bounds."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Child", "Control?", "null", "Owns the one child inside the popup frame and collapses it while the popup is closed."),
        new PropertyDescription("Anchor", "Control?", "null", "Uses an optional sibling control as the anchor rectangle for the preferred placement."),
        new PropertyDescription("Placement", "PopupPlacement", "Below", "Chooses below, above, left, or right and flips to the natural opposite side before edge clamping."),
        new PropertyDescription("Glyphs / BorderColor", "Glyphs / Color?", "Rounded / inherited", "Controls the physical frame family and optional direct frame color."),
        new PropertyDescription("Background", "Color?", "inherited", "Fills the complete popup surface so content behind a drop-down never bleeds through."),
        new PropertyDescription("SurfaceBounds", "Rect", "empty", "Reports the committed framed popup rectangle while open."),
        new PropertyDescription("IsOpen", "bool", "false", "Controls the framed surface, child arrangement, rendering, hit testing, and focus transfer to a focusable child."),
        new PropertyDescription("CloseOnEscape", "bool", "true", "Closes an open popup when Escape bubbles through the owned child."),
        new PropertyDescription("Closing / Closed", "event", "null", "Observe dismissal immediately before and after the owned child becomes unavailable."),
    ];

    /// <summary>Initializes the Popup showcase page and composes its specimens.</summary>
    internal PopupShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        var status = new ControlText("Choose an item with the mouse, arrows, or Enter.")
        {
            Foreground = Palette.Muted,
        };
        var trigger = new ControlButton
        {
            Content = new ControlText("Actions ▼"),
            Style = Palette.Interactive(),
        };
        var choices = new ControlList
        {
            Width = Length.Cells(24),
            Height = Length.Cells(5),
            Items = ["Duplicate", "Rename", "Archive", "Delete"],
            SelectedIndex = 0,
            Style = Palette.Interactive(),
        };
        var popup = new ControlPopup
        {
            Anchor = trigger,
            Placement = PopupPlacement.Below,
            Glyphs = Glyphs.Rounded,
            BorderColor = Palette.Accent,
            Background = Palette.Surface,
            Child = choices,
        };
        trigger.Click += (_, _) => popup.IsOpen = !popup.IsOpen;
        choices.ItemInvoked += (_, eventArgs) =>
        {
            status.Content = eventArgs.Item is string choice
                ? $"Selected {choice}."
                : "No action selected.";
            popup.IsOpen = false;
        };
        var content = PaneSupport.Vertical();
        content.Children.Add(PaneSupport.SampleSection(
            "Anchored action menu",
            "Open the compact menu, then select with the mouse or keyboard. Escape closes it without selecting anything.",
            PaneSupport.ButtonSpecimen(trigger)));
        content.Children.Add(status);
        var overlay = new ControlOverlay { ClipToBounds = false };
        overlay.Children.Add(content);
        ControlOverlay.SetZIndex(popup, 10);
        overlay.Children.Add(popup);
        examples.Children.Add(overlay);
    }
}
