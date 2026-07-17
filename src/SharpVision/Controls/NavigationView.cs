// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Input;

/// <summary>Provides a sidebar navigation control with typed items, groups, header, and footer.</summary>
public sealed class NavigationView: CompositeControl
{
    private readonly Stack _itemsStack;
    private readonly Stack _footerStack;
    private readonly Text _headerText;
    private readonly ItemNavigator _navigator;

    /// <summary>Initializes a square semantic navigation surface with an empty item collection.</summary>
    public NavigationView()
    {
        _headerText = new Text(string.Empty)
        {
            Visibility = Visibility.Collapsed,
            Padding = new Thickness(1, 0),
            Attributes = TerminalAttributes.Bold,
        };

        _footerStack = new Stack();
        _itemsStack = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ScrollBarChrome = ScrollBarChrome.Thin,
        };
        _navigator = new ItemNavigator(CollectNavigableEntries);

        var root = new Dock();
        Dock.SetSide(_headerText, Side.Top);
        root.Children.Add(_headerText);
        Dock.SetSide(_footerStack, Side.Bottom);
        root.Children.Add(_footerStack);
        root.Children.Add(_itemsStack);

        InitializeContent(root);
        Items = new NavigationViewItems(this, isFooter: false);
        FooterItems = new NavigationViewItems(this, isFooter: true);
        BorderThickness = new Thickness(1);
        BorderGlyphs = Glyphs.Light;
        Background = ColorRole.Surface;
        Focusable = true;
        TabStop = true;
        TabNavigation = TabNavigation.None;
        _ = AddHandler(Events.Key, OnKeyRouted);
    }

    /// <summary>Raised after the selected item changes.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Gets or sets an optional header title.</summary>
    /// <exception cref="InvalidOperationException">The attached view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The view is disposed.</exception>
    public string? Header
    {
        get;
        set
        {
            if (SetProperty(ref field, value, ChangeImpact.Measure))
            {
                _headerText.Content = value ?? string.Empty;
                _headerText.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }

    /// <summary>Gets the typed main item collection.</summary>
    public NavigationViewItems Items { get; }

    /// <summary>Gets the typed footer item collection.</summary>
    public NavigationViewItems FooterItems { get; }

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
    internal Control GetItem(int index, bool isFooter) =>
        (isFooter ? _footerStack : _itemsStack).Children[index];

    /// <summary>Adds one typed entry to a section.</summary>
    internal void AddEntry(Control entry, bool isFooter)
    {
        Debug.Assert(
            entry is NavigationViewItem or NavigationViewGroup or NavigationViewSeparator,
            "Navigation view entries are constrained by typed collection overloads.");
        var stack = isFooter ? _footerStack : _itemsStack;

        if (entry is NavigationViewSeparator)
        {
            entry.Width = Length.Percent(100);
        }

        stack.Children.Add(entry);

        if (entry is NavigationViewItem item)
        {
            item.Focusable = false;
            item.TabStop = false;
            item.Invoked += OnItemInvoked;
        }
    }

    /// <summary>Removes one typed entry from a section.</summary>
    internal bool RemoveEntry(Control entry, bool isFooter)
    {
        var stack = isFooter ? _footerStack : _itemsStack;
        var currentRemoved = _navigator.Current is { } current &&
            (ReferenceEquals(current, entry) || IsDescendantOf(current, entry));
        var selectedIndex = SelectedItem is { } selected
            ? CollectSelectableItems().IndexOf(selected)
            : -1;

        if (!stack.Children.Remove(entry))
        {
            return false;
        }

        if (currentRemoved)
        {
            _ = _navigator.SetCurrent(null);
        }

        if (entry is NavigationViewItem item)
        {
            item.Invoked -= OnItemInvoked;

            if (ReferenceEquals(SelectedItem, item))
            {
                var remaining = CollectSelectableItems();
                Select(remaining.Count == 0 ? null : remaining[Math.Min(selectedIndex, remaining.Count - 1)]);
            }
        }

        return true;
    }

    /// <summary>Clears all entries in a section.</summary>
    internal void ClearEntries(bool isFooter)
    {
        var stack = isFooter ? _footerStack : _itemsStack;

        if (_navigator.Current is { } current && IsDescendantOf(current, stack))
        {
            _ = _navigator.SetCurrent(null);
        }

        foreach (var child in stack.Children)
        {
            if (child is NavigationViewItem item)
            {
                item.Invoked -= OnItemInvoked;
            }
        }

        stack.Children.Clear();
        Select(null);
    }

    /// <summary>Updates the selected item when a child receives focus externally.</summary>
    internal void NotifyItemFocused(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _ = _navigator.SetCurrent(item);
        Select(item);
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

        if (eventArgs.Phase != Phase.Bubble || eventArgs.Stroke.Action != KeyAction.Press)
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
        }
    }

    private void OnItemInvoked(object? sender, ActivationEventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is NavigationViewItem item)
        {
            _ = _navigator.SetCurrent(item);
            Select(item);
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

    private void CommitCurrent(Control current)
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

        if (SelectedItem is { } previous)
        {
            previous.CommitSelection(false);
        }

        SelectedItem = item;

        item?.CommitSelection(true);

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private List<NavigationViewItem> CollectSelectableItems()
    {
        List<NavigationViewItem> result = [];
        CollectFrom(_itemsStack, result);
        CollectFrom(_footerStack, result);
        return result;
    }

    private List<Control> CollectNavigableEntries()
    {
        List<Control> result = [];
        CollectNavigableFrom(_itemsStack, result);
        CollectNavigableFrom(_footerStack, result);
        return result;
    }

    private static void CollectNavigableFrom(Stack stack, List<Control> result)
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

                    if (sub.EffectiveIsVisible && sub.EffectiveIsEnabled)
                    {
                        result.Add(sub);
                    }
                }
            }
        }
    }

    private static void CollectFrom(Stack stack, List<NavigationViewItem> result)
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

                    if (sub.EffectiveIsVisible && sub.EffectiveIsEnabled)
                    {
                        result.Add(sub);
                    }
                }
            }
        }
    }

    private static bool IsDescendantOf(Control control, Control ancestor)
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
