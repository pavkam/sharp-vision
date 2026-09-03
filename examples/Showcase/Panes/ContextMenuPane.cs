// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the ContextMenu control with custom and built-in text input context menus.</summary>
internal sealed class ContextMenuPane: CompositeControlBase
{
    internal ContextMenuPane() => InitializeContent(CreateContent());

    internal const string Title = "ContextMenu";

    private static DocPage CreateContent()
    {
        var status = new Text("Right-click a control to open its context menu.");

        var customMenu = new ContextMenu
        {
            FadeInDuration = TimeSpan.FromMilliseconds(120),
            FadeOutDuration = TimeSpan.FromMilliseconds(160)
        };
        customMenu.Items.Add(new MenuItem { Text = "&Inspect" });
        customMenu.Items.Add(new MenuItem { Text = "&Run", ShortcutText = "F5" });
        customMenu.Items.Add(new MenuItem { Text = "&Debug", ShortcutText = "F9" });
        customMenu.Items.Add(new MenuSeparator());
        customMenu.Items.Add(new MenuItem { Text = "De&ploy", IsEnabled = false });
        customMenu.Opening += (_, _) =>
            status.Content = "Context menu opened.";
        customMenu.Closed += (_, _) =>
            status.Content = "Context menu closed.";
        var target = new Button
        {
            Text = "Right-click &me",
            Width = Length.Cells(20),
            Height = Length.Cells(3),
            ContextMenu = customMenu
        };
        ShowcasePaneHelpers.Place(target, 2, 2);
        ShowcasePaneHelpers.Place(status, 2, 10);
        var customStage = new Overlay
        {
            Width = Length.Cells(34),
            Height = Length.Cells(12),
            ClipToBounds = true,
            Children =
            {
                ShowcasePaneHelpers.ApplicationSurface(
                    "Workspace\n\n\n\n\n\n\n\n\nContext menu closed."),
                target,
                status
            }
        };

        var textInput = new TextInput
        {
            Text = "Right-click for edit menu",
            Width = Length.Cells(30)
        };

        var editMenu = new ContextMenu();
        editMenu.Items.Add(new MenuItem { Text = "Cu&t", ShortcutText = "Ctrl+X" });
        editMenu.Items.Add(new MenuItem { Text = "&Copy", ShortcutText = "Ctrl+C" });
        editMenu.Items.Add(new MenuItem { Text = "&Paste", ShortcutText = "Ctrl+V" });
        editMenu.Items.Add(new MenuSeparator());
        editMenu.Items.Add(new MenuItem { Text = "Select &All", ShortcutText = "Ctrl+A" });
        var editTarget = new Button
        {
            Text = "&Edit-style menu",
            Width = Length.Cells(20),
            Height = Length.Cells(3),
            ContextMenu = editMenu
        };

        var builderStatus = new Text("Right-click a control to open its context menu.");
        var builderMenu = new ContextMenu(
            MenuBuilder.Vertical()
                .Item("&Inspect")
                .Item("&Run", shortcut: "F5")
                .Item("&Debug", shortcut: "F9")
                .Separator()
                .Item("De&ploy", isEnabled: false)
                .Build());
        builderMenu.Opening += (_, _) =>
            builderStatus.Content = "Context menu opened.";
        builderMenu.Closed += (_, _) =>
            builderStatus.Content = "Context menu closed.";
        var builderTarget = new Button
        {
            Text = "&Builder-composed menu",
            Width = Length.Cells(20),
            Height = Length.Cells(3),
            ContextMenu = builderMenu
        };

        return new DocPage(
            Title,
            "<info>ContextMenu</info> displays a vertical menu at the pointer position on secondary (right) click. Any control supports it through the <info>ContextMenu</info> property. <info>TextInput</info> attaches one by default with clipboard and editing commands.",
            new DocSection(
                "🖱️",
                "Custom context menu",
                "Right-click the button to open a context menu with action items. Shared Popup fades preserve the menu plane through visual entrance and dismissal. The <warning>Deploy</warning> entry is disabled and cannot be invoked.",
                new DocExample(
                    "Right-click target",
                    "Right-click the button to see the context menu. <reverse>Escape</reverse> or clicking outside dismisses it.",
                    customStage,
                    "menu.FadeInDuration = TimeSpan.FromMilliseconds(120);\nmenu.FadeOutDuration = TimeSpan.FromMilliseconds(160);")),
            new DocSection(
                "📋",
                "TextInput context menu",
                "<info>TextInput</info> includes a built-in <info>TextInputContextMenu</info> with Cut, Copy, Paste, Select All, Undo, and Redo — each dynamically enabled based on selection, clipboard, and undo state.",
                new DocExample(
                    "Text editor with default context menu",
                    "Select text, then right-click to see clipboard actions. Items enable and disable dynamically.",
                    textInput)),
            new DocSection(
                "🔨",
                "Independent context menus",
                "Each control carries its own <info>ContextMenu</info> instance, so a second control can offer entirely different items without the two menus interfering.",
                new DocExample(
                    "A second control with its own menu",
                    "Right-click the button to see clipboard-style items distinct from the first example.",
                    editTarget)),
            new DocSection(
                "🧰",
                "Building a context menu with MenuBuilder",
                "<info>ContextMenu</info> also accepts a menu built with <info>MenuBuilder</info>, composing the fluent builder API with the wrapper's popup, light dismiss, and Opening/Closing events.",
                new DocExample(
                    "Right-click target",
                    "Right-click the button to see the context menu built through MenuBuilder. <reverse>Escape</reverse> or clicking outside dismisses it.",
                    new DocColumn(builderTarget, builderStatus),
                    "var menu = new ContextMenu(\n    MenuBuilder.Vertical()\n        .Item(\"&Inspect\")\n        .Item(\"&Run\", shortcut: \"F5\")\n        .Item(\"&Debug\", shortcut: \"F9\")\n        .Separator()\n        .Item(\"De&ploy\", isEnabled: false)\n        .Build());")));
    }
}
