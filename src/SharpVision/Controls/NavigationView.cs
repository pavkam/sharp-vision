// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Input;

/// <summary>Provides a retained sidebar navigation control with typed items, groups, header, and footer.</summary>
public sealed class NavigationView: CompositeControl
{
    private readonly Stack _footerStack;
    private readonly Text _headerText;
    private readonly Stack _itemsStack;

    /// <summary>Initializes a navigation view with empty main and footer collections.</summary>
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
        TabNavigation = TabNavigation.Cycle;
        _ = AddHandler(Events.Key, OnKeyRouted);
    }

    /// <summary>Raised after the selected item identity and visual state commit.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Gets or sets an optional header title hidden when null or empty.</summary>
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

    /// <summary>Gets the typed retained main item collection.</summary>
    public NavigationViewItems Items { get; }

    /// <summary>Gets the typed retained pinned-footer item collection.</summary>
    public NavigationViewItems FooterItems { get; }

    /// <summary>Gets the currently selected item, or null.</summary>
    public NavigationViewItem? SelectedItem { get; private set; }

    /// <summary>Gets the main section's current vertical scroll offset.</summary>
    public int VerticalOffset => _itemsStack.VerticalOffset;

    /// <summary>Gets the item count for one section.</summary>
    /// <param name="isFooter">Whether to address the footer section.</param>
    internal int GetItemCount(bool isFooter) =>
        (isFooter ? _footerStack : _itemsStack).Children.Count;

    /// <summary>Gets one item by index in a section.</summary>
    /// <param name="index">The valid zero-based section index.</param>
    /// <param name="isFooter">Whether to address the footer section.</param>
    internal Control GetItem(int index, bool isFooter) =>
        (isFooter ? _footerStack : _itemsStack).Children[index];

    /// <summary>Adds one validated typed entry to a section.</summary>
    internal void AddEntry(Control entry, bool isFooter)
    {
        Debug.Assert(
            entry is NavigationViewItem or NavigationViewGroup or NavigationViewSeparator,
            "Navigation view entries are constrained by typed collection overloads.");
        var stack = isFooter ? _footerStack : _itemsStack;
        stack.Children.Add(entry);

        if (entry is NavigationViewItem item)
        {
            item.IsTabStop = false;
            item.Invoked += OnItemInvoked;
        }
    }

    /// <summary>Removes one identical typed entry from a section without disposing it.</summary>
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

    /// <summary>Clears all retained entries in one section without disposing them.</summary>
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

    /// <summary>Updates selection when an owned item receives focus externally.</summary>
    internal void NotifyItemFocused(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Select(item);
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

    private List<NavigationViewItem> CollectSelectableItems()
    {
        List<NavigationViewItem> result = [];
        CollectFrom(_itemsStack, result);
        CollectFrom(_footerStack, result);
        return result;
    }

    private void OnItemInvoked(object? sender, ActivationEventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is NavigationViewItem item)
        {
            Select(item);
        }
    }

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Bubble || eventArgs.Stroke.Action != KeyAction.Press)
        {
            return;
        }

        var direction = eventArgs.Stroke.Code == Code.Up
            ? -1
            : eventArgs.Stroke.Code == Code.Down
                ? 1
                : 0;

        if (direction == 0)
        {
            return;
        }

        var all = CollectSelectableItems();
        var current = SelectedItem is { } selected ? all.IndexOf(selected) : -1;
        var next = current + direction;

        if (next >= 0 && next < all.Count)
        {
            Select(all[next]);
            _ = FocusOwner?.Focus(all[next]);
            _ = BringIntoView(all[next]);
            eventArgs.Handled = true;
        }
    }

    private bool BringIntoView(NavigationViewItem item) =>
        IsDescendantOf(item, _itemsStack) ? _itemsStack.BringIntoView(item) : _footerStack.BringIntoView(item);

    private static bool IsDescendantOf(Control item, Control ancestor)
    {
        for (var current = item.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
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
            previous.IsTabStop = false;
        }

        SelectedItem = item;

        if (item is not null)
        {
            item.CommitSelection(true);
            item.IsTabStop = true;
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
