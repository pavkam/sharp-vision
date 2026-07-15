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

        var overlay = new Overlay() { ClipToBounds = false };
        overlay.Children.Add(Doc.Column(trigger, status));
        Overlay.SetZIndex(popup, 10);
        overlay.Children.Add(popup);

        var variants = new Stack() { Orientation = Orientation.Horizontal, Spacing = 6 };
        variants.Children.Add(PlacementDemo("Above", PopupPlacement.Above));
        variants.Children.Add(PlacementDemo("Left", PopupPlacement.Left));
        variants.Children.Add(PlacementDemo("Right", PopupPlacement.Right));

        return Doc.Page(
            Title,
            "Displays one owned content control on an opaque bordered surface relative to an optional anchor.",
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
        var trigger = new Button() { Content = new Text(label) };
        var popup = new Popup()
        {
            Anchor = trigger,
            Placement = placement,
            Glyphs = Glyphs.Rounded,
            Content = new Text($"{label} content"),
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
