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

        return Doc.Page(
            Title,
            "Displays one owned child on an opaque bordered surface relative to an optional anchor.",
            Doc.Example(
                "Anchored action menu",
                "Click the trigger, or focus it and press Enter or Space, to open the compact list anchored below it. Arrow keys and Enter navigate and choose inside the popup; Escape closes it without selecting anything and restores focus to the trigger.",
                overlay));
    }
}
