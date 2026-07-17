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
        var status = new Text("Selected action: none");
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

        Place(trigger, 2, 2);
        Place(status, 2, 13);
        var menuStage = ApplicationStage(
            52,
            15,
            "Projects  Search  Settings\n\n\n\n\n" +
            "release notes · now\n" +
            "roadmap · 2m ago",
            popup,
            trigger,
            status);

        var placementStatus = new Text("Choose a direction to open the preview.");
        var placementAnchor = PlacementControl("⚓ Anchor", ColorRole.Accent);
        var placementPopup = new Popup
        {
            Anchor = placementAnchor,
            Placement = PopupPlacement.Below,
            Glyphs = Glyphs.Rounded,
            Content = new Text("Placement preview"),
        };
        var above = PlacementButton("↑ Above", PopupPlacement.Above, placementPopup, placementStatus);
        var below = PlacementButton("↓ Below", PopupPlacement.Below, placementPopup, placementStatus);
        var left = PlacementButton("← Left", PopupPlacement.Left, placementPopup, placementStatus);
        var right = PlacementButton("Right →", PopupPlacement.Right, placementPopup, placementStatus);
        var placementSeparator = new Separator { Width = Length.Cells(66) };
        Place(above, 29, 3);
        Place(left, 2, 9);
        Place(placementAnchor, 29, 9);
        Place(right, 57, 9);
        Place(below, 29, 16);
        Place(placementSeparator, 2, 20);
        Place(placementStatus, 2, 22);
        var placementStage = ApplicationStage(
            70,
            24,
            "Popup placement diagram",
            placementPopup,
            above,
            left,
            placementAnchor,
            right,
            below,
            placementSeparator,
            placementStatus);
        var placementSurface = (Dock) placementStage.Children[0];
        placementSurface.BorderGlyphs = Glyphs.Rounded;
        placementSurface.BorderColor = ColorRole.Accent;

        var edgeTrigger = new Button { Content = new Text("Edge anchor") };
        var edgePopup = new Popup
        {
            Anchor = edgeTrigger,
            Placement = PopupPlacement.Below,
            Content = new Text("Flips above, then clamps"),
        };
        Place(edgeTrigger, 2, 6);
        edgeTrigger.Click += (_, _) => edgePopup.IsOpen = !edgePopup.IsOpen;
        var edgeStage = ApplicationStage(
            32,
            10,
            "Short workspace\n\n\n\n" +
            "Document behind popup",
            edgePopup,
            edgeTrigger);

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
        Place(lifecycleAnchor, 2, 2);
        Place(lifecycleStatus, 2, 8);
        var lifecycleStage = ApplicationStage(
            38,
            10,
            "Settings\n\n\n\n\n" +
            "Task status: running",
            lifecyclePopup,
            lifecycleAnchor,
            lifecycleStatus);

        var styledAnchor = new Button { Content = new Text("Styled popup") };
        var styledPopup = new Popup
        {
            Anchor = styledAnchor,
            Content = new Text("Explicit surface colors"),
            BorderColor = Color.Indexed(14),
            Background = Color.Indexed(0),
        };
        styledAnchor.Click += (_, _) => styledPopup.IsOpen = !styledPopup.IsOpen;
        Place(styledAnchor, 2, 2);
        var styledStage = ApplicationStage(
            38,
            9,
            "Theme preview\n\n\n\n\n" +
            "Content behind popup",
            styledPopup,
            styledAnchor);

        var resizeAnchor = new Button { Content = new Text("Resize anchor") };
        var resizePopup = new Popup
        {
            Anchor = resizeAnchor,
            Content = new Text("Repositions after layout"),
            Placement = PopupPlacement.Right,
        };
        resizeAnchor.Click += (_, _) => resizePopup.IsOpen = !resizePopup.IsOpen;
        Place(resizeAnchor, 2, 3);
        var resizeStage = ApplicationStage(
            48,
            9,
            "Responsive workspace\n\n\n\n\n\n" +
            "Resize the terminal while this is open",
            resizePopup,
            resizeAnchor);

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
                    menuStage,
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
                    lifecycleStage,
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

    private static Overlay ApplicationStage(
        int width,
        int height,
        string content,
        Popup popup,
        params Control[] controls)
    {
        var interactions = new Canvas { ClipToBounds = false };
        foreach (var control in controls)
        {
            interactions.Children.Add(control);
        }

        var stage = new Overlay
        {
            Width = Length.Cells(width),
            Height = Length.Cells(height),
            ClipToBounds = true,
            Children =
            {
                ApplicationSurface(content),
                interactions,
                popup,
            },
        };

        return stage;
    }

    private static Dock ApplicationSurface(string content) => new()
    {
        Background = ColorRole.Surface,
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
        var button = PlacementControl(label, ColorRole.Border);
        button.Click += (_, _) =>
        {
            popup.Placement = placement;
            popup.IsOpen = true;
            status.Content = $"Requested side: {placement}";
        };

        return button;
    }

    private static Button PlacementControl(string label, ColorRole borderColor) => new()
    {
        Content = new Text(label),
        Background = ColorRole.Surface,
        BorderThickness = new Thickness(1),
        BorderGlyphs = Glyphs.Rounded,
        BorderColor = borderColor,
        Padding = new Thickness(1, 0),
    };

    private static void Place(Control control, int left, int top)
    {
        Canvas.SetLeft(control, Length.Cells(left));
        Canvas.SetTop(control, Length.Cells(top));
    }
}
