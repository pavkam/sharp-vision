// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the Flyout control with light-dismiss, placement, and lifecycle specimens.</summary>
internal sealed class FlyoutPane: CompositeControl
{
    internal FlyoutPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Flyout";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        // Basic flyout with action content.
        var status = new Text("No action taken.");
        var trigger = new Button { Content = new Text("&Options") };
        var confirmButton = new Button { Content = new Text("&Confirm") };
        var cancelButton = new Button { Content = new Text("C&ancel") };
        var actionRow = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { confirmButton, cancelButton }
        };
        var flyoutContent = new Stack
        {
            Spacing = 1,
            Width = Length.Cells(28),
            Children =
            {
                new Text("Choose an action:"),
                actionRow
            }
        };
        var flyout = new Flyout
        {
            Anchor = trigger,
            Placement = PopupPlacement.Below,
            ShowAnchorIndicator = true,
            Content = flyoutContent
        };
        trigger.Click += (_, _) => flyout.IsOpen = !flyout.IsOpen;
        confirmButton.Click += (_, _) =>
        {
            status.Content = "Action confirmed.";
            flyout.IsOpen = false;
        };
        cancelButton.Click += (_, _) =>
        {
            status.Content = "Action cancelled.";
            flyout.IsOpen = false;
        };
        ShowcasePaneHelpers.Place(trigger, 2, 2);
        ShowcasePaneHelpers.Place(status, 2, 12);
        var basicStage = ShowcasePaneHelpers.ApplicationStage(
            40,
            14,
            "Application workspace",
            flyout,
            trigger,
            status);

        // ShowAt convenience method.
        var showAtStatus = new Text("Click any button to open the flyout at its position.");
        var showAtFlyout = new Flyout
        {
            ShowAnchorIndicator = true,
            Content = new Text("Anchored here") { Padding = new Thickness(1, 0) }
        };
        var buttonA = new Button { Content = new Text("Button &A") };
        var buttonB = new Button { Content = new Text("Button &B") };
        var buttonC = new Button { Content = new Text("Button &C") };
        buttonA.Click += (_, _) =>
        {
            showAtFlyout.ShowAt(buttonA);
            showAtStatus.Content = "Opened at Button A.";
        };
        buttonB.Click += (_, _) =>
        {
            showAtFlyout.ShowAt(buttonB);
            showAtStatus.Content = "Opened at Button B.";
        };
        buttonC.Click += (_, _) =>
        {
            showAtFlyout.ShowAt(buttonC);
            showAtStatus.Content = "Opened at Button C.";
        };
        ShowcasePaneHelpers.Place(buttonA, 2, 2);
        ShowcasePaneHelpers.Place(buttonB, 16, 2);
        ShowcasePaneHelpers.Place(buttonC, 30, 2);
        ShowcasePaneHelpers.Place(showAtStatus, 2, 9);
        var showAtStage = ShowcasePaneHelpers.ApplicationStage(
            46,
            11,
            "Application workspace",
            showAtFlyout,
            buttonA,
            buttonB,
            buttonC,
            showAtStatus);

        // Lifecycle events.
        var lifecycleLog = new Text("Lifecycle: idle");
        var lifecycleTrigger = new Button { Content = new Text("&Toggle flyout") };
        var lifecycleFlyout = new Flyout
        {
            Anchor = lifecycleTrigger,
            ShowAnchorIndicator = true,
            Content = new Text("Flyout content") { Padding = new Thickness(1, 0) }
        };
        lifecycleTrigger.Click += (_, _) => lifecycleFlyout.IsOpen = !lifecycleFlyout.IsOpen;
        lifecycleFlyout.Closing += (_, _) => lifecycleLog.Content = "Lifecycle: Closing";
        lifecycleFlyout.Closed += (_, _) => lifecycleLog.Content += " → Closed";
        ShowcasePaneHelpers.Place(lifecycleTrigger, 2, 2);
        ShowcasePaneHelpers.Place(lifecycleLog, 2, 9);
        var lifecycleStage = ShowcasePaneHelpers.ApplicationStage(
            40,
            11,
            "Application workspace",
            lifecycleFlyout,
            lifecycleTrigger,
            lifecycleLog);

        // Placement demo.
        var placementAnchor = new Button
        {
            Content = new Text("A&nchor"),
            Padding = new Thickness(1, 0)
        };
        var placementFlyout = new Flyout
        {
            Anchor = placementAnchor,
            ShowAnchorIndicator = true,
            Content = new Text("Flyout content") { Padding = new Thickness(1, 0) }
        };
        var placementStatus = new Text("Choose a direction.");
        var aboveBtn = PlacementButton("↑ Abo&ve", PopupPlacement.Above, placementFlyout, placementStatus);
        var belowBtn = PlacementButton("↓ Be&low", PopupPlacement.Below, placementFlyout, placementStatus);
        var leftBtn = PlacementButton("← Le&ft", PopupPlacement.Left, placementFlyout, placementStatus);
        var rightBtn = PlacementButton("→ &Right", PopupPlacement.Right, placementFlyout, placementStatus);
        ShowcasePaneHelpers.Place(aboveBtn, 22, 3);
        ShowcasePaneHelpers.Place(leftBtn, 2, 8);
        ShowcasePaneHelpers.Place(placementAnchor, 22, 8);
        ShowcasePaneHelpers.Place(rightBtn, 43, 8);
        ShowcasePaneHelpers.Place(belowBtn, 22, 13);
        ShowcasePaneHelpers.Place(placementStatus, 2, 17);
        var placementStage = ShowcasePaneHelpers.ApplicationStage(
            60,
            19,
            "Placement diagram",
            placementFlyout,
            aboveBtn,
            leftBtn,
            placementAnchor,
            rightBtn,
            belowBtn,
            placementStatus);

        return new DocPage(
            Title,
            "<info>Flyout</info> displays anchored overlay content with automatic light dismiss. Simpler than Popup — always dismisses on outside press, no modal support.",
            new DocSection(
                "💬",
                "Basic flyout",
                "A flyout presents contextual actions anchored to a trigger. Click outside or press <reverse>Escape</reverse> to dismiss.",
                new DocExample(
                    "Action panel",
                    "Activate Options to open the flyout. Choose Confirm or Cancel, or click outside the flyout to dismiss it. <reverse>Escape</reverse> also closes.",
                    basicStage,
                    "var flyout = new Flyout\n{\n    Anchor = trigger,\n    ShowAnchorIndicator = true,\n    Content = actions\n};\nflyout.IsOpen = true;")),
            new DocSection(
                "📌",
                "ShowAt",
                "<info>ShowAt</info> sets the anchor and opens in one call, making it easy to share a single flyout across multiple triggers.",
                new DocExample(
                    "Shared flyout",
                    "Click any button to open the same flyout anchored at that position. The ▲ arrow indicates the anchor.",
                    showAtStage,
                    "flyout.ShowAt(buttonA);")),
            new DocSection(
                "🔄",
                "Lifecycle",
                "<info>Closing</info> fires while content is still visible; <info>Closed</info> follows after collapse.",
                new DocExample(
                    "Ordered events",
                    "Toggle the flyout open, then close it by clicking outside or pressing Escape. Observe the Closing → Closed sequence.",
                    lifecycleStage,
                    "flyout.Closing += (_, _) => Log(\"Closing\");\nflyout.Closed += (_, _) => Log(\"Closed\");")),
            new DocSection(
                "📍",
                "Placement",
                "Set <info>Placement</info> to control which side of the anchor the flyout prefers. It flips when space is limited.",
                new DocExample(
                    "Four sides",
                    "Choose Above, Below, Left, or Right. The flyout opens on the selected side of the central anchor. The arrow indicator points toward the anchor.",
                    placementStage)));
    }

    private static Button PlacementButton(
        string label,
        PopupPlacement placement,
        Flyout flyout,
        Text status)
    {
        var button = new Button
        {
            Content = new Text(label),
            Padding = new Thickness(1, 0)
        };
        button.Click += (_, _) =>
        {
            flyout.Placement = placement;
            flyout.IsOpen = true;
            status.Content = $"Requested side: {placement}";
        };

        return button;
    }
}
