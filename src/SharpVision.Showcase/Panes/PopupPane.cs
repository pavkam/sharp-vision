// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the Popup control with an anchored, keyboard- and pointer-driven action menu.</summary>
internal sealed class PopupPane: CompositeControl
{

    internal PopupPane() => InitializeContent(CreateContent());
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Popup";

    /// <inheritdoc/>
    private static Dock CreateContent()
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
            Content = choices,
        };
        trigger.Click += (_, _) => popup.IsOpen = !popup.IsOpen;
        choices.ItemInvoked += (_, eventArgs) =>
        {
            status.Content = eventArgs.Item is string choice
                ? $"Selected {choice}."
                : "No action selected.";
            popup.IsOpen = false;
        };

        var menuControls = Doc.Column(trigger, status);
        menuControls.Margin = new Thickness(2, 2, 0, 0);
        var overlay = new Overlay
        {
            Width = Length.Cells(52),
            Height = Length.Cells(13),
            ClipToBounds = false,
            Children =
            {
                ApplicationSurface(
                    "Projects  Search  Settings\n\n" +
                    "release-notes.md      edited now\n" +
                    "roadmap.md            edited 2m ago\n\n" +
                    "2 files · branch main"),
                menuControls,
            },
        };
        Overlay.SetZIndex(popup, 10);
        overlay.Children.Add(popup);

        var placementStatus = new Text("Choose a side to open the preview.");
        var placementAnchor = new Button { Content = new Text("Preview anchor") };
        var placementPopup = new Popup
        {
            Width = Length.Star(1),
            Height = Length.Star(1),
            Anchor = placementAnchor,
            Placement = PopupPlacement.Below,
            Glyphs = Glyphs.Rounded,
            Content = new Text("Placement preview"),
        };
        var above = PlacementButton("Above", PopupPlacement.Above, placementPopup, placementStatus);
        var below = PlacementButton("Below", PopupPlacement.Below, placementPopup, placementStatus);
        var left = PlacementButton("Left", PopupPlacement.Left, placementPopup, placementStatus);
        var right = PlacementButton("Right", PopupPlacement.Right, placementPopup, placementStatus);
        var placementCanvas = new Canvas
        {
            Width = Length.Cells(64),
            Height = Length.Cells(22),
            ClipToBounds = false,
        };
        Place(above, 24, 1);
        Place(left, 2, 8);
        Place(placementAnchor, 24, 8);
        Place(right, 50, 8);
        Place(below, 24, 15);
        Place(placementStatus, 2, 19);
        placementCanvas.Children.Add(above);
        placementCanvas.Children.Add(left);
        placementCanvas.Children.Add(placementAnchor);
        placementCanvas.Children.Add(right);
        placementCanvas.Children.Add(below);
        placementCanvas.Children.Add(placementStatus);
        placementCanvas.Children.Add(placementPopup);
        var placementStage = new Overlay
        {
            Width = Length.Cells(64),
            Height = Length.Cells(22),
            ClipToBounds = false,
            Children =
            {
                ApplicationSurface(
                    "Files  Edit  View  Run  Help\n\n" +
                    "src/Controls/Popup.cs\n" +
                    "tests/PopupTests.cs\n\n" +
                    "Ready · 2 changes · Ln 48, Col 12"),
                placementCanvas,
            },
        };

        var edgeTrigger = new Button { Content = new Text("Edge anchor") };
        var edgePopup = new Popup
        {
            Anchor = edgeTrigger,
            Placement = PopupPlacement.Below,
            Content = new Text("Flips above, then clamps"),
        };
        var edgeStage = new Overlay
        {
            Width = Length.Cells(28),
            Height = Length.Cells(5),
            ClipToBounds = false,
            Children =
            {
                ApplicationSurface("Short workspace\n\nAnchor stays near the lower edge"),
            },
        };
        edgeTrigger.VerticalAlignment = VerticalAlignment.Bottom;
        edgeTrigger.Click += (_, _) => edgePopup.IsOpen = !edgePopup.IsOpen;
        edgeStage.Children.Add(edgeTrigger);
        Overlay.SetZIndex(edgePopup, 10);
        edgeStage.Children.Add(edgePopup);

        var lifecycleStatus = new Text("Lifecycle: closed");
        var lifecycleAnchor = new Button { Content = new Text("Show lifecycle popup") };
        var lifecyclePopup = new Popup
        {
            Anchor = lifecycleAnchor,
            Content = new Text("Lifecycle content"),
        };
        lifecyclePopup.Closing += (_, _) => lifecycleStatus.Content = "Lifecycle: Closing";
        lifecyclePopup.Closed += (_, _) => lifecycleStatus.Content += " → Closed";
        lifecycleAnchor.Click += (_, _) =>
        {
            if (lifecyclePopup.IsOpen)
            {
                lifecyclePopup.IsOpen = false;
            }
            else
            {
                lifecycleStatus.Content = "Lifecycle: open";
                lifecyclePopup.IsOpen = true;
            }
        };
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
            Content = new Text("Explicit surface colors"),
            BorderColor = Color.Indexed(14),
            Background = Color.Indexed(0),
        };
        styledAnchor.Click += (_, _) => styledPopup.IsOpen = !styledPopup.IsOpen;
        var styledStage = new Overlay
        {
            Width = Length.Cells(30),
            Height = Length.Cells(6),
            ClipToBounds = false,
            Children =
            {
                ApplicationSurface("Theme surface behind explicit popup chrome"),
                styledAnchor,
                styledPopup,
            },
        };

        var resizeAnchor = new Button { Content = new Text("Resize anchor") };
        var resizePopup = new Popup
        {
            Anchor = resizeAnchor,
            Content = new Text("Repositions after layout"),
            Placement = PopupPlacement.Right,
        };
        resizeAnchor.Click += (_, _) => resizePopup.IsOpen = !resizePopup.IsOpen;
        var resizeStage = new Overlay
        {
            Width = Length.Cells(34),
            Height = Length.Cells(6),
            ClipToBounds = false,
            Children =
            {
                ApplicationSurface("Resize the host after opening the popup"),
                resizeAnchor,
                resizePopup,
            },
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
                    "var popup = new Popup\n{\n    Anchor = trigger,\n    Content = choices,\n    Placement = PopupPlacement.Below,\n};")),
            Doc.Section(
                "💬",
                "Placement",
                "Above, Below, Left, and Right are preferred sides rather than promises to draw outside the host.",
                Doc.Example(
                    "Four sides, one anchor",
                    "Choose Above, Below, Left, or Right. The action opens one Popup around the same central anchor and reports the requested side.",
                    placementStage)),
            Doc.Section(
                "💬",
                "Fallback and clamp",
                "When the preferred side cannot fit, Popup tries the natural opposite side before clamping to its host.",
                Doc.Example(
                    "Lower-edge anchor",
                    "Activate Edge anchor. The popup prefers below but must flip above the trigger inside the short stage.",
                    edgeStage)),
            Doc.Section(
                "💬",
                "Lifecycle",
                "Closing runs while child content is still available; Closed follows after it becomes unavailable.",
                Doc.Example(
                    "Ordered close notifications",
                    "Activate Show lifecycle popup to open it, then activate again and observe Closing → Closed from the public events.",
                    Doc.Column(lifecycleStage, lifecycleStatus),
                    "popup.Closing += RestoreFocus;\npopup.Closed += ReportClosed;\npopup.IsOpen = false;")),
            Doc.Section(
                "💬",
                "Surface style",
                "Popup clears its complete framed surface using inherited or explicit background and border colors.",
                Doc.Example(
                    "Explicit opaque surface",
                    "Activate Styled popup. Content behind the promoted surface cannot bleed through its configured background.",
                    styledStage)),
            Doc.Section(
                "💬",
                "Resize",
                "An open Popup participates in the next normal layout pass and recomputes placement before rendering.",
                Doc.Example(
                    "Live anchored bounds",
                    "Activate Resize anchor, then resize the terminal; the open popup repositions and clamps with its anchor.",
                    resizeStage)));
    }

    private static Dock ApplicationSurface(string content) => new()
    {
        Background = ThemeColors.Surface,
        FillMode = FillMode.Opaque,
        BorderThickness = new Thickness(1),
        BorderGlyphs = Glyphs.Light,
        Padding = new Thickness(1, 0),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        Children = { new Text(content) },
    };

    private static Button PlacementButton(
        string label,
        PopupPlacement placement,
        Popup popup,
        Text status)
    {
        var button = new Button { Content = new Text(label) };
        button.Click += (_, _) =>
        {
            popup.Placement = placement;
            popup.IsOpen = true;
            status.Content = $"Requested side: {label}";
        };

        return button;
    }

    private static void Place(Control control, int left, int top)
    {
        Canvas.SetLeft(control, Length.Cells(left));
        Canvas.SetTop(control, Length.Cells(top));
    }
}
