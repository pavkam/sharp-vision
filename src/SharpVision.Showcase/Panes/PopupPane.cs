// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the Popup control with an anchored, keyboard- and pointer-driven action menu.</summary>
internal sealed class PopupPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Popup";

    /// <inheritdoc/>
    protected override Control Build()
    {
        var status = new Text("Choose an item with the mouse, arrows, or Enter.");
        var trigger = new Button() { Content = new Text("Actions ▼") };
        var choices = new List()
        {
            Width = Length.Cells(24),
            Height = Length.Cells(5),
            Items = ["Duplicate", "Rename", "Archive", "Delete"],
            SelectedIndex = 0,
        };
        var popup = new Popup()
        {
            Anchor = trigger,
            Placement = PopupPlacement.Below,
            Glyphs = Glyphs.Rounded,
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

        var overlay = new Overlay() { ClipToBounds = false };
        overlay.Children.Add(Doc.Column(trigger, status));
        Overlay.SetZIndex(popup, 10);
        overlay.Children.Add(popup);

        var variants = new Stack() { Orientation = Orientation.Horizontal, Spacing = 6 };
        variants.Children.Add(PlacementDemo("Above", PopupPlacement.Above));
        variants.Children.Add(PlacementDemo("Left", PopupPlacement.Left));
        variants.Children.Add(PlacementDemo("Right", PopupPlacement.Right));

        var edgeTrigger = new Button { Content = new Text("Edge anchor") };
        var edgePopup = new Popup
        {
            Anchor = edgeTrigger,
            Placement = PopupPlacement.Below,
            Child = new Text("Flips above, then clamps"),
            IsOpen = true,
        };
        var edgeStage = new Overlay
        {
            Width = Length.Cells(28),
            Height = Length.Cells(5),
            ClipToBounds = false,
        };
        edgeTrigger.VerticalAlignment = VerticalAlignment.Bottom;
        edgeStage.Children.Add(edgeTrigger);
        Overlay.SetZIndex(edgePopup, 10);
        edgeStage.Children.Add(edgePopup);

        var lifecycleStatus = new Text("Lifecycle: open");
        var lifecycleAnchor = new Button { Content = new Text("Close lifecycle popup") };
        var lifecyclePopup = new Popup
        {
            Anchor = lifecycleAnchor,
            Child = new Text("Lifecycle content"),
            IsOpen = true,
        };
        lifecyclePopup.Closing += (_, _) => lifecycleStatus.Content = "Lifecycle: Closing";
        lifecyclePopup.Closed += (_, _) => lifecycleStatus.Content += " → Closed";
        lifecycleAnchor.Click += (_, _) => lifecyclePopup.IsOpen = false;
        var lifecycleStage = new Overlay
        {
            Width = Length.Cells(32),
            Height = Length.Cells(6),
            ClipToBounds = false,
            Children = { lifecycleAnchor, lifecyclePopup },
        };

        var styledAnchor = new Button { Content = new Text("Styled popup") };
        var styledPopup = new Popup
        {
            Anchor = styledAnchor,
            Child = new Text("Explicit surface colors"),
            IsOpen = true,
            BorderColor = Color.Indexed(14),
            Background = Color.Indexed(0),
        };
        var styledStage = new Overlay
        {
            Width = Length.Cells(30),
            Height = Length.Cells(6),
            ClipToBounds = false,
            Children = { styledAnchor, styledPopup },
        };

        var resizeAnchor = new Button { Content = new Text("Resize anchor") };
        var resizePopup = new Popup
        {
            Anchor = resizeAnchor,
            Child = new Text("Repositions after layout"),
            IsOpen = true,
            Placement = PopupPlacement.Right,
        };
        var resizeStage = new Overlay
        {
            Width = Length.Cells(34),
            Height = Length.Cells(6),
            ClipToBounds = false,
            Children = { resizeAnchor, resizePopup },
        };

        return Doc.Page(
            Title,
            "Displays one owned child on an opaque bordered surface relative to an optional anchor.",
            Doc.Section(
                "💬",
                "Anchored menu",
                "Compose an owned List below a trigger without adding modal behavior or bypassing normal input routing.",
                Doc.Example(
                    "Action list",
                    "Open with pointer, Enter, or Space; choose with arrows and Enter; Escape closes and restores trigger focus.",
                    overlay,
                    "var popup = new Popup\n{\n    Anchor = trigger,\n    Child = choices,\n    Placement = PopupPlacement.Below,\n};")),
            Doc.Section(
                "💬",
                "Placement",
                "Above, Below, Left, and Right are preferred sides rather than promises to draw outside the host.",
                Doc.Example(
                    "Three alternate sides",
                    "Open each trigger to compare the preferred anchored edge when enough space exists.",
                    variants)),
            Doc.Section(
                "💬",
                "Fallback and clamp",
                "When the preferred side cannot fit, Popup tries the natural opposite side before clamping to its host.",
                Doc.Example(
                    "Lower-edge anchor",
                    "This open popup prefers below but must flip above the trigger inside the short stage.",
                    edgeStage)),
            Doc.Section(
                "💬",
                "Lifecycle",
                "Closing runs while child content is still available; Closed follows after it becomes unavailable.",
                Doc.Example(
                    "Ordered close notifications",
                    "Activate Close lifecycle popup and observe Closing → Closed from the public events.",
                    Doc.Column(lifecycleStage, lifecycleStatus),
                    "popup.Closing += RestoreFocus;\npopup.Closed += ReportClosed;\npopup.IsOpen = false;")),
            Doc.Section(
                "💬",
                "Surface style",
                "Popup clears its complete framed surface using inherited or explicit background and border colors.",
                Doc.Example(
                    "Explicit opaque surface",
                    "The content behind this open popup cannot bleed through its configured background.",
                    styledStage)),
            Doc.Section(
                "💬",
                "Resize",
                "An open Popup participates in the next normal layout pass and recomputes placement before rendering.",
                Doc.Example(
                    "Live anchored bounds",
                    "Resize the terminal while this right-placed popup stays open; it repositions and clamps with its anchor.",
                    resizeStage)));
    }

    private static Overlay PlacementDemo(string label, PopupPlacement placement)
    {
        var trigger = new Button() { Content = new Text(label) };
        var popup = new Popup()
        {
            Anchor = trigger,
            Placement = placement,
            Glyphs = Glyphs.Rounded,
            Child = new Text($"{label} content"),
        };
        trigger.Click += (_, _) => popup.IsOpen = !popup.IsOpen;

        var overlay = new Overlay()
        {
            Width = Length.Cells(14),
            Height = Length.Cells(3),
            ClipToBounds = false,
        };
        overlay.Children.Add(trigger);
        Overlay.SetZIndex(popup, 10);
        overlay.Children.Add(popup);
        return overlay;
    }
}
