// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using Menus;

/// <summary>Provides a default context menu with clipboard and editing commands for <see cref="TextInput"/>.</summary>
[PublicAPI]
public sealed class TextInputContextMenu: ContextMenu
{
    private readonly TextInput _textInput;
    private readonly MenuItem _cutItem;
    private readonly MenuItem _copyItem;
    private readonly MenuItem _pasteItem;
    private readonly MenuItem _selectAllItem;
    private readonly MenuItem _undoItem;
    private readonly MenuItem _redoItem;

    /// <summary>Initializes a context menu bound to the specified text input.</summary>
    /// <param name="textInput">The non-null owning text input.</param>
    internal TextInputContextMenu(TextInput textInput)
    {
        ArgumentNullException.ThrowIfNull(textInput);
        _textInput = textInput;

        _cutItem = new MenuItem { Text = "Cut", ShortcutText = "Ctrl+X" };
        _copyItem = new MenuItem { Text = "Copy", ShortcutText = "Ctrl+C" };
        _pasteItem = new MenuItem { Text = "Paste", ShortcutText = "Ctrl+V" };
        _selectAllItem = new MenuItem { Text = "Select All", ShortcutText = "Ctrl+A" };
        _undoItem = new MenuItem { Text = "Undo", ShortcutText = "Ctrl+Z" };
        _redoItem = new MenuItem { Text = "Redo", ShortcutText = "Ctrl+Y" };

        _cutItem.Invoked += OnCut;
        _copyItem.Invoked += OnCopy;
        _pasteItem.Invoked += OnPaste;
        _selectAllItem.Invoked += OnSelectAll;
        _undoItem.Invoked += OnUndo;
        _redoItem.Invoked += OnRedo;

        Items.Add(_undoItem);
        Items.Add(_redoItem);
        Items.Add(new MenuSeparator());
        Items.Add(_cutItem);
        Items.Add(_copyItem);
        Items.Add(_pasteItem);
        Items.Add(new MenuSeparator());
        Items.Add(_selectAllItem);

        Opening += OnOpening;
    }

    internal Action<string>? ClipboardWriter { get; set; }

    internal Func<string>? ClipboardReader { get; set; }

    private void OnOpening(object? sender, EventArgs e)
    {
        var hasSelection = _textInput.SelectionLength > 0;
        var isPassword = _textInput.PasswordCharacter.HasValue;
        var isReadOnly = _textInput.ReadOnly;

        _cutItem.Enabled = hasSelection && !isPassword && !isReadOnly;
        _copyItem.Enabled = hasSelection && !isPassword;
        _pasteItem.Enabled = !isReadOnly && ClipboardReader is not null && ClipboardReader().Length > 0;
        _selectAllItem.Enabled = _textInput.Text.Length > 0;
        _undoItem.Enabled = _textInput.CanUndo;
        _redoItem.Enabled = _textInput.CanRedo;
    }

    private void OnCut(object? sender, MenuItemInvokedEventArgs e)
    {
        var text = _textInput.CutSelection();

        if (text.Length > 0)
        {
            ClipboardWriter?.Invoke(text);
        }
    }

    private void OnCopy(object? sender, MenuItemInvokedEventArgs e)
    {
        var text = _textInput.CopySelection();

        if (text.Length > 0)
        {
            ClipboardWriter?.Invoke(text);
        }
    }

    private void OnPaste(object? sender, MenuItemInvokedEventArgs e)
    {
        if (ClipboardReader?.Invoke() is { Length: > 0 } text)
        {
            _textInput.PasteClipboard(text);
        }
    }

    private void OnSelectAll(object? sender, MenuItemInvokedEventArgs e) =>
        _textInput.Select(0, _textInput.Text.Length);

    private void OnUndo(object? sender, MenuItemInvokedEventArgs e) =>
        _textInput.Undo();

    private void OnRedo(object? sender, MenuItemInvokedEventArgs e) =>
        _textInput.Redo();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Opening -= OnOpening;
            _cutItem.Invoked -= OnCut;
            _copyItem.Invoked -= OnCopy;
            _pasteItem.Invoked -= OnPaste;
            _selectAllItem.Invoked -= OnSelectAll;
            _undoItem.Invoked -= OnUndo;
            _redoItem.Invoked -= OnRedo;
        }

        base.Dispose(disposing);
    }
}
