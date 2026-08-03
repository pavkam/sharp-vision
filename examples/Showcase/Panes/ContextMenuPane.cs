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

        var customMenu = new ContextMenu();
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

        var textInput = new TextInput
        {
            Text = "Right-click for edit menu",
            Width = Length.Cells(30)
        };

        var directMenu = new ContextMenu();
        directMenu.Items.Add(new MenuItem { Text = "Cu&t", ShortcutText = "Ctrl+X" });
        directMenu.Items.Add(new MenuItem { Text = "&Copy", ShortcutText = "Ctrl+C" });
        directMenu.Items.Add(new MenuItem { Text = "&Paste", ShortcutText = "Ctrl+V" });
        directMenu.Items.Add(new MenuSeparator());
        directMenu.Items.Add(new MenuItem { Text = "Select &All", ShortcutText = "Ctrl+A" });
        var builderTarget = new Button
        {
            Text = "&Builder menu",
            Width = Length.Cells(20),
            Height = Length.Cells(3),
            ContextMenu = directMenu
        };

        return new DocPage(
            Title,
            "<info>ContextMenu</info> displays a vertical menu at the pointer position on secondary (right) click. Any control supports it through the <info>ContextMenu</info> property. <info>TextInput</info> attaches one by default with clipboard and editing commands.",
            new DocSection(
                "🖱️",
                "Custom context menu",
                "Right-click the button to open a context menu with action items. The <warning>Deploy</warning> entry is disabled and cannot be invoked.",
                new DocExample(
                    "Right-click target",
                    "Right-click the button to see the context menu. <reverse>Escape</reverse> or clicking outside dismisses it.",
                    new DocColumn(target, status))),
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
                "Another context menu",
                "Attach a second context menu to another control with different items.",
                new DocExample(
                    "Directly constructed context menu",
                    "Right-click the button to see clipboard-style items.",
                    builderTarget)));
    }
}
