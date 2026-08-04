// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using SharpVision.Controls.Layout;
using SharpVision.Controls.Scrolling;
using SharpVision.Terminal.Input;

using DisplayText = Controls.Display.Text;
using LayoutStack = Controls.Layout.Stack;

/// <summary>Provides a sidebar navigation control with typed items, groups, header, and footer.</summary>
[PublicAPI]
public sealed class NavigationView: CompositeControlBase
{
    private readonly LayoutStack _itemsStack;
    private readonly LayoutStack _footerStack;
    private readonly DisplayText _headerText;
    private readonly CurrentItemNavigator _navigator;
    private readonly StyleSlot<ScrollBarStyle> _scrollBarStyle;
    private readonly Dictionary<ControlBase, NavigationEntryPresentation> _requestedPresentations = [];
    private bool _isHandlingKnownRemoval;

    /// <summary>Gets or sets the complete local style for this control's generated scrollbar.</summary>
    /// <remarks>
    /// Null returns the bar to the library default for this control, which is
    /// <see cref="ScrollBarStyle.ThinLine"/>. An explicit value stays caller-owned. The
    /// generated bar is a private retained part, so this proxy is the only way to reach it.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached navigation view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The navigation view is disposed.</exception>
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
    /// <exception cref="InvalidOperationException">The attached navigation view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The navigation view is disposed.</exception>
    public int HorizontalOffset
    {
        get => _itemsStack.HorizontalOffset;
        set => _itemsStack.HorizontalOffset = value;
    }

    /// <summary>Gets or sets the valid vertical content offset of the generated scroll container.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached navigation view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The navigation view is disposed.</exception>
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
    /// <exception cref="InvalidOperationException">The attached navigation view is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The navigation view is disposed.</exception>
    public bool ScrollBy(int x, int y, ScrollCause cause = ScrollCause.Programmatic) =>
        _itemsStack.ScrollBy(x, y, cause);

    /// <summary>Scrolls minimally to expose one owned entry, without requiring the caller to know
    /// about the private realized visual tree.</summary>
    /// <param name="item">The non-null owned navigation entry.</param>
    /// <returns>True when at least one offset changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item is not a descendant of the scrolling items container.</exception>
    /// <exception cref="InvalidOperationException">The attached navigation view is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The navigation view is disposed.</exception>
    public bool BringItemIntoView(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _itemsStack.BringIntoView(item);
    }

    /// <summary>Initializes a quiet square navigation background with an empty item collection.</summary>
    public NavigationView()
    {
        _headerText = new DisplayText(string.Empty)
        {
            UseMnemonic = true,
            Visibility = Visibility.Collapsed,
        };

        _footerStack = new LayoutStack();
        _itemsStack = new LayoutStack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded
        };
        _navigator = new CurrentItemNavigator(CollectNavigableEntries);

        var root = new Dock();
        Dock.SetSide(_headerText, DockSide.Top);
        root.Children.Add(_headerText);
        Dock.SetSide(_footerStack, DockSide.Bottom);
        root.Children.Add(_footerStack);
        root.Children.Add(_itemsStack);

        _itemsStack.Children.Changed += OnEntryHostChanged;
        _footerStack.Children.Changed += OnEntryHostChanged;

        InitializeContent(root);
        _scrollBarStyle = InitializePartStyle(
            Controls.Scrolling.ScrollBarStyle.ForwardingDefinition,
            nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, _itemsStack, nameof(ScrollBarStyle));
        Items = new NavigationViewEntryCollection(this, isFooter: false);
        FooterItems = new NavigationViewEntryCollection(this, isFooter: true);
        Focusable = true;
        TabStop = true;
        TabNavigation = TabNavigation.None;
        _ = AddHandler(Events.Key, OnKeyRouted);
    }

    /// <summary>Raised after the selected item changes.</summary>
    public event EventHandler<NavigationViewSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Gets or sets an optional header title.</summary>
    /// <exception cref="InvalidOperationException">The attached view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The view is disposed.</exception>
    public string? Header
    {
        get;
        set
        {
            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                _headerText.Content = value ?? string.Empty;
                _headerText.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }

    /// <inheritdoc/>
    protected override string? AccessKeyText => Header;

    /// <summary>Gets the typed main item collection.</summary>
    public NavigationViewEntryCollection Items { get; }

    /// <summary>Gets the typed footer item collection.</summary>
    public NavigationViewEntryCollection FooterItems { get; }

    /// <summary>Gets the currently selected item, or null.</summary>
    public NavigationViewItem? SelectedItem { get; private set; }

    /// <summary>Selects a currently owned navigation item without moving keyboard focus.</summary>
    /// <param name="item">The non-null item owned by this navigation view.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="item"/> is not owned by this navigation view.</exception>
    public void SelectItem(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!ReferenceEquals(item.FindNavigationView(), this))
        {
            throw new ArgumentException("The item is not owned by this navigation view.", nameof(item));
        }

        _ = _navigator.SetCurrent(item);
        Select(item);
    }

    /// <summary>Gets the item count for one section.</summary>
    internal int GetItemCount(bool isFooter) =>
        (isFooter ? _footerStack : _itemsStack).Children.Count;

    /// <summary>Gets one item by index in a section.</summary>
    internal ControlBase GetItem(int index, bool isFooter) =>
        (isFooter ? _footerStack : _itemsStack).Children[index];

    /// <summary>Adds one typed entry to a section.</summary>
    internal void AddEntry(ControlBase entry, bool isFooter) => InsertEntry(GetItemCount(isFooter), entry, isFooter);

    /// <summary>Inserts one typed entry at a position in a section.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    internal void InsertEntry(int index, ControlBase entry, bool isFooter)
    {
        Debug.Assert(
            entry is NavigationViewItem or NavigationViewGroup or NavigationViewSeparator,
            "Navigation view entries are constrained by typed collection overloads.");
        var stack = isFooter ? _footerStack : _itemsStack;

        // Ownership is secured before any authored property is captured or
        // overwritten. A rejected insertion must leave the caller's object
        // exactly as it found it.
        stack.Children.Insert(index, entry);
        _requestedPresentations.Add(
            entry,
            new NavigationEntryPresentation(entry.Focusable, entry.TabStop));

        if (entry is NavigationViewItem item)
        {
            item.Focusable = false;
            item.TabStop = false;
            item.Invoked += OnItemInvoked;
        }
    }

    /// <summary>Removes one typed entry from a section.</summary>
    internal bool RemoveEntry(ControlBase entry, bool isFooter)
    {
        var stack = isFooter ? _footerStack : _itemsStack;
        var repair = PrepareRemoval(entry);

        if (!stack.Children.Contains(entry))
        {
            return false;
        }

        if (entry is NavigationViewItem item)
        {
            item.Invoked -= OnItemInvoked;
        }

        RestorePresentation(entry);

        _isHandlingKnownRemoval = true;

        try
        {
            _ = stack.Children.Remove(entry);
        }
        finally
        {
            _isHandlingKnownRemoval = false;
        }

        CompleteRemoval(repair);

        return true;
    }

    /// <summary>Removes the owned entry at a position in a section.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current entries.</exception>
    internal void RemoveEntryAt(int index, bool isFooter)
    {
        var stack = isFooter ? _footerStack : _itemsStack;

        if ((uint) index >= (uint) stack.Children.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The removal index is outside the section.");
        }

        _ = RemoveEntry(stack.Children[index], isFooter);
    }

    /// <summary>Moves one owned entry to a different position within the same section, preserving its
    /// identity. SelectedItem is tracked by reference, not index, so a move never needs the
    /// PrepareRemoval/CompleteRemoval repair a removal does.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="oldIndex"/> or <paramref name="newIndex"/> is outside the current entries.
    /// </exception>
    internal void MoveEntry(int oldIndex, int newIndex, bool isFooter)
    {
        var stack = isFooter ? _footerStack : _itemsStack;

        if ((uint) oldIndex >= (uint) stack.Children.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(oldIndex), oldIndex, "The source index is outside the section.");
        }

        if ((uint) newIndex >= (uint) stack.Children.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newIndex), newIndex, "The destination index is outside the section.");
        }

        if (oldIndex == newIndex)
        {
            return;
        }

        var entry = stack.Children[oldIndex];
        stack.Children.RemoveAt(oldIndex);
        stack.Children.Insert(newIndex, entry);
    }

    /// <summary>Gets the position of one entry within a section, or -1 when not owned there.</summary>
    internal int IndexOfEntry(ControlBase entry, bool isFooter) =>
        (isFooter ? _footerStack : _itemsStack).Children.IndexOf(entry);

    /// <summary>Replaces the owned entry at a position in a section, preserving position.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current entries.</exception>
    internal void ReplaceEntryAt(int index, ControlBase entry, bool isFooter)
    {
        Debug.Assert(
            entry is NavigationViewItem or NavigationViewGroup or NavigationViewSeparator,
            "Navigation view entries are constrained by typed collection overloads.");
        var stack = isFooter ? _footerStack : _itemsStack;

        if ((uint) index >= (uint) stack.Children.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The replacement index is outside the section.");
        }

        var old = stack.Children[index];

        if (ReferenceEquals(old, entry))
        {
            return;
        }

        var repair = PrepareRemoval(old);

        if (old is NavigationViewItem oldItem)
        {
            oldItem.Invoked -= OnItemInvoked;
        }

        RestorePresentation(old);

        _isHandlingKnownRemoval = true;

        try
        {
            stack.Children[index] = entry;
        }
        finally
        {
            _isHandlingKnownRemoval = false;
        }

        _requestedPresentations.Add(
            entry,
            new NavigationEntryPresentation(entry.Focusable, entry.TabStop));

        if (entry is NavigationViewItem newItem)
        {
            newItem.Focusable = false;
            newItem.TabStop = false;
            newItem.Invoked += OnItemInvoked;
        }

        CompleteRemoval(repair);
    }

    // Repairs selection/current-item state for a top-level entry that left the tree without
    // going through RemoveEntry/ClearEntries — most notably a direct Dispose() call, which
    // ControlBase.DisposeCore removes from its owning slot as ordinary disposal, publishing this
    // same notification. RemoveEntry/ClearEntries already run the precise, position-aware
    // repair themselves and suppress this handler for their own mutation via
    // _isHandlingKnownRemoval, so this only ever reconciles the otherwise-unhandled path.
    private void OnEntryHostChanged()
    {
        if (_isHandlingKnownRemoval)
        {
            return;
        }

        // This notification publishes from inside DisposeCore, which removes the item from
        // its owning slot before IsDisposed itself flips true — IsDisposing is what's already
        // set at this point.
        if (_navigator.Current is { IsDisposing: true } or { IsDisposed: true })
        {
            _ = _navigator.SetCurrent(null);
        }

        if (SelectedItem is { IsDisposing: true } or { IsDisposed: true })
        {
            var remaining = CollectSelectableItems();
            Select(remaining.Count == 0 ? null : remaining[0]);
        }
    }

    // Captures whether the current-navigation or selected item is the root
    // being removed or one of its descendants, before detachment makes the
    // ancestor walk impossible. A group counts as its own root, so removing
    // an entire group (or clearing one) repairs a selected descendant the
    // same way removing that descendant directly would — this is also the
    // seam NavigationViewGroup uses to repair selection for removals that
    // never pass through RemoveEntry/ClearEntries at all.
    internal NavigationViewRemovalRepair PrepareRemoval(ControlBase root)
    {
        var currentRemoved = _navigator.Current is { } current &&
                             (ReferenceEquals(current, root) || IsDescendantOf(current, root));
        var selectedRemoved = SelectedItem is { } selected &&
                              (ReferenceEquals(selected, root) || IsDescendantOf(selected, root));
        var selectedIndex = selectedRemoved ? CollectSelectableItems().IndexOf(SelectedItem!) : -1;

        return new NavigationViewRemovalRepair(currentRemoved, selectedRemoved, selectedIndex);
    }

    // Runs after detachment, using state captured by PrepareRemoval before
    // the removed subtree left the tree.
    internal void CompleteRemoval(NavigationViewRemovalRepair repair)
    {
        if (repair.CurrentRemoved)
        {
            _ = _navigator.SetCurrent(null);
        }

        if (repair.SelectedRemoved)
        {
            var remaining = CollectSelectableItems();

            // SelectedIndex is -1 whenever the selected item had already become unselectable
            // (e.g. disabled) before removal, since PrepareRemoval looks it up in the
            // selectable set alone — SelectItem/Select never require a selectable target. Treat
            // that the same as the empty-remaining case instead of indexing with the sentinel.
            Select(remaining.Count == 0
                ? null
                : remaining[Math.Max(0, Math.Min(repair.SelectedIndex, remaining.Count - 1))]);
        }
    }

    // Runs before the entry is detached so callers observe restored values
    // immediately, not this view's private presentation policy.
    private void RestorePresentation(ControlBase entry)
    {
        if (!_requestedPresentations.Remove(entry, out var presentation))
        {
            return;
        }

        if (entry is NavigationViewItem item)
        {
            item.Focusable = presentation.Focusable;
            item.TabStop = presentation.TabStop;
        }
    }

    /// <summary>Clears all entries in a section.</summary>
    internal void ClearEntries(bool isFooter)
    {
        var stack = isFooter ? _footerStack : _itemsStack;
        var repair = PrepareRemoval(stack);

        foreach (var child in stack.Children)
        {
            if (child is NavigationViewItem item)
            {
                item.Invoked -= OnItemInvoked;
            }

            RestorePresentation(child);
        }

        _isHandlingKnownRemoval = true;

        try
        {
            stack.Children.Clear();
        }
        finally
        {
            _isHandlingKnownRemoval = false;
        }

        CompleteRemoval(repair);
    }

    /// <summary>Updates the selected item when a child receives focus externally.</summary>
    internal void NotifyItemFocused(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _ = _navigator.SetCurrent(item);
        Select(item);
    }

    /// <summary>Commits an item activated by a grouped child through the owning view.</summary>
    /// <param name="item">The non-null item owned by this navigation view.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    internal void NotifyItemInvoked(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (ReferenceEquals(item.FindNavigationView(), this))
        {
            _ = _navigator.SetCurrent(item);
            Select(item);
        }
    }

    /// <summary>Focuses this view and invokes one mnemonic-selected item through its ordinary owner path.</summary>
    /// <param name="item">The available owned item declaring the matched mnemonic.</param>
    /// <returns>True when the item belongs to this view and was invoked.</returns>
    internal bool InvokeAccessKey(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!ReferenceEquals(item.FindNavigationView(), this))
        {
            return false;
        }

        _ = Focus();
        _ = _navigator.SetCurrent(item);
        item.ActivateFromOwner(ActivationCause.Keyboard);
        return true;
    }

    /// <summary>Focuses this view and toggles one mnemonic-selected group.</summary>
    /// <param name="group">The available owned group declaring the matched mnemonic.</param>
    /// <returns>True when the group belongs to this view and was toggled.</returns>
    internal bool InvokeAccessKey(NavigationViewGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (!ReferenceEquals(group.FindNavigationView(), this))
        {
            return false;
        }

        _ = Focus();
        NotifyGroupInvoked(group);
        group.IsExpanded = !group.IsExpanded;
        return true;
    }

    /// <summary>Commits one pointer-targeted group as the current keyboard entry.</summary>
    /// <param name="group">The non-null owned group.</param>
    internal void NotifyGroupInvoked(NavigationViewGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        _ = _navigator.SetCurrent(group);
    }

    /// <summary>Repairs selection after a retained group changes descendant visibility.</summary>
    /// <param name="group">The non-null owned group that changed expansion.</param>
    internal void NotifyGroupVisibilityChanged(NavigationViewGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (_navigator.Current is { } current && IsDescendantOf(current, group))
        {
            _ = _navigator.SetCurrent(group);
        }

        if (SelectedItem is null || SelectedItem.EffectiveIsVisible)
        {
            return;
        }

        var remaining = CollectSelectableItems();
        Select(remaining.Count == 0 ? null : remaining[0]);
    }

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Bubble || eventArgs.Stroke.Action != KeyAction.Press)
        {
            return;
        }

        int direction;

        if (eventArgs.Stroke.Code == Code.Enter ||
            (eventArgs.Stroke.Code == Code.Character && eventArgs.Stroke.Character == new Rune(' ')))
        {
            eventArgs.Handled = ActivateCurrent();
            return;
        }

        if (eventArgs.Stroke.Code is Code.Home or Code.End)
        {
            var endpoints = CollectNavigableEntries();

            if (endpoints.Count > 0)
            {
                var target = eventArgs.Stroke.Code == Code.Home ? endpoints[0] : endpoints[^1];
                _ = _navigator.SetCurrent(target);
                CommitCurrent(target);
                eventArgs.Handled = true;
            }

            return;
        }

        // PageUp/PageDown: move by a viewport's worth of realized item height. Handling the key
        // here - rather than leaving it unhandled - is what stops it from escaping to page an
        // enclosing scrollable container out from under the still-focused view (see #210).
        if (eventArgs.Stroke.Code is Code.PageUp or Code.PageDown)
        {
            var entries = CollectNavigableEntries();

            if (entries.Count > 0)
            {
                var target = StepPage(entries, eventArgs.Stroke.Code == Code.PageDown ? 1 : -1);
                _ = _navigator.SetCurrent(target);
                CommitCurrent(target);
                eventArgs.Handled = true;
            }

            return;
        }

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
            CommitCurrent(current);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            SelectionChanged = null;
            _ = _navigator.SetCurrent(null);
            Select(null);
        }
    }

    private void OnItemInvoked(object? sender, ActivationEventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is NavigationViewItem item)
        {
            NotifyItemInvoked(item);
        }
    }

    private bool ActivateCurrent()
    {
        if (_navigator.Current is null)
        {
            var entries = CollectNavigableEntries();

            if (entries.Count == 0)
            {
                return false;
            }

            _ = _navigator.SetCurrent(entries[0]);
        }

        if (_navigator.Current is NavigationViewGroup group)
        {
            group.IsExpanded = !group.IsExpanded;
            return true;
        }

        if (_navigator.Current is NavigationViewItem item)
        {
            item.ActivateFromOwner(ActivationCause.Keyboard);
            return true;
        }

        return false;
    }

    private void CommitCurrent(ControlBase current)
    {
        if (current is NavigationViewItem item)
        {
            Select(item);
        }

        if (IsDescendantOf(current, _itemsStack))
        {
            _ = _itemsStack.BringIntoView(current);
        }
    }

    private void Select(NavigationViewItem? item)
    {
        if (ReferenceEquals(SelectedItem, item))
        {
            return;
        }

        var previous = SelectedItem;

        if (previous is { IsDisposed: false })
        {
            previous.CommitSelection(false);
        }

        SelectedItem = item;

        item?.CommitSelection(true);

        SelectionChanged?.Invoke(this, new NavigationViewSelectionChangedEventArgs(previous, item));
    }

    private List<NavigationViewItem> CollectSelectableItems()
    {
        List<NavigationViewItem> result = [];
        CollectFrom(_itemsStack, result);
        CollectFrom(_footerStack, result);
        return result;
    }

    // Accumulates realized entry heights from the current position until the sum reaches the
    // committed viewport height, rather than treating the viewport's cell height as an entry
    // count (the defect #212 fixed for ListView's identical shape). At least one step always
    // happens because the loop advances before its first accumulated check.
    private ControlBase StepPage(List<ControlBase> entries, int direction)
    {
        var index = _navigator.Current is { } current ? entries.IndexOf(current) : -1;

        if (index < 0)
        {
            index = direction > 0 ? -1 : entries.Count;
        }

        var target = Math.Max(1, Viewport.Height);
        var accumulated = 0;

        while (true)
        {
            var next = index + direction;

            if (next < 0 || next >= entries.Count)
            {
                return entries[Math.Clamp(next, 0, entries.Count - 1)];
            }

            index = next;
            accumulated += Math.Max(0, entries[index].Bounds.Height);

            if (accumulated >= target)
            {
                return entries[index];
            }
        }
    }

    private List<ControlBase> CollectNavigableEntries()
    {
        List<ControlBase> result = [];
        CollectNavigableFrom(_itemsStack, result);
        CollectNavigableFrom(_footerStack, result);
        return result;
    }

    private static void CollectNavigableFrom(LayoutStack stack, List<ControlBase> result)
    {
        foreach (var child in stack.Children)
        {
            if (child is NavigationViewItem { EffectiveIsVisible: true, EffectiveIsEnabled: true } item)
            {
                result.Add(item);
            }
            else if (child is NavigationViewGroup { EffectiveIsVisible: true, EffectiveIsEnabled: true } group)
            {
                result.Add(group);

                if (!group.IsExpanded)
                {
                    continue;
                }

                for (var index = 0; index < group.ItemCount; index++)
                {
                    var sub = group.ItemAt(index);

                    if (sub is { EffectiveIsVisible: true, EffectiveIsEnabled: true })
                    {
                        result.Add(sub);
                    }
                }
            }
        }
    }

    private static void CollectFrom(LayoutStack stack, List<NavigationViewItem> result)
    {
        foreach (var child in stack.Children)
        {
            if (child is NavigationViewItem { EffectiveIsVisible: true, EffectiveIsEnabled: true } item)
            {
                result.Add(item);
            }
            else if (child is NavigationViewGroup { IsExpanded: true } group)
            {
                for (var index = 0; index < group.ItemCount; index++)
                {
                    var sub = group.ItemAt(index);

                    if (sub is { EffectiveIsVisible: true, EffectiveIsEnabled: true })
                    {
                        result.Add(sub);
                    }
                }
            }
        }
    }

    private static bool IsDescendantOf(ControlBase control, ControlBase ancestor)
    {
        for (var current = control.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }
}
