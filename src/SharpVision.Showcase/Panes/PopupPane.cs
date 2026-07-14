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
        Text status = new("Choose an item with the mouse, arrows, or Enter.");
        Button trigger = new() { Content = new Text("Actions ▼") };
        List choices = new()
        {
            Width = Length.Cells(24),
            Height = Length.Cells(5),
            Items = ["Duplicate", "Rename", "Archive", "Delete"],
            SelectedIndex = 0,
        };
        Popup popup = new()
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

        Overlay overlay = new() { ClipToBounds = false };
        overlay.Children.Add(Doc.Column(trigger, status));
        Overlay.SetZIndex(popup, 10);
        overlay.Children.Add(popup);

        Stack variants = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
        variants.Children.Add(PlacementDemo("Above", PopupPlacement.Above));
        variants.Children.Add(PlacementDemo("Left", PopupPlacement.Left));
        variants.Children.Add(PlacementDemo("Right", PopupPlacement.Right));

        return Doc.Page(
            Title,
            "Displays one owned child on an opaque bordered surface relative to an optional anchor.",
            Doc.Example(
                "Anchored action menu",
                "Click the trigger, or focus it and press Enter or Space, to open the compact list anchored below it. Arrow keys and Enter navigate and choose inside the popup; Escape closes it without selecting anything and restores focus to the trigger.",
                overlay),
            Doc.Example(
                "Placement variants",
                "PopupPlacement also offers Above, Left, and Right; each trigger below opens its popup on that preferred side of its own anchor when space permits.",
                variants));
    }

    private static Overlay PlacementDemo(string label, PopupPlacement placement)
    {
        Button trigger = new() { Content = new Text(label) };
        Popup popup = new()
        {
            Anchor = trigger,
            Placement = placement,
            Glyphs = Glyphs.Rounded,
            Child = new Text($"{label} content"),
        };
        trigger.Click += (_, _) => popup.IsOpen = !popup.IsOpen;

        Overlay overlay = new()
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
