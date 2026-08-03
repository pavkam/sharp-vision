// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using Layout;

using Scrolling;

using SharpVision.Controls.Input;
using SharpVision.Terminal.Input;

using LayoutStack = Layout.Stack;

/// <summary>Displays hierarchical data as an expandable and collapsible tree of items.</summary>
[PublicAPI]
public sealed class TreeView: CompositeControlBase
{
    /// <inheritdoc/>
    protected override ThemeRole ThemeRole => ThemeRole.Container;
    private readonly LayoutStack _itemsStack;
    private readonly StyleSlot<ScrollBarStyle> _scrollBarStyle;
    private readonly CurrentItemNavigator _navigator;
    private readonly HashSet<TreeViewItem> _selectedItems = [];
    private TreeViewItem? _selectionAnchor;
    private readonly List<ControlBase> _flatBuffer = [];
    private readonly List<TreeViewItemCollection> _walkItems = [];
    private readonly List<int> _walkIndex = [];
    private HashSet<TreeViewItem> _hooked = [];
    private HashSet<TreeViewItem> _hookedNext = [];
    private int _selectionVersion;
    private bool _rebuildScheduled;
    private int _batchUpdateDepth;

    /// <summary>Gets or sets the complete local style for this control's generated scrollbar.</summary>
    /// <remarks>
    /// Null returns the bar to the library default for this control, which is
    /// <see cref="ScrollBarStyle.ThinBlock"/>. An explicit value stays caller-owned. The
    /// generated bar is a private retained part, so this proxy is the only way to reach it.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached tree view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    public ScrollBarStyle? ScrollBarStyle
    {
        get => _scrollBarStyle.Local;
        set => _scrollBarStyle.Local = value;
    }

    /// <summary>Gets the resolved style applied to the generated scrollbar.</summary>
    /// <remarks>
    /// Resolved by the bar itself, so a null local value reports whatever the active Theme or the
    /// library default supplies rather than an opinion this control baked in.
    /// </remarks>
    public ScrollBarStyle ActualScrollBarStyle => _scrollBarStyle.Actual;

    /// <summary>Raised after the generated scroll container's offset commits.</summary>
    /// <remarks>
    /// The scrolling items container is a private retained part; this forwards its
    /// <see cref="Container.ScrollChanged"/> so a consumer can observe scroll position without
    /// reaching into private presentation trees (see #75).
    /// </remarks>
    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged
    {
        add => _itemsStack.ScrollChanged += value;
        remove => _itemsStack.ScrollChanged -= value;
    }

    /// <summary>Gets the committed non-negative content extent of the generated scroll container.</summary>
    public Size Extent => _itemsStack.Extent;

    /// <summary>Gets the committed non-negative visible extent of the generated scroll container.</summary>
    public Size Viewport => _itemsStack.Viewport;

    /// <summary>Gets or sets the valid horizontal content offset of the generated scroll container.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached tree view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    public int HorizontalOffset
    {
        get => _itemsStack.HorizontalOffset;
        set => _itemsStack.HorizontalOffset = value;
    }

    /// <summary>Gets or sets the valid vertical content offset of the generated scroll container.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached tree view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    public int VerticalOffset
    {
        get => _itemsStack.VerticalOffset;
        set => _itemsStack.VerticalOffset = value;
    }

    /// <summary>Scrolls the generated scroll container by signed cell deltas with saturation and
    /// endpoint clamping.</summary>
    /// <param name="x">The requested horizontal delta.</param>
    /// <param name="y">The requested vertical delta.</param>
    /// <param name="cause">The defined input path.</param>
    /// <returns>True when at least one offset changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached tree view is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    public bool ScrollBy(int x, int y, ScrollCause cause = ScrollCause.Programmatic) =>
        _itemsStack.ScrollBy(x, y, cause);

    /// <summary>Scrolls minimally to expose one owned item, without requiring the caller to know
    /// about the private realized visual tree.</summary>
    /// <param name="item">The non-null owned item.</param>
    /// <returns>True when at least one offset changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item is not currently realized as a visible descendant.</exception>
    /// <exception cref="InvalidOperationException">The attached tree view is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    public bool BringItemIntoView(TreeViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _itemsStack.BringIntoView(item);
    }

    /// <summary>Initializes a framed focusable tree view with an empty root item collection.</summary>
    public TreeView()
    {
        _itemsStack = new LayoutStack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded
        };
        _navigator = new CurrentItemNavigator(CollectVisibleItems);

        var root = new Dock();
        root.Children.Add(_itemsStack);

        InitializeContent(root);
        _scrollBarStyle = InitializePartStyle(
            Scrolling.ScrollBarStyle.ForwardingDefinition,
            nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, _itemsStack, nameof(ScrollBarStyle));
        Items = new TreeViewItemCollection { Owner = this };
        Focusable = true;
        TabStop = true;
        TabNavigation = TabNavigation.None;
        _ = AddHandler(Events.Key, OnKeyRouted);
    }

    /// <summary>Raised before a caller- or input-driven selection change is committed.</summary>
    /// <remarks>
    /// Setting <see cref="System.ComponentModel.CancelEventArgs.Cancel"/> abandons the proposal. A
    /// handler that changes the selection itself also abandons it, because the proposal it was
    /// asked about no longer describes the current state. Normalization the control performs on
    /// its own behalf is not cancellable.
    /// </remarks>
    public event EventHandler<TreeViewSelectionChangingEventArgs>? SelectionChanging;

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

            // Publish the mode itself before normalizing, so an observer that reacts to the
            // selection notifications below already sees the new mode. Selection notifications are
            // not a substitute for the property name a two-way binding needs.
            _ = SetProperty(ref field, value, InvalidationImpact.Render);

            // Mode transitions invalidate pending proposals even when normalization leaves the
            // selection unchanged.
            _selectionVersion++;

            if (value == TreeSelectionMode.None)
            {
                _ = CommitSelection([]);
            }
            else if (value == TreeSelectionMode.Single && _selectedItems.Count > 1)
            {
                var first = CollectSelectedItems().FirstOrDefault();
                _ = CommitSelection(first is null ? [] : [first]);
            }
        }
    } = TreeSelectionMode.Single;

    /// <summary>Gets or sets the check-mark presentation shared by this tree's checkable items.</summary>
    /// <remarks>
    /// Null leaves each item on the library default. An item may still override this locally
    /// through <see cref="TreeViewItem.CheckMark"/>, giving the precedence local item, then owning
    /// tree, then library default. Only the mark layout and glyphs are shared; a tree row paints
    /// itself from its own resolved style, so no CheckBox appearance is applied here.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached tree view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    public CheckMark? CheckMark
    {
        get;
        set
        {
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            var previous = ActualCheckMark;
            field = value;
            var current = ActualCheckMark;

            // The mark occupies cells ahead of the header, so a width change moves every row's
            // text and needs a measure pass rather than a repaint.
            var impact = previous.Width != current.Width
                ? InvalidationImpact.Measure
                : previous == current
                    ? InvalidationImpact.None
                    : InvalidationImpact.Render;
            NotifyPropertyChanged(nameof(CheckMark), impact);

            if (previous != current)
            {
                NotifyPropertyChanged(nameof(ActualCheckMark), InvalidationImpact.None);
                InvalidateItemChrome(impact);
            }
        }
    }

    /// <summary>Gets the check-mark presentation applied to items that do not override it.</summary>
    public CheckMark ActualCheckMark => CheckMark ?? Input.CheckMark.Brackets;

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

    /// <summary>Adds or removes one owned item from the selection without replacing the rest.</summary>
    /// <remarks>
    /// The single-item <see cref="SelectItem"/> replaces the whole selection, so programmatic
    /// multi-select previously required emulating input or rebuilding the complete set. Selecting
    /// while <see cref="SelectionMode"/> is <see cref="TreeSelectionMode.Single"/> replaces the
    /// selection, matching what input does. Deselecting is always permitted.
    /// </remarks>
    /// <param name="item">The non-null owned item.</param>
    /// <param name="selected">Whether the item should end up selected.</param>
    /// <returns><see langword="true"/> when the committed selection changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item is not owned by this tree view.</exception>
    /// <exception cref="InvalidOperationException">
    /// Selection was requested while the selection mode is <see cref="TreeSelectionMode.None"/>,
    /// or the attached tree view is mutated off-dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    public bool SetSelected(TreeViewItem item, bool selected)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!ReferenceEquals(item.FindTreeView(), this))
        {
            throw new ArgumentException("The item is not owned by this tree view.", nameof(item));
        }

        if (selected && SelectionMode == TreeSelectionMode.None)
        {
            throw new InvalidOperationException("TreeSelectionMode.None does not accept selection.");
        }

        VerifyMutable();

        if (selected && !item.EffectiveIsEnabled)
        {
            return false;
        }

        HashSet<TreeViewItem> next = [.. _selectedItems];

        if (selected)
        {
            if (SelectionMode == TreeSelectionMode.Single)
            {
                next.Clear();
            }

            _ = next.Add(item);
        }
        else
        {
            _ = next.Remove(item);
        }

        if (_selectedItems.SetEquals(next))
        {
            return false;
        }

        if (!CommitSelection(next, cancellable: true))
        {
            return false;
        }

        if (selected)
        {
            _selectionAnchor = item;
        }

        return true;
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

        _ = CommitSelection(CollectAllItems().Where(static item => item.IsEnabled), cancellable: true);
    }

    /// <summary>Clears the current selection.</summary>
    public void ClearSelection()
    {
        VerifyMutable();
        _ = CommitSelection([], cancellable: true);
    }

    /// <summary>Expands every item in the tree.</summary>
    public void ExpandAll()
    {
        BeginUpdate();

        try
        {
            SetExpandedForAll(Items, expanded: true);
        }
        finally
        {
            EndUpdate();
        }
    }

    /// <summary>Collapses every item in the tree.</summary>
    public void CollapseAll()
    {
        BeginUpdate();

        try
        {
            SetExpandedForAll(Items, expanded: false);
        }
        finally
        {
            EndUpdate();
        }
    }

    /// <summary>Begins a batch of structural changes, deferring the flat-list rebuild until the
    /// matching <see cref="EndUpdate"/>. Calls may nest; the rebuild happens once, when the
    /// outermost <see cref="EndUpdate"/> returns.</summary>
    /// <remarks>
    /// Every individual item mutation otherwise triggers its own complete flat-list rebuild.
    /// Wrapping a sequence of additions (for example, populating a tree from a filesystem walk
    /// or a data source) in <see cref="BeginUpdate"/>/<see cref="EndUpdate"/> replaces that
    /// per-mutation cost with a single rebuild after the whole batch commits.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached tree view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    public void BeginUpdate()
    {
        VerifyMutable();
        _batchUpdateDepth++;
    }

    /// <summary>Ends a batch begun by <see cref="BeginUpdate"/>. Rebuilds the flat list once,
    /// only when this call closes the outermost pending batch.</summary>
    /// <exception cref="InvalidOperationException">
    /// The attached tree view is mutated off-dispatcher, or this call has no matching
    /// <see cref="BeginUpdate"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    public void EndUpdate()
    {
        VerifyMutable();

        if (_batchUpdateDepth == 0)
        {
            throw new InvalidOperationException("EndUpdate was called without a matching BeginUpdate.");
        }

        _batchUpdateDepth--;

        if (_batchUpdateDepth == 0)
        {
            RebuildFlatList();
        }
    }

    /// <summary>Verifies the owning tree is mutable on the current dispatcher.</summary>
    /// <exception cref="InvalidOperationException">The tree is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree is disposed.</exception>
    internal void VerifyTreeMutable() => VerifyMutable();

    /// <summary>Notifies the tree that a structural change requires rebuilding the flat list.</summary>
    internal void NotifyStructureChanged()
    {
        if (_rebuildScheduled || _batchUpdateDepth > 0)
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

        if (eventArgs.Phase != RoutingPhase.Bubble || eventArgs.Stroke.Action != KeyAction.Press)
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

        // PageUp/PageDown: move by a viewport's worth of realized item height. Handling the key
        // here - rather than leaving it unhandled - is what stops it from escaping to page an
        // enclosing scrollable container out from under the still-focused tree (see #210).
        if (eventArgs.Stroke.Code is Code.PageUp or Code.PageDown)
        {
            var visible = CollectVisibleItems();

            if (visible.Count > 0)
            {
                var target = StepPage(visible, eventArgs.Stroke.Code == Code.PageDown ? 1 : -1);
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

    private void CommitCurrent(ControlBase current, Modifiers modifiers)
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

        return CommitSelection(next, cancellable: true);
    }

    private bool CommitSelection(IEnumerable<TreeViewItem> items, bool cancellable = false)
    {
        var next = new HashSet<TreeViewItem>(items);
        var ownedItems = CollectAllItems();

        // Retention filters on ownership only - EffectiveIsEnabled walks the whole ancestor chain,
        // so a disabled ancestor would wipe every realized item's selection on the next rebuild
        // regardless of the item's own state. "Disabled items are never selected" is instead
        // enforced by the request paths themselves (SetSelected, ApplyInputSelection, SelectAll)
        // before they ever reach here (see #201).
        _ = next.RemoveWhere(item => !ownedItems.Contains(item));

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

        if (!changed)
        {
            return false;
        }

        // Capture the delta against the previous set before it is replaced. Ordering both
        // snapshots by the tree walk keeps them stable and comparable to SelectedItems.
        List<TreeViewItem> added = [];
        List<TreeViewItem> removed = [];

        foreach (var item in ownedItems)
        {
            if (next.Contains(item) && !_selectedItems.Contains(item))
            {
                added.Add(item);
            }
            else if (!next.Contains(item) && _selectedItems.Contains(item))
            {
                removed.Add(item);
            }
        }

        // A detached item leaves the owned walk, so it can only be reported as removed here.
        foreach (var item in _selectedItems)
        {
            if (!next.Contains(item) && !ownedItems.Contains(item))
            {
                removed.Add(item);
            }
        }

        var version = _selectionVersion;

        if (cancellable)
        {
            var changing = new TreeViewSelectionChangingEventArgs(added, removed);
            SelectionChanging?.Invoke(this, changing);

            // A handler that mutated the selection invalidated the proposal it was shown, so the
            // stale delta must not be committed on top of whatever it decided.
            if (changing.Cancel ||
                version != _selectionVersion ||
                (SelectionMode == TreeSelectionMode.None && next.Count > 0))
            {
                return false;
            }
        }

        _selectionVersion++;

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
        NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);
        NotifyPropertyChanged(nameof(SelectedItems), InvalidationImpact.Render);
        SelectionChanged?.Invoke(
            this,
            new TreeViewSelectionChangedEventArgs(previous, SelectedItem, added, removed));

        return true;
    }

    internal void NotifyCheckStateChanged(TreeViewItem item)
    {
        _ = item;
        Invalidate(Invalidation.Render);
    }

    private void InvalidateItemChrome(InvalidationImpact impact)
    {
        // Items resolve the shared mark on demand, so they only need to be told to redraw or, when
        // the reservation moved, to measure again.
        foreach (var control in _itemsStack.Children)
        {
            if (control is TreeViewItem item)
            {
                item.NotifyCheckMarkChanged(impact);
            }
        }
    }

    internal void NotifyItemEnabledChanged(TreeViewItem item)
    {
        _ = item;
        RebuildFlatList();
    }

    private void RebuildFlatList()
    {
        _rebuildScheduled = false;
        _flatBuffer.Clear();
        FlattenVisible(_flatBuffer);

        // Hook only the difference. Detaching every handler and reattaching it on every rebuild
        // made a single item mutation proportional to the whole visible list.
        _hookedNext.Clear();

        foreach (var control in _flatBuffer)
        {
            var item = (TreeViewItem) control;
            _ = _hookedNext.Add(item);

            if (!_hooked.Contains(item))
            {
                item.Invoked += OnItemInvoked;
            }
        }

        foreach (var item in _hooked)
        {
            if (!_hookedNext.Contains(item))
            {
                item.Invoked -= OnItemInvoked;
            }
        }

        (_hooked, _hookedNext) = (_hookedNext, _hooked);

        // One validated snapshot instead of a clear plus one validated add per item. The registry
        // also short-circuits an identical order, so a rebuild that changes nothing costs nothing.
        _itemsStack.Children.ReplaceAll(_flatBuffer);

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
        // Ownership alone gates retention here too - EffectiveIsEnabled would make a disabled
        // ancestor wipe every realized item's selection on the next rebuild (see #201).
        var ownedItems = CollectAllItems();
        var retained = _selectedItems.Where(ownedItems.Contains).ToArray();
        if (_selectionAnchor is not null && !ownedItems.Contains(_selectionAnchor))
        {
            _selectionAnchor = retained.FirstOrDefault();
        }

        // A structural rebuild may detach items a pending proposal still references.
        _selectionVersion++;
        _ = CommitSelection(retained);
    }

    private void FlattenVisible(List<ControlBase> destination)
    {
        // An explicit stack, because the hierarchy depth is caller-controlled and recursion here
        // would turn a valid deep tree into an unrecoverable StackOverflowException.
        _walkItems.Clear();
        _walkIndex.Clear();
        _walkItems.Add(Items);
        _walkIndex.Add(0);

        while (_walkItems.Count > 0)
        {
            var depth = _walkItems.Count - 1;
            var items = _walkItems[depth];
            var index = _walkIndex[depth];

            if (index >= items.Count)
            {
                _walkItems.RemoveAt(depth);
                _walkIndex.RemoveAt(depth);
                continue;
            }

            _walkIndex[depth] = index + 1;
            var item = items[index];
            item.Depth = depth;
            item.Focusable = false;
            item.TabStop = false;
            destination.Add(item);

            if (item.HasChildren && item.IsExpanded)
            {
                _walkItems.Add(item.Children);
                _walkIndex.Add(0);
            }
        }
    }

    private void OnItemInvoked(object? sender, ActivationEventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is TreeViewItem item)
        {
            NotifyItemInvoked(item, eventArgs.Cause, item.LastModifiers);
        }
    }

    // Accumulates realized item heights from the current position until the sum reaches the
    // committed viewport height, rather than treating the viewport's cell height as an item
    // count (the defect #212 fixed for ListView's identical shape). At least one step always
    // happens because the loop advances before its first accumulated check.
    private ControlBase StepPage(List<ControlBase> visible, int direction)
    {
        var index = _navigator.Current is { } current ? visible.IndexOf(current) : -1;

        if (index < 0)
        {
            index = direction > 0 ? -1 : visible.Count;
        }

        var target = Math.Max(1, Viewport.Height);
        var accumulated = 0;

        while (true)
        {
            var next = index + direction;

            if (next < 0 || next >= visible.Count)
            {
                return visible[Math.Clamp(next, 0, visible.Count - 1)];
            }

            index = next;
            accumulated += Math.Max(0, visible[index].Bounds.Height);

            if (accumulated >= target)
            {
                return visible[index];
            }
        }
    }

    private List<ControlBase> CollectVisibleItems()
    {
        List<ControlBase> result = [];

        foreach (var child in _itemsStack.Children)
        {
            if (child is TreeViewItem { EffectiveIsVisible: true, EffectiveIsEnabled: true } item)
            {
                result.Add(item);
            }
        }

        return result;
    }

    private static TreeViewItem? FindParentItem(TreeViewItem target) =>
        // The item already knows its owning collection, and that collection knows its owning item,
        // so the parent is a direct lookup. Walking the whole hierarchy to rediscover it was both
        // recursive and quadratic.
        target.ParentCollection?.ParentItem;

    private static void SetExpandedForAll(TreeViewItemCollection items, bool expanded)
    {
        // Iterative for the same reason as the flatten walk: depth is caller-controlled.
        List<TreeViewItemCollection> pending = [items];

        while (pending.Count > 0)
        {
            var current = pending[^1];
            pending.RemoveAt(pending.Count - 1);

            foreach (var item in current)
            {
                if (item.HasChildren)
                {
                    item.IsExpanded = expanded;
                    pending.Add(item.Children);
                }
            }
        }
    }

    private static int IndexOf(List<ControlBase> items, ControlBase value)
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
        List<TreeViewItemCollection> pending = [Items];
        List<int> cursors = [0];

        // Preorder without recursion so a deep caller hierarchy cannot exhaust the stack.
        while (pending.Count > 0)
        {
            var depth = pending.Count - 1;
            var items = pending[depth];
            var index = cursors[depth];

            if (index >= items.Count)
            {
                pending.RemoveAt(depth);
                cursors.RemoveAt(depth);
                continue;
            }

            cursors[depth] = index + 1;
            var item = items[index];
            result.Add(item);
            pending.Add(item.Children);
            cursors.Add(0);
        }

        return result;
    }

    private List<TreeViewItem> CollectSelectedItems() =>
        [.. CollectAllItems().Where(_selectedItems.Contains)];

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            SelectionChanging = null;
            SelectionChanged = null;
            ItemInvoked = null;
        }
    }
}
