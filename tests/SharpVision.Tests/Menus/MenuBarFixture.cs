// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Builds the application-shaped menu tree the session tests share: a horizontal bar
/// docked above an editor, with a File drop-down that nests a two-level Open Recent branch, an
/// Edit drop-down, and a top-level command item without a submenu.</summary>
internal sealed class MenuBarFixture
{
    private MenuBarFixture(
        Dock root,
        TextInput editor,
        Menu bar,
        MenuItem file,
        Menu fileMenu,
        MenuItem @new,
        MenuItem recent,
        Menu recentMenu,
        MenuItem today,
        MenuItem yesterday,
        MenuItem archive,
        Menu archiveMenu,
        MenuItem save,
        MenuItem edit,
        Menu editMenu,
        MenuItem undo,
        MenuItem direct)
    {
        Root = root;
        Editor = editor;
        Bar = bar;
        File = file;
        FileMenu = fileMenu;
        New = @new;
        Recent = recent;
        RecentMenu = recentMenu;
        Today = today;
        Yesterday = yesterday;
        Archive = archive;
        ArchiveMenu = archiveMenu;
        Save = save;
        Edit = edit;
        EditMenu = editMenu;
        Undo = undo;
        Direct = direct;
    }

    /// <summary>Gets the dock that hosts the bar on top and the editor below it.</summary>
    internal Dock Root { get; }

    /// <summary>Gets the editor that owns focus before any menu interaction.</summary>
    internal TextInput Editor { get; }

    /// <summary>Gets the horizontal menu bar.</summary>
    internal Menu Bar { get; }

    /// <summary>Gets the File heading.</summary>
    internal MenuItem File { get; }

    /// <summary>Gets the File drop-down: New, Open Recent (submenu), separator, Save.</summary>
    internal Menu FileMenu { get; }

    /// <summary>Gets the first File row.</summary>
    internal MenuItem New { get; }

    /// <summary>Gets the File row that owns the Open Recent submenu.</summary>
    internal MenuItem Recent { get; }

    /// <summary>Gets the Open Recent submenu: Today, Yesterday, Archive (submenu).</summary>
    internal Menu RecentMenu { get; }

    /// <summary>Gets the first Open Recent row.</summary>
    internal MenuItem Today { get; }

    /// <summary>Gets the second Open Recent row.</summary>
    internal MenuItem Yesterday { get; }

    /// <summary>Gets the Open Recent row that owns the Archive submenu.</summary>
    internal MenuItem Archive { get; }

    /// <summary>Gets the Archive submenu.</summary>
    internal Menu ArchiveMenu { get; }

    /// <summary>Gets the last File row, after the separator.</summary>
    internal MenuItem Save { get; }

    /// <summary>Gets the Edit heading.</summary>
    internal MenuItem Edit { get; }

    /// <summary>Gets the Edit drop-down.</summary>
    internal Menu EditMenu { get; }

    /// <summary>Gets the first Edit row.</summary>
    internal MenuItem Undo { get; }

    /// <summary>Gets the top-level command item that has no submenu.</summary>
    internal MenuItem Direct { get; }

    /// <summary>Gets the File drop-down's retained popup.</summary>
    internal Popup FilePopup => OwnedTree.Find<Popup>(File).ShouldNotBeNull();

    /// <summary>Gets the Open Recent submenu's retained popup.</summary>
    internal Popup RecentPopup => OwnedTree.Find<Popup>(Recent).ShouldNotBeNull();

    /// <summary>Gets the Archive submenu's retained popup.</summary>
    internal Popup ArchivePopup => OwnedTree.Find<Popup>(Archive).ShouldNotBeNull();

    /// <summary>Gets the Edit drop-down's retained popup.</summary>
    internal Popup EditPopup => OwnedTree.Find<Popup>(Edit).ShouldNotBeNull();

    /// <summary>Creates a fresh, unmounted fixture.</summary>
    /// <returns>The fixture whose <see cref="Root"/> is ready to mount.</returns>
    internal static MenuBarFixture Create()
    {
        var archiveMenu = new Menu { Orientation = Orientation.Vertical };
        archiveMenu.Items.Add(new MenuItem { Text = "2024" });
        archiveMenu.Items.Add(new MenuItem { Text = "2023" });

        var today = new MenuItem { Text = "Today" };
        var yesterday = new MenuItem { Text = "Yesterday" };
        var archive = new MenuItem { Text = "Archive", Submenu = archiveMenu };
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(today);
        recentMenu.Items.Add(yesterday);
        recentMenu.Items.Add(archive);

        var @new = new MenuItem { Text = "New" };
        var recent = new MenuItem { Text = "Open Recent", Submenu = recentMenu };
        var save = new MenuItem { Text = "Save" };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(@new);
        fileMenu.Items.Add(recent);
        fileMenu.Items.Add(new MenuSeparator());
        fileMenu.Items.Add(save);

        var undo = new MenuItem { Text = "Undo" };
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(undo);
        editMenu.Items.Add(new MenuItem { Text = "Redo" });

        var file = new MenuItem { Text = "&File", Submenu = fileMenu };
        var edit = new MenuItem { Text = "&Edit", Submenu = editMenu };
        var direct = new MenuItem { Text = "&Direct" };
        var bar = new Menu { Orientation = Orientation.Horizontal, Spacing = 2, IsTabStop = false };
        bar.Items.Add(file);
        bar.Items.Add(edit);
        bar.Items.Add(direct);

        var editor = new TextInput
        {
            Text = "alpha",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var root = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Dock.SetSide(bar, DockSide.Top);
        Dock.SetSide(editor, DockSide.Bottom);
        root.Children.Add(bar);
        root.Children.Add(editor);

        return new MenuBarFixture(
            root,
            editor,
            bar,
            file,
            fileMenu,
            @new,
            recent,
            recentMenu,
            today,
            yesterday,
            archive,
            archiveMenu,
            save,
            edit,
            editMenu,
            undo,
            direct);
    }

    /// <summary>Resolves the selected-row background the mounted theme paints, projected to the
    /// surface's color depth.</summary>
    /// <param name="surface">The mounted surface.</param>
    /// <returns>The concrete selection background color.</returns>
    internal Color SelectionBackground(ComponentSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        return ThemeColorHelper.SelectionBackground(Bar.Theme.ShouldNotBeNull());
    }
}
