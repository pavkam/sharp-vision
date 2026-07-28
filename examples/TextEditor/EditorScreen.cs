// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TextEditor;

using System.Globalization;

/// <summary>Main editor screen with file I/O, menus, find/replace, context menu, and status bar.</summary>
public sealed class EditorScreen: Screen
{
    private static readonly FilePickerFilter _textFilters =
        new("Text files", "*.txt", "*.md", "*.cs", "*.json", "*.xml", "*.yml", "*.yaml", "*.log");

    private readonly TextInput _editor;
    private readonly Text _position;
    private readonly Text _statusMessage;
    private readonly Text _fileNameLabel;
    private readonly FindReplaceDialog _findReplace;
    private readonly Menu _contextMenu;
    private readonly Popup _contextPopup;
    private string? _filePath;
    private string? _initialPath;
    private bool _dirty;
    private string _savedText = string.Empty;

    /// <summary>Initializes the editor layout.</summary>
    /// <param name="filePath">Optional file path to open on startup.</param>
    public EditorScreen(string? filePath = null)
    {
        _initialPath = filePath;
        _editor = new TextInput
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            WordWrap = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ScrollBars = ScrollBars.Vertical
        };
        _editor.SelectionChanged += OnSelectionChanged;
        _editor.TextChanged += OnTextChanged;

        _findReplace = new FindReplaceDialog(_editor);

        _contextMenu = BuildContextMenu();
        _contextMenu.ItemInvoked += OnContextMenuInvoked;
        _contextPopup = new Popup
        {
            Anchor = _editor,
            Content = _contextMenu,
            Placement = PopupPlacement.Below
        };

        var appMenu = MenuBuilder.Horizontal(spacing: 2)
            .Submenu("&File", file => file
                .Item("&New", shortcut: "Ctrl+N", onInvoke: () => QueueEditorCommand(NewFileAsync))
                .Item("&Open...", shortcut: "Ctrl+O", onInvoke: () => QueueEditorCommand(OpenFileAsync))
                .Separator()
                .Item("&Save", shortcut: "Ctrl+S", onInvoke: () => QueueEditorCommand(SaveFileAsync))
                .Item("Save &As...", shortcut: "Ctrl+Shift+S",
                    onInvoke: () => QueueEditorCommand(SaveFileAsAsync))
                .Separator()
                .Item("&Quit", shortcut: "Ctrl+Q",
                    onInvoke: () => QueueEditorCommand(QuitAsync)))
            .Submenu("&Edit", edit => edit
                .Item("&Undo", shortcut: "Ctrl+Z", onInvoke: () => QueueEditorCommand(() => _editor.Undo()))
                .Item("&Redo", shortcut: "Ctrl+Y", onInvoke: () => QueueEditorCommand(() => _editor.Redo()))
                .Separator()
                .Item("Cu&t", shortcut: "Ctrl+X", onInvoke: () => QueueClipboardShortcut(new Rune('x')))
                .Item("&Copy", shortcut: "Ctrl+C", onInvoke: () => QueueClipboardShortcut(new Rune('c')))
                .Item("&Paste", shortcut: "Ctrl+V", onInvoke: () => QueueClipboardShortcut(new Rune('v')))
                .Separator()
                .Item(
                    "Select &All",
                    shortcut: "Ctrl+A",
                    onInvoke: () => QueueEditorCommand(() => _editor.Select(0, _editor.Text.Length))))
            .Submenu("&View", view => view
                .Item("&Word Wrap", shortcut: "Alt+Z", onInvoke: ToggleWordWrap))
            .Submenu("&Search", search => search
                .Item("&Find", shortcut: "Ctrl+F", onInvoke: () => QueueEditorCommand(_findReplace.OpenFind))
                .Item("&Replace", shortcut: "Ctrl+H", onInvoke: () => QueueEditorCommand(_findReplace.OpenReplace)))
            .Build();
        appMenu.TabStop = false;

        var menuBar = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Face = new Face(
                ThemeColor.ControlText,
                Color.Rgb(0x30, 0x30, 0x3A),
                ThemeDecoration.NormalText,
                Underline.None,
                Color.Default),
            Height = Length.Cells(1),
            Padding = new Thickness(1, 0),
            Children = { appMenu }
        };

        _position = new Text("Ln 1, Col 1") { Overflow = Overflow.Clip };
        _statusMessage = new Text("Ready") { Overflow = Overflow.Clip };
        _fileNameLabel = new Text("<d>Untitled</d>") { Overflow = Overflow.Clip };
        var encoding = new Text("<d>UTF-8</d>") { Overflow = Overflow.Clip };
        var wrapLabel = new Text("<d>Wrap</d>") { Overflow = Overflow.Clip };
        var statusBar = new StatusBar { Padding = new Thickness(1, 0) };
        statusBar.Items.Add(new StatusBarItem { Content = _statusMessage });
        statusBar.Items.Add(new StatusBarItem
        {
            Alignment = StatusBarItemAlignment.Right,
            Content = _position
        });
        statusBar.Items.Add(new StatusBarItem
        {
            Alignment = StatusBarItemAlignment.Right,
            Content = wrapLabel
        });
        statusBar.Items.Add(new StatusBarItem
        {
            Alignment = StatusBarItemAlignment.Right,
            Content = encoding
        });
        statusBar.Items.Add(new StatusBarItem
        {
            Alignment = StatusBarItemAlignment.Right,
            Content = _fileNameLabel
        });

        var layout = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        Dock.SetSide(menuBar, Side.Top);
        Dock.SetSide(statusBar, Side.Bottom);
        layout.Children.Add(menuBar);
        layout.Children.Add(statusBar);
        layout.Children.Add(_editor);

        var root = new ControlOverlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { layout, _contextPopup, _findReplace.Window }
        };
        ControlOverlay.SetLeft(layout, Length.Cells(0));
        ControlOverlay.SetTop(layout, Length.Cells(0));
        ControlOverlay.SetRight(layout, Length.Cells(0));
        ControlOverlay.SetBottom(layout, Length.Cells(0));
        InitializeContent(root);

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

        if (_initialPath is { } path)
        {
            _initialPath = null;
            LoadInitialFileAsync(path);
        }
        else
        {
            UpdateTitle();
        }
    }

    private async void LoadInitialFileAsync(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            _editor.Text = await File.ReadAllTextAsync(fullPath);
            _filePath = fullPath;
            MarkClean();
            _editor.Select(0, 0);
            SetStatus($"Opened {Path.GetFileName(fullPath)}");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    protected override void OnDispose()
    {
        _editor.SelectionChanged -= OnSelectionChanged;
        _editor.TextChanged -= OnTextChanged;
        _contextMenu.ItemInvoked -= OnContextMenuInvoked;
    }

    #region File operations

    private void UpdateTitle()
    {
        var name = _filePath is not null ? Path.GetFileName(_filePath) : "Untitled";
        var marker = _dirty ? " *" : "";
        Application?.Terminal.SetTitle($"{name}{marker} - SharpVision Editor");
        _fileNameLabel.Content = $"<d>{Text.Escape(name)}</d>";
    }

    private void ToggleWordWrap()
    {
        _editor.WordWrap = !_editor.WordWrap;
        _editor.ScrollBars = _editor.WordWrap ? ScrollBars.Vertical : ScrollBars.Both;
        SetStatus(_editor.WordWrap ? "Word wrap on" : "Word wrap off");
    }

    private void SetStatus(string message) => _statusMessage.Content = message;

    private void MarkClean()
    {
        _dirty = false;
        _savedText = _editor.Text;
        UpdateTitle();
    }

    private void MarkDirty()
    {
        if (_dirty)
        {
            return;
        }

        _dirty = true;
        UpdateTitle();
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (!string.Equals(_editor.Text, _savedText, StringComparison.Ordinal))
        {
            MarkDirty();
        }
    }

    private async void NewFileAsync()
    {
        if (!await ConfirmDiscardAsync())
        {
            return;
        }

        _filePath = null;
        _editor.Text = string.Empty;
        MarkClean();
        _ = Application?.Focus.Focus(_editor);
        SetStatus("New file");
    }

    private async void OpenFileAsync()
    {
        if (!await ConfirmDiscardAsync())
        {
            return;
        }

        var options = new FilePickerOptions
        {
            Title = "Open File",
            Filters = [_textFilters, FilePickerFilter.AllFiles]
        };
        var result = await FilePickerDialog.ShowAsync(_editor, options);

        if (!result.Accepted || result.SelectedPath is not { } path)
        {
            _ = Application?.Focus.Focus(_editor);
            return;
        }

        try
        {
            _editor.Text = await File.ReadAllTextAsync(path);
            _filePath = path;
            MarkClean();
            _editor.Select(0, 0);
            SetStatus($"Opened {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            _ = await MessageBox.ShowAsync(_editor, $"Could not open file:\n{ex.Message}", "Error");
        }

        _ = Application?.Focus.Focus(_editor);
    }

    private async void SaveFileAsync()
    {
        if (_filePath is null)
        {
            SaveFileAsAsync();
            return;
        }

        await WriteFileAsync(_filePath);
    }

    private async void SaveFileAsAsync()
    {
        var options = new SaveFileOptions
        {
            Title = "Save As",
            InitialFileName = _filePath is not null ? Path.GetFileName(_filePath) : "",
            Filters = [_textFilters, FilePickerFilter.AllFiles],
            ConfirmOverwrite = true
        };
        var result = await SaveFileDialog.ShowAsync(_editor, options);

        if (!result.Confirmed || result.Path is not { } path)
        {
            _ = Application?.Focus.Focus(_editor);
            return;
        }

        await WriteFileAsync(path);
        _ = Application?.Focus.Focus(_editor);
    }

    private async Task WriteFileAsync(string path)
    {
        try
        {
            await File.WriteAllTextAsync(path, _editor.Text);
            _filePath = path;
            MarkClean();
            SetStatus($"Saved {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            _ = await MessageBox.ShowAsync(_editor, $"Could not save file:\n{ex.Message}", "Error");
        }
    }

    private async void QuitAsync()
    {
        if (await ConfirmDiscardAsync())
        {
            Application?.Closed();
        }
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        if (!_dirty)
        {
            return true;
        }

        var name = _filePath is not null ? Path.GetFileName(_filePath) : "Untitled";
        var result = await MessageBox.ShowAsync(
            _editor,
            $"Save changes to {name}?",
            "Unsaved Changes",
            MessageBoxButtons.YesNoCancel);

        switch (result)
        {
            case MessageBoxResult.Cancel:
                return false;
            case MessageBoxResult.Yes when _filePath is null:
                {
                    var options = new SaveFileOptions
                    {
                        Title = "Save As",
                        Filters = [_textFilters, FilePickerFilter.AllFiles],
                        ConfirmOverwrite = true
                    };
                    var saveResult = await SaveFileDialog.ShowAsync(_editor, options);

                    if (!saveResult.Confirmed || saveResult.Path is null)
                    {
                        return false;
                    }

                    await WriteFileAsync(saveResult.Path);
                    break;
                }
            case MessageBoxResult.Yes:
                await WriteFileAsync(_filePath);
                break;
            case MessageBoxResult.Ok:
            case MessageBoxResult.No:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return true;
    }

    #endregion

    #region Context menu

    private Menu BuildContextMenu() => MenuBuilder.Vertical()
        .Item("Cu&t", onInvoke: () => QueueClipboardShortcut(new Rune('x')))
        .Item("&Copy", onInvoke: () => QueueClipboardShortcut(new Rune('c')))
        .Item("&Paste", onInvoke: () => QueueClipboardShortcut(new Rune('v')))
        .Separator()
        .Item("Select &All", onInvoke: () => QueueEditorCommand(() => _editor.Select(0, _editor.Text.Length)))
        .Separator()
        .Item("&Find", onInvoke: () => QueueEditorCommand(_findReplace.OpenFind))
        .Item("&Replace", onInvoke: () => QueueEditorCommand(_findReplace.OpenReplace))
        .Build();

    private void OnContextMenuInvoked(object? sender, MenuItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        _contextPopup.IsOpen = false;
    }

    private void OnGlobalPointer(object? sender, PointerEventArgs e)
    {
        _ = sender;

        if (e is { Handled: false, Pointer: { Action: PointerAction.Press, Buttons: Buttons.Secondary } })
        {
            _ = _contextPopup.OpenModal(initialFocus: _contextMenu);
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

        if ((e.Stroke.Modifiers & Modifiers.Alt) != 0 &&
            e.Stroke.Code == Code.Character &&
            e.Stroke.Character is { } altCh &&
            Rune.ToLowerInvariant(altCh) == new Rune('z'))
        {
            ToggleWordWrap();
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
        var shift = (e.Stroke.Modifiers & Modifiers.Shift) != 0;

        if (lower == new Rune('q'))
        {
            QuitAsync();
            e.Handled = true;
        }
        else if (lower == new Rune('n'))
        {
            NewFileAsync();
            e.Handled = true;
        }
        else if (lower == new Rune('o'))
        {
            OpenFileAsync();
            e.Handled = true;
        }
        else if (lower == new Rune('s') && shift)
        {
            SaveFileAsAsync();
            e.Handled = true;
        }
        else if (lower == new Rune('s'))
        {
            SaveFileAsync();
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

    #region Editor commands and status

    private void QueueEditorCommand(System.Action command)
    {
        Debug.Assert(command is not null, "A TextEditor menu command must be provided.");
        var application = Application;

        if (application is null)
        {
            return;
        }

        if (application.Modality.Active is not { } scope)
        {
            Run();
            return;
        }

        scope.Exited += OnExited;
        return;

        void OnExited(object? sender, EventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            scope.Exited -= OnExited;
            Run();
        }

        void Run()
        {
            if (IsDisposed || _editor.IsDisposed)
            {
                return;
            }

            _ = application.Focus.Focus(_editor);
            command();
        }
    }

    private void QueueClipboardShortcut(Rune command)
    {
        QueueEditorCommand(() =>
        {
            var application = Application;

            if (application is null)
            {
                return;
            }

            var stroke = new Stroke(
                Code.Character,
                command,
                nativeCode: 0,
                Modifiers.Control,
                KeyAction.Press);
            application.Input(in stroke);
        });
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
