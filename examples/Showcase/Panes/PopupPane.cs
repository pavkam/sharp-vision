// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the Popup control with an anchored, keyboard- and pointer-driven action menu.</summary>
internal sealed class PopupPane: CompositeControlBase
{
    internal PopupPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Popup";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        // Anchored action menu with default Dismiss scope and workspace backdrop controls.
        var status = new Text("Selected action: none");
        var scopeStatus = new Text("Modal scope: closed");
        var backdropStatus = new Text("Workspace action: untouched");
        var trigger = new Button { Text = "&Actions ▼" };
        var backdropAction = new Button { Text = "&Workspace action" };
        var choices = new ListView
        {
            Width = Length.Cells(24),
            Height = Length.Cells(5),
            Items = ["Duplicate", "Rename", "Archive", "Delete"],
            SelectedIndex = 0
        };
        var popup = new Popup
        {
            Anchor = trigger,
            Placement = PopupPlacement.Below,
            Border = PopupBorder(BorderGlyphStyle.Rounded),
            Content = choices
        };
        trigger.Click += (_, _) =>
        {
            if (popup.IsOpen)
            {
                popup.IsOpen = false;
                return;
            }

            scopeStatus.Content = "Modal scope: active (Dismiss)";
            popup.IsOpen = true;
        };
        popup.Closed += (_, _) =>
            scopeStatus.Content = "Modal scope: closed; trigger focus restored";
        backdropAction.Click += (_, _) => backdropStatus.Content = "Workspace action: activated";
        choices.ItemInvoked += (_, eventArgs) =>
        {
            status.Content = eventArgs.Item is string choice
                ? $"Selected {choice}."
                : "No action selected.";
            popup.IsOpen = false;
        };

        ShowcasePaneHelpers.Place(trigger, 2, 2);
        ShowcasePaneHelpers.Place(backdropAction, 32, 2);
        ShowcasePaneHelpers.Place(scopeStatus, 2, 12);
        ShowcasePaneHelpers.Place(backdropStatus, 2, 11);
        ShowcasePaneHelpers.Place(status, 2, 13);
        var menuStage = ShowcasePaneHelpers.ApplicationStage(
            52,
            15,
            "Application workspace",
            popup,
            trigger,
            backdropAction,
            scopeStatus,
            backdropStatus,
            status);

        // Four placement buttons around one shared anchor.
        var placementStatus = new Text("Choose a direction to open the preview.");
        var placementAnchor = PlacementControl("⚓ Anc&hor");
        var placementPopup = new Popup
        {
            Anchor = placementAnchor,
            Placement = PopupPlacement.Below,
            Border = PopupBorder(BorderGlyphStyle.Rounded),
            Content = new Text("Placement preview")
        };
        var above = PlacementButton("↑ Abo&ve", PopupPlacement.Above, placementPopup, placementStatus);
        var below = PlacementButton("↓ &Below", PopupPlacement.Below, placementPopup, placementStatus);
        var left = PlacementButton("← &Left", PopupPlacement.Left, placementPopup, placementStatus);
        var right = PlacementButton("&Right →", PopupPlacement.Right, placementPopup, placementStatus);
        var placementSeparator = new Separator { Width = Length.Cells(66) };
        ShowcasePaneHelpers.Place(above, 29, 3);
        ShowcasePaneHelpers.Place(left, 2, 9);
        ShowcasePaneHelpers.Place(placementAnchor, 29, 9);
        ShowcasePaneHelpers.Place(right, 57, 9);
        ShowcasePaneHelpers.Place(below, 29, 16);
        ShowcasePaneHelpers.Place(placementSeparator, 2, 20);
        ShowcasePaneHelpers.Place(placementStatus, 2, 22);
        var placementStage = ShowcasePaneHelpers.ApplicationStage(
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
        placementSurface.Border = PopupBorder(BorderGlyphStyle.Rounded);

        // Lower-edge anchor forces flip-above fallback inside a short host.
        var edgeTrigger = new Button { Text = "&Edge anchor" };
        var edgePopup = new Popup
        {
            Anchor = edgeTrigger,
            Placement = PopupPlacement.Below,
            Content = new Text("Flips above, then clamps")
        };
        ShowcasePaneHelpers.Place(edgeTrigger, 2, 6);
        edgeTrigger.Click += (_, _) => edgePopup.IsOpen = !edgePopup.IsOpen;
        var edgeStage = ShowcasePaneHelpers.ApplicationStage(
            32,
            10,
            "Short workspace\n\n\n\n" +
            "Document behind popup",
            edgePopup,
            edgeTrigger);

        // Closing runs while content is still available; Closed follows afterward.
        var lifecycleStatus = new Text("Lifecycle: closed");
        var lifecycleAnchor = new Button { Text = "&Show lifecycle popup" };
        var lifecyclePopup = new Popup { Anchor = lifecycleAnchor, Content = new Text("Lifecycle content") };
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
        ShowcasePaneHelpers.Place(lifecycleAnchor, 2, 2);
        ShowcasePaneHelpers.Place(lifecycleStatus, 2, 8);
        var lifecycleStage = ShowcasePaneHelpers.ApplicationStage(
            38,
            10,
            "Settings\n\n\n\n\n" +
            "Task status: running",
            lifecyclePopup,
            lifecycleAnchor,
            lifecycleStatus);

        // Open popup recomputes placement on the next layout pass after resize.
        var resizeAnchor = new Button { Text = "Resi&ze anchor" };
        var resizePopup = new Popup
        {
            Anchor = resizeAnchor,
            Content = new Text("Repositions after layout"),
            Placement = PopupPlacement.Right
        };
        resizeAnchor.Click += (_, _) => resizePopup.IsOpen = !resizePopup.IsOpen;
        ShowcasePaneHelpers.Place(resizeAnchor, 2, 3);
        var resizeStage = ShowcasePaneHelpers.ApplicationStage(
            48,
            9,
            "Responsive workspace\n\n\n\n\n\n" +
            "Resize the terminal while this is open",
            resizePopup,
            resizeAnchor);

        return new DocPage(
            Title,
            "<info>Popup</info> displays one owned child on an opaque bordered surface relative to an optional <info>PlacementTarget</info>.",
            new DocSection(
                "💬",
                "Anchored menu",
                "Opening automatically gives the action list a Dismiss plane. An outside press closes the popup without replaying that press to the workspace.",
                new DocExample(
                    "Action list",
                    "Open with pointer, <reverse>Enter</reverse>, or <reverse>Space</reverse>; choose with arrows and <reverse>Enter</reverse>, or press Workspace action to <warning>dismiss</warning> without activating it. <reverse>Escape</reverse> also closes and restores trigger focus.",
                    menuStage,
                    "popup.IsOpen = true; // Dismiss modality is automatic")),
            new DocSection(
                "📍",
                "Placement",
                "Above, Below, Left, and Right are preferred sides rather than promises to draw outside the host.",
                new DocExample(
                    "Four sides, one anchor",
                    "Choose Above, Below, Left, or Right. The action opens one Popup around the same central anchor and reports the requested side.",
                    placementStage)),
            new DocSection(
                "↩️",
                "Fallback and clamp",
                "When the preferred side cannot fit, Popup tries the natural opposite side before clamping to its host.",
                new DocExample(
                    "Lower-edge anchor",
                    "Activate Edge anchor. The popup prefers below but must flip above the trigger inside the short stage.",
                    edgeStage)),
            new DocSection(
                "🔄",
                "Lifecycle",
                "Closing runs while child content is still available; Closed follows after it becomes unavailable.",
                new DocExample(
                    "Ordered close notifications",
                    "Activate Show lifecycle popup to open it, then activate again and observe Closing → Closed from the public events.",
                    lifecycleStage,
                    "popup.Closing += RestoreFocus;\npopup.Closed += ReportClosed;\npopup.IsOpen = false;")),
            new DocSection(
                "📐",
                "Resize",
                "An open Popup participates in the next normal layout pass and recomputes placement before rendering.",
                new DocExample(
                    "Live anchored bounds",
                    "Activate Resize anchor, then resize the terminal; the open popup repositions and clamps with its anchor.",
                    resizeStage)));
    }

    private static Border PopupBorder(BorderGlyphStyle glyphStyle) => new(
        BorderSide.All,
        glyphStyle,
        SemanticColor.ControlBorder,
        Color.Transparent,
        SemanticDecoration.Border);

    private static Button PlacementButton(
        string label,
        PopupPlacement placement,
        Popup popup,
        Text status)
    {
        var button = PlacementControl(label);
        button.Click += (_, _) =>
        {
            popup.Placement = placement;
            popup.IsOpen = true;
            status.Content = $"Requested side: {placement}";
        };

        return button;
    }

    private static Button PlacementControl(string label) => new()
    {
        Text = label,
        Padding = new Thickness(1, 0)
    };
}
