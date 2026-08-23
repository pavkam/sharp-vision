// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.SyntaxHighlighting;

using SharpVision.Menus;

/// <summary>Provides a default context menu with copy, selection, and folding commands for <see cref="CodeView"/>.</summary>
/// <remarks>
/// Assigned to <see cref="ControlBase.ContextMenu"/> by <see cref="CodeView"/>'s own constructor,
/// exactly like <c>TextInput</c> assigns its own default context menu - and, like that property,
/// freely replaceable: <c>codeView.ContextMenu = myOwnMenu;</c> replaces this default with anything
/// else, or with null to disable the right-click menu entirely.
/// </remarks>
[PublicAPI]
public sealed class CodeViewContextMenu: ContextMenu
{
    private readonly CodeView _owner;
    private readonly MenuItem _copyItem;
    private readonly MenuItem _selectAllItem;
    private readonly MenuItem _collapseAllItem;
    private readonly MenuItem _expandAllItem;

    /// <summary>Initializes a context menu bound to one owning code view.</summary>
    /// <param name="owner">The non-null owning code view.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public CodeViewContextMenu(CodeView owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;

        _copyItem = new MenuItem { Text = "Copy", ShortcutText = "Ctrl+C" };
        _selectAllItem = new MenuItem { Text = "Select All", ShortcutText = "Ctrl+A" };
        _collapseAllItem = new MenuItem { Text = "Collapse All Folds" };
        _expandAllItem = new MenuItem { Text = "Expand All Folds" };

        _copyItem.Invoked += OnCopy;
        _selectAllItem.Invoked += OnSelectAll;
        _collapseAllItem.Invoked += OnCollapseAll;
        _expandAllItem.Invoked += OnExpandAll;

        Items.Add(_copyItem);
        Items.Add(_selectAllItem);
        Items.Add(new MenuSeparator());
        Items.Add(_collapseAllItem);
        Items.Add(_expandAllItem);

        Opening += OnOpening;
    }

    private void OnOpening(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        _copyItem.IsEnabled = !_owner.Selection.IsEmpty;
        _selectAllItem.IsEnabled = _owner.Code.Length > 0;

        var foldingAvailable = _owner.IsFoldingEnabled && _owner.FoldRanges.Count > 0;
        _collapseAllItem.IsEnabled = foldingAvailable;
        _expandAllItem.IsEnabled = foldingAvailable;
    }

    private void OnCopy(object? sender, MenuItemInvokedEventArgs e)
    {
        _ = sender;
        _ = e;
        _owner.RequestClipboardCopy();
    }

    private void OnSelectAll(object? sender, MenuItemInvokedEventArgs e)
    {
        _ = sender;
        _ = e;
        _owner.SelectAll();
    }

    private void OnCollapseAll(object? sender, MenuItemInvokedEventArgs e)
    {
        _ = sender;
        _ = e;
        _owner.CollapseAll();
    }

    private void OnExpandAll(object? sender, MenuItemInvokedEventArgs e)
    {
        _ = sender;
        _ = e;
        _owner.ExpandAll();
    }
}
