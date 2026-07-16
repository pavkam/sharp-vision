// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;



/// <summary>Documents the Menu control with practical application-style command, context, and menu bar specimens.</summary>
internal sealed class MenuPane: CompositeControl
{

    internal MenuPane() => InitializeContent(CreateContent());
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Menu";

    /// <inheritdoc/>
    private static Dock CreateContent()
    {
        var status = new Text("Right-click or use arrows, then press Enter to invoke.");

        var contextMenu = new Menu { Orientation = Orientation.Vertical };
        contextMenu.Items.Add(new MenuItem { Content = new Text("Cut") });
        contextMenu.Items.Add(new MenuItem { Content = new Text("Copy") });
        contextMenu.Items.Add(new MenuItem { Content = new Text("Paste") });
        contextMenu.Items.Add(new MenuSeparator());
        contextMenu.Items.Add(new MenuItem { Content = new Text("Select all") });
        contextMenu.Items.Add(new MenuSeparator());
        contextMenu.Items.Add(new MenuItem { Content = new Text("Auto save"), Kind = MenuItemKind.Check, IsChecked = true });
        contextMenu.Items.Add(new MenuSeparator());
        contextMenu.Items.Add(new MenuItem { Content = new Text("Compact"), Kind = MenuItemKind.Radio, GroupName = "view", IsChecked = true });
        contextMenu.Items.Add(new MenuItem { Content = new Text("Comfortable"), Kind = MenuItemKind.Radio, GroupName = "view" });
        contextMenu.Items.Add(new MenuItem { Content = new Text("Spacious"), Kind = MenuItemKind.Radio, GroupName = "view" });
        contextMenu.ItemInvoked += (_, e) => status.Content = $"Invoked: {Label(e.Item)}";

        var contextFrame = new Dock
        {
            Width = Length.Cells(28),
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Children = { contextMenu },
        };

        var barStatus = new Text("Select a top-level entry with arrows or click.");

        var menuBar = new Menu { Orientation = Orientation.Horizontal, Spacing = 2 };
        menuBar.Items.Add(new MenuItem { Content = new Text("File") });
        menuBar.Items.Add(new MenuItem { Content = new Text("Edit") });
        menuBar.Items.Add(new MenuItem { Content = new Text("View") });
        menuBar.Items.Add(new MenuItem { Content = new Text("Help") });
        menuBar.ItemInvoked += (_, e) => barStatus.Content = $"Invoked: {Label(e.Item)}";

        var barFrame = new Dock
        {
            Width = Length.Cells(40),
            Background = ThemeColors.Surface,
            FillMode = FillMode.Opaque,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderGlyphs = Glyphs.Light,
            Padding = new Thickness(1, 0),
            Children = { menuBar },
        };

        var flyoutStatus = new Text("Click the button to open the action menu.");
        var flyoutTrigger = new Button { Content = new Text("Project actions ▼") };
        var flyoutMenu = new Menu { Orientation = Orientation.Vertical };
        flyoutMenu.Items.Add(new MenuItem { Content = new Text("Build") });
        flyoutMenu.Items.Add(new MenuItem { Content = new Text("Test") });
        flyoutMenu.Items.Add(new MenuItem { Content = new Text("Publish") });
        flyoutMenu.ItemInvoked += (_, e) =>
        {
            flyoutStatus.Content = $"Action: {Label(e.Item)}";
        };
        var flyout = new Popup
        {
            Anchor = flyoutTrigger,
            Content = flyoutMenu,
            Placement = PopupPlacement.Below,
        };
        flyoutTrigger.Click += (_, _) => flyout.IsOpen = !flyout.IsOpen;

        var flyoutStage = new Canvas
        {
            Width = Length.Cells(40),
            Height = Length.Cells(8),
            ClipToBounds = false,
        };
        Canvas.SetLeft(flyoutTrigger, Length.Cells(1));
        Canvas.SetTop(flyoutTrigger, Length.Cells(1));
        Canvas.SetLeft(flyoutStatus, Length.Cells(1));
        Canvas.SetTop(flyoutStatus, Length.Cells(6));
        flyoutStage.Children.Add(flyoutTrigger);
        flyoutStage.Children.Add(flyout);
        flyoutStage.Children.Add(flyoutStatus);

        var selectionStatus = new Text("Navigate with arrows, then press Enter.");
        var selectionMenu = new Menu { Orientation = Orientation.Vertical };
        selectionMenu.Items.Add(new MenuItem { Content = new Text("Inspect") });
        selectionMenu.Items.Add(new MenuItem { Content = new Text("Run") });
        selectionMenu.Items.Add(new MenuItem { Content = new Text("Debug") });
        selectionMenu.Items.Add(new MenuItem { Content = new Text("Deploy"), IsEnabled = false });
        selectionMenu.ItemInvoked += (_, e) =>
            selectionStatus.Content = $"Selected: {selectionMenu.SelectedIndex}, invoked: {Label(e.Item)}";

        var selectionFrame = new Dock
        {
            Width = Length.Cells(28),
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Children = { selectionMenu },
        };

        return Doc.Page(
            Title,
            "Arranges command, check, radio, and separator items in a vertical or horizontal menu with keyboard navigation and selection tracking.",
            Doc.Section(
                "📑",
                "Command menu",
                "A vertical menu combines command items, separators, check toggles, and radio groups in a single navigable list.",
                Doc.Example(
                    "Context menu with commands, toggles, and radio groups",
                    "Use arrow keys to navigate. Enter or Space activates the selected item. Check items toggle, radio items select within their group. Separators are skipped.",
                    Doc.Column(contextFrame, status),
                    "var menu = new Menu();\nmenu.Items.Add(new MenuItem { Content = new Text(\"Cut\") });\nmenu.Items.Add(new MenuSeparator());\nmenu.Items.Add(new MenuItem { Kind = MenuItemKind.Check });")),
            Doc.Section(
                "📑",
                "Menu bar",
                "Horizontal orientation switches arrow traversal to Left/Right while keeping the same item semantics and invocation contract.",
                Doc.Example(
                    "Application menu bar",
                    "Navigate across File, Edit, View, and Help with Left/Right arrows. Enter invokes the selected entry.",
                    Doc.Column(barFrame, barStatus))),
            Doc.Section(
                "📑",
                "Popup composition",
                "Place a Menu inside a Popup anchored to a trigger button for a flyout action list that opens and closes on demand.",
                Doc.Example(
                    "Button-triggered action menu",
                    "Click the button to open the popup menu. Select an action with arrows and Enter. The popup closes after invocation.",
                    flyoutStage,
                    "var popup = new Popup\n{\n    Anchor = trigger,\n    Content = menu,\n    Placement = PopupPlacement.Below,\n};\ntrigger.Click += (_, _) => popup.IsOpen = !popup.IsOpen;")),
            Doc.Section(
                "📑",
                "Selection and invocation",
                "SelectedIndex tracks the keyboard position independently of activation. Disabled items are visible but skipped by navigation and invocation.",
                Doc.Example(
                    "Navigate, then invoke",
                    "Arrow keys change the selected index. Enter activates and reports both the index and the invoked item. The Deploy entry is disabled and cannot be selected.",
                    Doc.Column(selectionFrame, selectionStatus))));
    }

    private static string Label(MenuItem item) => ((Text) item.Content!).Content;
}
