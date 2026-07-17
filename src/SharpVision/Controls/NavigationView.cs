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

    /// <summary>Initializes a navigation view with an empty item collection.</summary>
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

        var root = new Dock();
        Dock.SetSide(_headerText, Side.Top);
        root.Children.Add(_headerText);
        Dock.SetSide(_footerStack, Side.Bottom);
        root.Children.Add(_footerStack);
        root.Children.Add(_itemsStack);

        InitializeContent(root);
        Items = new NavigationViewItems(this, isFooter: false);
        FooterItems = new NavigationViewItems(this, isFooter: true);
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

        if (!stack.Children.Remove(entry))
        {
            return false;
        }

        if (entry is NavigationViewItem item)
        {
            item.Invoked -= OnItemInvoked;

            if (ReferenceEquals(SelectedItem, item))
            {
                Select(null);
            }
        }

        return true;
    }

    /// <summary>Clears all entries in a section.</summary>
    internal void ClearEntries(bool isFooter)
    {
        var stack = isFooter ? _footerStack : _itemsStack;

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
        Select(item);
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
            if (SelectedItem is not null)
            {
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

        var all = CollectSelectableItems();
        var current = SelectedItem is { } selected ? all.IndexOf(selected) : -1;
        var next = current + direction;

        if (next >= 0 && next < all.Count)
        {
            Select(all[next]);
            _ = _itemsStack.BringIntoView(all[next]);
            eventArgs.Handled = true;
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
            Select(item);
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
}
