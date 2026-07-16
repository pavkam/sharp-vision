// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TextEditor;

using System.Globalization;

/// <summary>Main editor screen with dropdown menu bar, find/replace, context menu, and status bar.</summary>
public sealed class EditorScreen: Screen
{
    private readonly TextInput _editor;
    private readonly Text _position;
    private readonly FindReplaceDialog _findReplace;
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

        _contextPopup = new Popup
        {
            Anchor = _editor,
            Content = BuildContextMenu(),
            Placement = PopupPlacement.Below,
        };

        var appMenu = MenuBuilder.Horizontal(spacing: 2)
            .Submenu("File", file => file
                .Item("New", shortcut: "Ctrl+N", onInvoke: NewFile)
                .Separator()
                .Item("Quit", shortcut: "Ctrl+Q", onInvoke: () => Application?.Closed()))
            .Submenu("Edit", edit => edit
                .Item("Undo", shortcut: "Ctrl+Z", onInvoke: () => _editor.Undo())
                .Item("Redo", shortcut: "Ctrl+Y", onInvoke: () => _editor.Redo())
                .Separator()
                .Item("Cut", shortcut: "Ctrl+X", onInvoke: () => _editor.CutSelection())
                .Item("Copy", shortcut: "Ctrl+C", onInvoke: () => _editor.CopySelection())
                .Item("Paste", shortcut: "Ctrl+V")
                .Separator()
                .Item("Select All", shortcut: "Ctrl+A", onInvoke: () => _editor.Select(0, _editor.Text.Length)))
            .Submenu("Search", search => search
                .Item("Find", shortcut: "Ctrl+F", onInvoke: () => _findReplace.OpenFind())
                .Item("Replace", shortcut: "Ctrl+H", onInvoke: () => _findReplace.OpenReplace()))
            .Build();
        appMenu.IsTabStop = false;

        var menuBar = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = ThemeColors.Surface,
            FillMode = FillMode.Opaque,
            Height = Length.Cells(1),
            Padding = new Thickness(1, 0),
            Children = { appMenu },
        };

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

        var layout = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        Dock.SetSide(menuBar, Side.Top);
        Dock.SetSide(statusBar, Side.Bottom);
        layout.Children.Add(menuBar);
        layout.Children.Add(statusBar);
        layout.Children.Add(_editor);
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
            "Try the dropdown menus: click File, Edit, or Search.\n" +
            "Right-click anywhere for a context menu.\n\n" +
            "Keyboard shortcuts:\n" +
            "  Ctrl+N   New file          Ctrl+F   Find\n" +
            "  Ctrl+Z   Undo              Ctrl+H   Find and replace\n" +
            "  Ctrl+Y   Redo              Ctrl+Q   Quit\n";
    }

    /// <inheritdoc/>
    protected override void OnDispose() => _editor.SelectionChanged -= OnSelectionChanged;

    #region Context menu

    private Menu BuildContextMenu() => MenuBuilder.Vertical()
        .Item("Cut", onInvoke: () => _editor.CutSelection())
        .Item("Copy", onInvoke: () => _editor.CopySelection())
        .Item("Paste")
        .Separator()
        .Item("Select All", onInvoke: () => _editor.Select(0, _editor.Text.Length))
        .Separator()
        .Item("Find", onInvoke: () => _findReplace.OpenFind())
        .Item("Replace", onInvoke: () => _findReplace.OpenReplace())
        .Build();

    private void OnGlobalPointer(object? sender, PointerEventArgs e)
    {
        _ = sender;

        if (!e.Handled && e.Pointer.Action == PointerAction.Press && e.Pointer.Buttons == Buttons.Secondary)
        {
            _contextPopup.IsOpen = true;
            e.Handled = true;
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

        if ((e.Stroke.Modifiers & Modifiers.Control) == 0 ||
            e.Stroke.Code != Code.Character ||
            e.Stroke.Character is not { } ch)
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
