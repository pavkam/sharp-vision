// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using Layout;

using SharpVision.Terminal.Input;

using LayoutStack = Layout.Stack;

/// <summary>Displays hierarchical data as an expandable and collapsible tree of items.</summary>
[PublicAPI]
public sealed class TreeView: CompositeControl
{
    /// <inheritdoc/>
    protected override ThemeRole ThemeRole => ThemeRole.Container;
    private readonly LayoutStack _itemsStack;
    private readonly CurrentItemNavigator _navigator;
    private readonly HashSet<TreeViewItem> _selectedItems = [];
    private TreeViewItem? _selectionAnchor;
    private bool _rebuildScheduled;
    private bool _batchUpdate;

    /// <summary>Initializes a framed focusable tree view with an empty root item collection.</summary>
    public TreeView()
    {
        _itemsStack = new LayoutStack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ScrollBarStyle = ScrollBarStyle.ThinBlock
        };
        _navigator = new CurrentItemNavigator(CollectVisibleItems);

        var root = new Dock();
        root.Children.Add(_itemsStack);

        InitializeContent(root);
        Items = new TreeViewItemCollection { Owner = this };
        Focusable = true;
        TabStop = true;
        TabNavigation = TabNavigation.None;
        _ = AddHandler(Events.Key, OnKeyRouted);
    }

    /// <summary>Raised after the selected item changes.</summary>
    public event EventHandler<TreeViewSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised after an item is activated by keyboard or pointer.</summary>
    public event EventHandler<TreeViewItemInvokedEventArgs>? ItemInvoked;

    /// <summary>Gets the typed root item collection.</summary>
    public TreeViewItemCollection Items { get; }

    /// <summary>Gets the currently selected item, or null.</summary>
    public TreeViewItem? SelectedItem { get; private set; }

    /// <summary>Gets the current selection in stable tree order.</summary>
    public IReadOnlyList<TreeViewItem> SelectedItems => CollectSelectedItems();

    /// <summary>Gets or sets whether the tree permits no, one, or many selected items.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="TreeSelectionMode"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached tree view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    public TreeSelectionMode SelectionMode
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The selection mode is unknown.");
            }

            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;

            if (value == TreeSelectionMode.None)
            {
                CommitSelection([]);
            }
            else if (value == TreeSelectionMode.Single && _selectedItems.Count > 1)
            {
                var first = CollectSelectedItems().FirstOrDefault();
                CommitSelection(first is null ? [] : [first]);
            }
        }
    } = TreeSelectionMode.Single;

    /// <summary>Gets or sets the number of cells per indentation level.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached tree view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    public int Indent
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                RebuildFlatList();
            }
        }
    } = 2;

    /// <summary>Programmatically selects the specified owned item.</summary>
    /// <param name="item">The non-null item to select.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item is not owned by this tree view.</exception>
    public void SelectItem(TreeViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!ReferenceEquals(item.FindTreeView(), this))
        {
            throw new ArgumentException("The item is not owned by this tree view.", nameof(item));
        }

        _ = _navigator.SetCurrent(item);
        _ = ApplyInputSelection(item, Modifiers.None);
    }

    /// <summary>Selects every enabled item when multiple selection is enabled.</summary>
    /// <exception cref="InvalidOperationException">The selection mode is not multiple.</exception>
    public void SelectAll()
    {
        VerifyMutable();

        if (SelectionMode != TreeSelectionMode.Multiple)
        {
            throw new InvalidOperationException("SelectAll requires multiple selection mode.");
        }

        CommitSelection(CollectAllItems().Where(static item => item.IsEnabled));
    }

    /// <summary>Clears the current selection.</summary>
    public void ClearSelection()
    {
        VerifyMutable();
        CommitSelection([]);
    }

    /// <summary>Expands every item in the tree.</summary>
    public void ExpandAll()
    {
        _batchUpdate = true;

        try
        {
            ExpandAllRecursive(Items);
        }
        finally
        {
            _batchUpdate = false;
        }

        RebuildFlatList();
    }

    /// <summary>Collapses every item in the tree.</summary>
    public void CollapseAll()
    {
        _batchUpdate = true;

        try
        {
            CollapseAllRecursive(Items);
        }
        finally
        {
            _batchUpdate = false;
        }

        RebuildFlatList();
    }

    /// <summary>Notifies the tree that a structural change requires rebuilding the flat list.</summary>
    internal void NotifyStructureChanged()
    {
        if (_rebuildScheduled || _batchUpdate)
        {
            return;
        }

        _rebuildScheduled = true;
        RebuildFlatList();
    }

    /// <summary>Notifies the tree that an item was activated by a pointer click.</summary>
    internal void NotifyItemInvoked(TreeViewItem item, ActivationCause cause, Modifiers modifiers = Modifiers.None)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!ReferenceEquals(item.FindTreeView(), this))
        {
            return;
        }

        _ = _navigator.SetCurrent(item);
        _ = ApplyInputSelection(item, modifiers);
        ItemInvoked?.Invoke(this, new TreeViewItemInvokedEventArgs(item, cause));
    }

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Bubble || eventArgs.Stroke.Action != KeyAction.Press)
        {
            return;
        }

        // Enter activates the current item. Space is a selection/check action.
        if (eventArgs.Stroke.Code == Code.Enter)
        {
            eventArgs.Handled = ActivateCurrent(eventArgs.Stroke.Modifiers);
            return;
        }

        if (eventArgs.Stroke.Code == Code.Character && eventArgs.Stroke.Character == new Rune(' '))
        {
            if (_navigator.Current is TreeViewItem { IsCheckable: true } checkable)
            {
                checkable.SetCheckState(checkable.IsChecked != true, ActivationCause.Keyboard, propagate: true);
                eventArgs.Handled = true;
                return;
            }

            if (_navigator.Current is TreeViewItem toggle)
            {
                eventArgs.Handled = ApplyInputSelection(
                    toggle,
                    SelectionMode == TreeSelectionMode.Multiple ? Modifiers.Control : Modifiers.None);
                return;
            }
        }

        if (eventArgs.Stroke.Code == Code.Character &&
            eventArgs.Stroke.Character == new Rune('a') &&
            (eventArgs.Stroke.Modifiers & Modifiers.Control) != 0 &&
            SelectionMode == TreeSelectionMode.Multiple)
        {
            SelectAll();
            eventArgs.Handled = true;
            return;
        }

        // Home/End: jump to first/last visible item
        if (eventArgs.Stroke.Code is Code.Home or Code.End)
        {
            var endpoints = CollectVisibleItems();

            if (endpoints.Count > 0)
            {
                var target = eventArgs.Stroke.Code == Code.Home ? endpoints[0] : endpoints[^1];
                _ = _navigator.SetCurrent(target);
                CommitCurrent(target, eventArgs.Stroke.Modifiers);
                eventArgs.Handled = true;
            }

            return;
        }

        // Left: collapse current or navigate to parent
        if (eventArgs.Stroke.Code == Code.Left)
        {
            eventArgs.Handled = HandleLeft();
            return;
        }

        // Right: expand current or navigate to first child
        if (eventArgs.Stroke.Code == Code.Right)
        {
            eventArgs.Handled = HandleRight();
            return;
        }

        // Up/Down: linear DFS navigation
        int direction;

        if (eventArgs.Stroke.Code == Code.Up)
        {
            direction = -1;
        }
        else if (eventArgs.Stroke.Code == Code.Down)
        {
            direction = 1;
        }
        else
        {
            return;
        }

        eventArgs.Handled = true;

        if (_navigator.Move(direction, wrap: false) && _navigator.Current is { } current)
        {
            CommitCurrent(current, eventArgs.Stroke.Modifiers);
        }
    }

    private bool HandleLeft()
    {
        if (_navigator.Current is not TreeViewItem item)
        {
            return false;
        }

        if (item is { HasChildren: true, IsExpanded: true })
        {
            item.IsExpanded = false;
            return true;
        }

        // Navigate to parent
        var parent = FindParentItem(item);

        if (parent is not null)
        {
            _ = _navigator.SetCurrent(parent);
            CommitCurrent(parent, Modifiers.None);
            return true;
        }

        return false;
    }

    private bool HandleRight()
    {
        if (_navigator.Current is not TreeViewItem item)
        {
            return false;
        }

        if (item is { HasChildren: true, IsExpanded: false })
        {
            item.IsExpanded = true;
            return true;
        }

        if (item is { HasChildren: true, IsExpanded: true })
        {
            var visibleItems = CollectVisibleItems();
            var index = IndexOf(visibleItems, item);

            if (index >= 0 && index + 1 < visibleItems.Count)
            {
                _ = _navigator.SetCurrent(visibleItems[index + 1]);
                CommitCurrent(visibleItems[index + 1], Modifiers.None);
                return true;
            }
        }

        return false;
    }

    private bool ActivateCurrent(Modifiers modifiers)
    {
        if (_navigator.Current is null)
        {
            var entries = CollectVisibleItems();

            if (entries.Count == 0)
            {
                return false;
            }

            _ = _navigator.SetCurrent(entries[0]);
        }

        if (_navigator.Current is TreeViewItem item)
        {
            item.ActivateFromOwner(ActivationCause.Keyboard);
            _ = ApplyInputSelection(item, modifiers);
            ItemInvoked?.Invoke(this, new TreeViewItemInvokedEventArgs(item, ActivationCause.Keyboard));
            return true;
        }

        return false;
    }

    private void CommitCurrent(Control current, Modifiers modifiers)
    {
        if (current is TreeViewItem item)
        {
            _ = ApplyInputSelection(item, modifiers);
        }

        _ = _itemsStack.BringIntoView(current);
    }

    private bool ApplyInputSelection(TreeViewItem item, Modifiers modifiers)
    {
        if (SelectionMode == TreeSelectionMode.None || !item.IsEnabled)
        {
            return false;
        }

        var next = new HashSet<TreeViewItem>(_selectedItems);
        var control = (modifiers & Modifiers.Control) != 0;
        var shift = (modifiers & Modifiers.Shift) != 0;
        TreeViewItem[] range = [];

        if (SelectionMode == TreeSelectionMode.Multiple && shift && _selectionAnchor is not null)
        {
            var visible = CollectVisibleItems();
            var start = IndexOf(visible, _selectionAnchor);
            var end = IndexOf(visible, item);
            if (start >= 0 && end >= 0)
            {
                range = [..
                    visible
                        .Skip(Math.Min(start, end))
                        .Take(Math.Abs(end - start) + 1)
                        .Cast<TreeViewItem>()
                        .Where(static candidate => candidate.IsEnabled)];
                next.Clear();
                next.UnionWith(range);
            }
        }
        else if (SelectionMode == TreeSelectionMode.Multiple && control)
        {
            if (!next.Remove(item))
            {
                _ = next.Add(item);
            }
            _selectionAnchor = item;
        }
        else
        {
            next.Clear();
            _ = next.Add(item);
            _selectionAnchor = item;
        }

        if (range.Length > 0)
        {
            _selectionAnchor ??= item;
        }

        CommitSelection(next);
        return true;
    }

    private void CommitSelection(IEnumerable<TreeViewItem> items)
    {
        var next = new HashSet<TreeViewItem>(items);
        var ownedItems = CollectAllItems();
        _ = next.RemoveWhere(item => !item.EffectiveIsEnabled || !ownedItems.Contains(item));

        if (SelectionMode == TreeSelectionMode.None)
        {
            next.Clear();
        }
        else if (SelectionMode == TreeSelectionMode.Single && next.Count > 1)
        {
            var first = CollectAllItems().FirstOrDefault(next.Contains);
            next = first is null ? [] : [first];
        }

        var previous = SelectedItem;
        var changed = !_selectedItems.SetEquals(next);

        foreach (var item in _selectedItems)
        {
            item.CommitSelection(next.Contains(item));
        }

        foreach (var item in next)
        {
            item.CommitSelection(true);
        }

        _selectedItems.Clear();
        _selectedItems.UnionWith(next);
        SelectedItem = CollectSelectedItems().FirstOrDefault();

        if (changed)
        {
            NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);
            NotifyPropertyChanged(nameof(SelectedItems), InvalidationImpact.Render);
            SelectionChanged?.Invoke(this, new TreeViewSelectionChangedEventArgs(previous, SelectedItem));
        }
    }

    internal void NotifyCheckStateChanged(TreeViewItem item)
    {
        _ = item;
        Invalidate(Invalidation.Render);
    }

    internal void NotifyItemEnabledChanged(TreeViewItem item)
    {
        _ = item;
        RebuildFlatList();
    }

    private void RebuildFlatList()
    {
        _rebuildScheduled = false;

        // Detach invoked handlers from existing items
        foreach (var child in _itemsStack.Children)
        {
            if (child is TreeViewItem item)
            {
                item.Invoked -= OnItemInvoked;
            }
        }

        _itemsStack.Children.Clear();
        AddItemsToStack(Items, depth: 0);

        // Repair navigator current if it was removed from the visible list
        if (_navigator.Current is TreeViewItem currentItem)
        {
            var visible = CollectVisibleItems();

            if (IndexOf(visible, currentItem) < 0)
            {
                _ = _navigator.SetCurrent(null);
            }
        }

        // Repair selection only when nodes are detached; collapsing a branch does not erase state.
        var ownedItems = CollectAllItems();
        var retained = _selectedItems.Where(item => ownedItems.Contains(item) && item.EffectiveIsEnabled).ToArray();
        if (_selectionAnchor is not null && (!ownedItems.Contains(_selectionAnchor) || !_selectionAnchor.EffectiveIsEnabled))
        {
            _selectionAnchor = retained.FirstOrDefault();
        }

        CommitSelection(retained);
    }

    private void AddItemsToStack(TreeViewItemCollection items, int depth)
    {
        foreach (var item in items)
        {
            item.Depth = depth;
            item.Focusable = false;
            item.TabStop = false;
            item.Invoked += OnItemInvoked;
            _itemsStack.Children.Add(item);

            if (item.HasChildren && item.IsExpanded)
            {
                AddItemsToStack(item.Children, depth + 1);
            }
        }
    }

    private List<Control> CollectVisibleItems()
    {
        List<Control> result = [];

        foreach (var child in _itemsStack.Children)
        {
            if (child is TreeViewItem { EffectiveIsVisible: true, EffectiveIsEnabled: true } item)
            {
                result.Add(item);
            }
        }

        return result;
    }

    private TreeViewItem? FindParentItem(TreeViewItem target) =>
        FindParentIn(Items, target);

    private static TreeViewItem? FindParentIn(TreeViewItemCollection items, TreeViewItem target)
    {
        foreach (var item in items)
        {
            foreach (var child in item.Children)
            {
                if (ReferenceEquals(child, target))
                {
                    return item;
                }
            }

            var found = FindParentIn(item.Children, target);

            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private void OnItemInvoked(object? sender, ActivationEventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is TreeViewItem item)
        {
            NotifyItemInvoked(item, eventArgs.Cause, item.LastModifiers);
        }
    }

    private static void ExpandAllRecursive(TreeViewItemCollection items)
    {
        foreach (var item in items)
        {
            if (item.HasChildren)
            {
                item.IsExpanded = true;
                ExpandAllRecursive(item.Children);
            }
        }
    }

    private static void CollapseAllRecursive(TreeViewItemCollection items)
    {
        foreach (var item in items)
        {
            if (item.HasChildren)
            {
                item.IsExpanded = false;
                CollapseAllRecursive(item.Children);
            }
        }
    }

    private static int IndexOf(List<Control> items, Control value)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (ReferenceEquals(items[index], value))
            {
                return index;
            }
        }

        return -1;
    }

    private List<TreeViewItem> CollectAllItems()
    {
        List<TreeViewItem> result = [];
        AddAllItems(Items, result);
        return result;
    }

    private static void AddAllItems(TreeViewItemCollection items, List<TreeViewItem> result)
    {
        foreach (var item in items)
        {
            result.Add(item);
            AddAllItems(item.Children, result);
        }
    }

    private List<TreeViewItem> CollectSelectedItems() =>
        [.. CollectAllItems().Where(_selectedItems.Contains)];

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            SelectionChanged = null;
            ItemInvoked = null;
        }
    }
}
