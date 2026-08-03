// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the Tooltip control with hover, focus, and rich-content specimens.</summary>
internal sealed class TooltipPane: CompositeControlBase
{
    internal TooltipPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Tooltip";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        // Text tooltips on a row of buttons.
        var saveButton = new Button { Content = new Text("&Save") };
        var copyButton = new Button { Content = new Text("&Copy") };
        var deleteButton = new Button { Content = new Text("&Delete") };
        Tooltip.SetText(saveButton, "Save the current document");
        Tooltip.SetText(copyButton, "Copy selection to clipboard");
        Tooltip.SetText(deleteButton, "Delete the selected item");
        var hoverStatus = new Text("<d>Hover over or focus a button to see its tooltip.</d>")
        {
            Overflow = Overflow.Wrap
        };
        var buttonRow = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children = { saveButton, copyButton, deleteButton }
        };
        var basicDemo = new Stack { Spacing = 1, Children = { buttonRow, hoverStatus } };

        // Rich content tooltip.
        var richAnchor = new Button
        {
            Content = new Text("&User info"),
            Padding = new Thickness(1, 0)
        };
        var richContent = new Stack
        {
            Spacing = 0,
            Children =
            {
                new Text("<b>Alex Ciobanu</b>") { Overflow = Overflow.Wrap },
                new Text("<d>Software Engineer</d>"),
                new Text("<d>Team: Runtime</d>")
            }
        };
        Tooltip.SetContent(richAnchor, richContent);
        var richDemo = new Stack { Spacing = 1, Children = { richAnchor } };

        // Configurable delay demo.
        var fastButton = new Button { Content = new Text("&Fast (100ms)") };
        var normalButton = new Button { Content = new Text("&Normal (500ms)") };
        var slowButton = new Button { Content = new Text("S&low (1500ms)") };
        Tooltip.SetText(fastButton, "Appears quickly", PopupPlacement.Below, TimeSpan.FromMilliseconds(100));
        Tooltip.SetText(normalButton, "Default delay");
        Tooltip.SetText(slowButton, "Slow to appear", PopupPlacement.Below, TimeSpan.FromMilliseconds(1500));
        var delayRow = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children = { fastButton, normalButton, slowButton }
        };
        var delayDemo = new Stack
        {
            Spacing = 1,
            Children =
            {
                delayRow,
                new Text("<d>Each button has a different ShowDelay. Hover to compare.</d>")
                {
                    Overflow = Overflow.Wrap
                }
            }
        };

        // Placement demo.
        var belowAnchor = new Button { Content = new Text("B&elow") };
        var aboveAnchor = new Button { Content = new Text("&Above") };
        var rightAnchor = new Button { Content = new Text("&Right") };
        var leftAnchor = new Button { Content = new Text("Lef&t") };
        Tooltip.SetText(belowAnchor, "Placed below", PopupPlacement.Below);
        Tooltip.SetText(aboveAnchor, "Placed above", PopupPlacement.Above);
        Tooltip.SetText(rightAnchor, "Placed right", PopupPlacement.Right);
        Tooltip.SetText(leftAnchor, "Placed left", PopupPlacement.Left);
        var placementRow = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children = { belowAnchor, aboveAnchor, rightAnchor, leftAnchor }
        };
        var placementDemo = new Stack
        {
            Spacing = 1,
            Children =
            {
                placementRow,
                new Text("<d>Each button places its tooltip on a different side.</d>")
                {
                    Overflow = Overflow.Wrap
                }
            }
        };

        return new DocPage(
            Title,
            "<info>Tooltip</info> displays a brief overlay anchored to any control, triggered by pointer hover or keyboard focus after a configurable delay.",
            new DocSection(
                "💬",
                "Text tooltips",
                "Attach a text tooltip to any control with <info>Tooltip.SetText</info>. The tooltip appears after <info>ShowDelay</info> on hover or focus, and hides on exit or press.",
                new DocExample(
                    "Button toolbar",
                    "Hover over or tab to each button. The tooltip appears after the default 500ms delay and closes on pointer exit or any press.",
                    new DocCard(basicDemo),
                    "Tooltip.SetText(button, \"Save the current document\");")),
            new DocSection(
                "🖼",
                "Rich content",
                "Use <info>Tooltip.SetContent</info> to display an arbitrary control subtree instead of plain text.",
                new DocExample(
                    "User profile card",
                    "Hover over or focus the button to see a multi-line profile card tooltip.",
                    new DocCard(richDemo),
                    "var card = new Stack { ... };\nTooltip.SetContent(anchor, card);")),
            new DocSection(
                "⏱",
                "Show delay",
                "Each tooltip can have its own <info>ShowDelay</info> controlling how long the pointer must rest before the tooltip appears.",
                new DocExample(
                    "Configurable delays",
                    "Compare Fast (100ms), Normal (500ms default), and Slow (1500ms) tooltip delays.",
                    new DocCard(delayDemo),
                    "var tip = Tooltip.GetTooltip(button)!;\ntip.ShowDelay = TimeSpan.FromMilliseconds(100);")),
            new DocSection(
                "📍",
                "Placement",
                "Set the <info>Placement</info> property to control which side of the anchor the tooltip prefers. The tooltip flips to the opposite side when space is limited.",
                new DocExample(
                    "Four sides",
                    "Hover each button to see tooltips placed Below, Above, Right, and Left.",
                    new DocCard(placementDemo),
                    "Tooltip.GetTooltip(button)!.Placement = PopupPlacement.Above;")));
    }
}
