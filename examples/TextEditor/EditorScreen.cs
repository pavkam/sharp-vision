// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TextEditor;

using System.Globalization;

/// <summary>Main editor screen with menu bar, multiline text editor, context menu, find/replace, and status bar.</summary>
public sealed class EditorScreen: Screen
{
    private readonly TextInput _editor;
    private readonly Text _position;
    private readonly FindReplaceDialog _findReplace;
    private readonly Menu _contextMenu;
    private readonly Popup _contextPopup;

    /// <summary>Initializes the editor layout.</summary>
    public EditorScreen()
    {
        _editor = new TextInput
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _editor.SelectionChanged += OnSelectionChanged;

        _findReplace = new FindReplaceDialog(_editor);

        _contextMenu = BuildContextMenu();
        _contextPopup = new Popup
        {
            Anchor = _editor,
            Content = _contextMenu,
            Placement = PopupPlacement.Below,
        };

        var menuBarDock = BuildMenuBar();

        _position = new Text("Ln 1, Col 1") { Overflow = Overflow.Clip };
        var encoding = new Text("<d>UTF-8</d>") { Overflow = Overflow.Clip };

        var statusBar = new Dock
        {
            Background = ThemeColors.Surface,
            FillMode = FillMode.Opaque,
            Height = Length.Cells(1),
            Padding = new Thickness(1, 0),
        };
        Dock.SetSide(encoding, Side.Right);
        statusBar.Children.Add(encoding);
        statusBar.Children.Add(_position);

        var editorArea = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        editorArea.Children.Add(_editor);
        Overlay.SetZIndex(_findReplace.Window, 10);
        editorArea.Children.Add(_findReplace.Window);

        var layout = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        Dock.SetSide(menuBarDock, Side.Top);
        Dock.SetSide(statusBar, Side.Bottom);
        layout.Children.Add(menuBarDock);
        layout.Children.Add(statusBar);
        layout.Children.Add(editorArea);

        InitializeContent(layout);
        _ = AddHandler(Events.Key, OnGlobalKey);
        _ = AddHandler(Events.Pointer, OnGlobalPointer);
    }

    /// <inheritdoc/>
    protected override void OnAttach(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Theme = Themes.Dark;
    }

    /// <inheritdoc/>
    protected override void OnStarted(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _ = application.Focus.Focus(_editor);
        _editor.Text =
            "Welcome to the SharpVision Text Editor.\n\n" +
            "This editor demonstrates the framework's TextInput control with\n" +
            "full multiline editing, undo/redo, selection, and clipboard.\n\n" +
            "Keyboard shortcuts:\n" +
            "  Ctrl+N   New file          Ctrl+F   Find\n" +
            "  Ctrl+Z   Undo              Ctrl+H   Find and replace\n" +
            "  Ctrl+Y   Redo              Ctrl+Q   Quit\n" +
            "  Ctrl+X   Cut               Right-click for context menu\n" +
            "  Ctrl+C   Copy\n" +
            "  Ctrl+V   Paste\n" +
            "  Ctrl+A   Select all\n";
    }

    /// <inheritdoc/>
    protected override void OnDispose()
    {
        _editor.SelectionChanged -= OnSelectionChanged;
        _contextMenu.ItemInvoked -= OnContextMenuInvoked;
    }

    #region Menu bar

    private Dock BuildMenuBar()
    {
        var bar = new Stack { Orientation = Orientation.Horizontal, Spacing = 1 };
        bar.Children.Add(DropdownEntry("File", BuildFileMenu()));
        bar.Children.Add(DropdownEntry("Edit", BuildEditMenu()));
        bar.Children.Add(DropdownEntry("Search", BuildSearchMenu()));

        return new Dock
        {
            Background = ThemeColors.Surface,
            FillMode = FillMode.Opaque,
            Height = Length.Cells(1),
            Padding = new Thickness(1, 0),
            Children = { bar },
        };
    }

    private static Overlay DropdownEntry(string label, Menu submenu)
    {
        var trigger = new Button
        {
            Content = new Text($"<b>{Text.Escape(label)}</b>"),
            HasShadow = false,
            BorderThickness = default,
        };

        var popup = new Popup
        {
            Anchor = trigger,
            Content = submenu,
            Placement = PopupPlacement.Below,
        };

        trigger.Click += (_, _) => popup.IsOpen = !popup.IsOpen;
        submenu.ItemInvoked += (_, _) => popup.IsOpen = false;

        var wrapper = new Overlay { Width = Length.Auto, Height = Length.Cells(1) };
        wrapper.Children.Add(trigger);
        wrapper.Children.Add(popup);
        return wrapper;
    }

    private Menu BuildFileMenu()
    {
        var menu = new Menu { Orientation = Orientation.Vertical };
        var fileNew = new MenuItem { Content = new Text("New"), ShortcutText = "Ctrl+N" };
        fileNew.Invoked += (_, _) => NewFile();
        var fileQuit = new MenuItem { Content = new Text("Quit"), ShortcutText = "Ctrl+Q" };
        fileQuit.Invoked += (_, _) => Application?.Closed();
        menu.Items.Add(fileNew);
        menu.Items.Add(new MenuSeparator());
        menu.Items.Add(fileQuit);
        return menu;
    }

    private Menu BuildEditMenu()
    {
        var menu = new Menu { Orientation = Orientation.Vertical };
        var undo = new MenuItem { Content = new Text("Undo"), ShortcutText = "Ctrl+Z" };
        undo.Invoked += (_, _) => _editor.Undo();
        var redo = new MenuItem { Content = new Text("Redo"), ShortcutText = "Ctrl+Y" };
        redo.Invoked += (_, _) => _editor.Redo();
        var cut = new MenuItem { Content = new Text("Cut"), ShortcutText = "Ctrl+X" };
        cut.Invoked += (_, _) => _editor.CutSelection();
        var copy = new MenuItem { Content = new Text("Copy"), ShortcutText = "Ctrl+C" };
        copy.Invoked += (_, _) => _editor.CopySelection();
        var paste = new MenuItem { Content = new Text("Paste"), ShortcutText = "Ctrl+V" };
        var selectAll = new MenuItem { Content = new Text("Select All"), ShortcutText = "Ctrl+A" };
        selectAll.Invoked += (_, _) => _editor.Select(0, _editor.Text.Length);
        menu.Items.Add(undo);
        menu.Items.Add(redo);
        menu.Items.Add(new MenuSeparator());
        menu.Items.Add(cut);
        menu.Items.Add(copy);
        menu.Items.Add(paste);
        menu.Items.Add(new MenuSeparator());
        menu.Items.Add(selectAll);
        return menu;
    }

    private Menu BuildSearchMenu()
    {
        var menu = new Menu { Orientation = Orientation.Vertical };
        var find = new MenuItem { Content = new Text("Find"), ShortcutText = "Ctrl+F" };
        find.Invoked += (_, _) => _findReplace.OpenFind();
        var replace = new MenuItem { Content = new Text("Replace"), ShortcutText = "Ctrl+H" };
        replace.Invoked += (_, _) => _findReplace.OpenReplace();
        menu.Items.Add(find);
        menu.Items.Add(replace);
        return menu;
    }

    #endregion

    #region Context menu

    private Menu BuildContextMenu()
    {
        var menu = new Menu { Orientation = Orientation.Vertical };
        menu.Items.Add(new MenuItem { Content = new Text("Cut") });
        menu.Items.Add(new MenuItem { Content = new Text("Copy") });
        menu.Items.Add(new MenuItem { Content = new Text("Paste") });
        menu.Items.Add(new MenuSeparator());
        menu.Items.Add(new MenuItem { Content = new Text("Select All") });
        menu.Items.Add(new MenuSeparator());
        menu.Items.Add(new MenuItem { Content = new Text("Find") });
        menu.Items.Add(new MenuItem { Content = new Text("Replace") });
        menu.ItemInvoked += OnContextMenuInvoked;
        return menu;
    }

    private void OnGlobalPointer(object? sender, PointerEventArgs e)
    {
        _ = sender;

        if (e.Handled || e.Pointer.Action != PointerAction.Press || e.Pointer.Buttons != Buttons.Secondary)
        {
            return;
        }

        _contextPopup.IsOpen = true;
        e.Handled = true;
    }

    private void OnContextMenuInvoked(object? sender, MenuItemInvokedEventArgs e)
    {
        _ = sender;
        _contextPopup.IsOpen = false;
        var label = ((Text) e.Item.Content!).Content;

        switch (label)
        {
            case "Cut": _ = _editor.CutSelection(); break;
            case "Copy": _ = _editor.CopySelection(); break;
            case "Select All": _editor.Select(0, _editor.Text.Length); break;
            case "Find": _findReplace.OpenFind(); break;
            case "Replace": _findReplace.OpenReplace(); break;
            case "Paste":
            default:
                break;
        }
    }

    #endregion

    #region Keyboard shortcuts

    private void OnGlobalKey(object? sender, KeyEventArgs e)
    {
        _ = sender;

        if (e.Handled || e.Phase != Phase.Preview || e.Stroke.Action != KeyAction.Press)
        {
            return;
        }

        if (e.Stroke.Code == Code.Escape && _findReplace.IsOpen)
        {
            _findReplace.Close();
            _ = Application?.Focus.Focus(_editor);
            e.Handled = true;
            return;
        }

        if ((e.Stroke.Modifiers & Modifiers.Control) == 0)
        {
            return;
        }

        if (e.Stroke.Code != Code.Character || e.Stroke.Character is not { } ch)
        {
            return;
        }

        var lower = Rune.ToLowerInvariant(ch);

        if (lower == new Rune('q'))
        {
            Application?.Closed();
            e.Handled = true;
        }
        else if (lower == new Rune('n'))
        {
            NewFile();
            e.Handled = true;
        }
        else if (lower == new Rune('f'))
        {
            _findReplace.OpenFind();
            e.Handled = true;
        }
        else if (lower == new Rune('h'))
        {
            _findReplace.OpenReplace();
            e.Handled = true;
        }
    }

    #endregion

    #region Commands and status

    private void NewFile()
    {
        _editor.Text = string.Empty;
        _ = Application?.Focus.Focus(_editor);
    }

    private void OnSelectionChanged(object? sender, InputSelectionChangedEventArgs e)
    {
        _ = sender;
        var (line, column) = ComputePosition(_editor.Text, e.Selection.Caret);
        var sel = _editor.SelectionLength;

        _position.Content = sel > 0
            ? $"Ln {line.ToString(CultureInfo.InvariantCulture)}, Col {column.ToString(CultureInfo.InvariantCulture)} (Sel {sel.ToString(CultureInfo.InvariantCulture)})"
            : $"Ln {line.ToString(CultureInfo.InvariantCulture)}, Col {column.ToString(CultureInfo.InvariantCulture)}";
    }

    private static (int Line, int Column) ComputePosition(string text, int caret)
    {
        var line = 1;
        var lastLineStart = 0;

        for (var i = 0; i < caret && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lastLineStart = i + 1;
            }
        }

        return (line, caret - lastLineStart + 1);
    }

    #endregion
}
