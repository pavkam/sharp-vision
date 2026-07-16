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
    private readonly HashSet<NavigationViewGroup> _subscribedGroups = [];
    private readonly HashSet<NavigationViewItem> _subscribedItems = [];
    private int _pendingSelectionIndex = -1;

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
    /// <exception cref="ArgumentException">The value contains a terminal control.</exception>
    /// <exception cref="InvalidOperationException">The attached view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The view is disposed.</exception>
    public string? Header
    {
        get;
        set
        {
            ValidateText(value, nameof(value));

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
        SynchronizeEntries();
        EnsureRovingTabStop();
    }

    /// <summary>Removes one identical typed entry from a section without disposing it.</summary>
    internal bool RemoveEntry(Control entry, bool isFooter)
    {
        var stack = isFooter ? _footerStack : _itemsStack;

        if (!stack.Children.Contains(entry))
        {
            return false;
        }

        CaptureSelectionIndex();

        if (!stack.Children.Remove(entry))
        {
            return false;
        }

        SynchronizeEntries();
        RepairSelection();
        return true;
    }

    /// <summary>Clears all retained entries in one section without disposing them.</summary>
    internal void ClearEntries(bool isFooter)
    {
        var stack = isFooter ? _footerStack : _itemsStack;

        if (stack.Children.Count == 0)
        {
            VerifyMutable();
            return;
        }

        CaptureSelectionIndex();
        stack.Children.Clear();
        SynchronizeEntries();
        RepairSelection();
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
            foreach (var item in _subscribedItems)
            {
                item.Invoked -= OnItemInvoked;
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            foreach (var group in _subscribedGroups)
            {
                group.ExpandedChanged -= OnGroupChanged;
                group.StructureChanging -= OnGroupChanging;
                group.StructureChanged -= OnGroupChanged;
            }

            _subscribedItems.Clear();
            _subscribedGroups.Clear();
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

    private List<NavigationViewItem> CollectAllItems()
    {
        List<NavigationViewItem> result = [];
        CollectAllFrom(_itemsStack, result);
        CollectAllFrom(_footerStack, result);
        return result;
    }

    private static void CollectAllFrom(Stack stack, List<NavigationViewItem> result)
    {
        foreach (var child in stack.Children)
        {
            if (child is NavigationViewItem item)
            {
                result.Add(item);
            }
            else if (child is NavigationViewGroup group)
            {
                for (var index = 0; index < group.ItemCount; index++)
                {
                    result.Add(group.ItemAt(index));
                }
            }
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

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Bubble || eventArgs.Stroke.Action != KeyAction.Press)
        {
            return;
        }

        var all = CollectSelectableItems();

        if (all.Count == 0)
        {
            return;
        }

        var current = SelectedItem is { } selected ? all.IndexOf(selected) : -1;
        var next = eventArgs.Stroke.Code == Code.Up
            ? current - 1
            : eventArgs.Stroke.Code == Code.Down
                ? current + 1
                : eventArgs.Stroke.Code == Code.Home
                    ? 0
                    : eventArgs.Stroke.Code == Code.End
                        ? all.Count - 1
                        : -1;

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

    private static void ValidateText(string? value, string name)
    {
        if (value is not null && Terminal.Unicode.Width.Measure(value).Controls > 0)
        {
            throw new ArgumentException("A navigation view header cannot contain terminal controls.", name);
        }
    }

    private void CaptureSelectionIndex()
    {
        _pendingSelectionIndex = SelectedItem is null
            ? -1
            : CollectSelectableItems().IndexOf(SelectedItem);
    }

    private void EnsureRovingTabStop()
    {
        var eligible = CollectSelectableItems();
        var target = SelectedItem is not null && eligible.Contains(SelectedItem)
            ? SelectedItem
            : eligible.FirstOrDefault();

        foreach (var item in CollectAllItems())
        {
            item.IsTabStop = ReferenceEquals(item, target);
        }
    }

    private void OnGroupChanging(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        CaptureSelectionIndex();
    }

    private void OnGroupChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        SynchronizeEntries();
        RepairSelection();
    }

    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is not nameof(IsEnabled) and not nameof(Visibility))
        {
            return;
        }

        _pendingSelectionIndex = sender is NavigationViewItem item
            ? CollectAllItems().IndexOf(item)
            : -1;
        RepairSelection();
    }

    private void RepairSelection()
    {
        if (SelectedItem is null)
        {
            EnsureRovingTabStop();
            _pendingSelectionIndex = -1;
            return;
        }

        var eligible = CollectSelectableItems();

        if (eligible.Contains(SelectedItem))
        {
            EnsureRovingTabStop();
            _pendingSelectionIndex = -1;
            return;
        }

        var focused = SelectedItem.IsFocused;
        NavigationViewItem? replacement = null;
        var all = CollectAllItems();
        var position = all.IndexOf(SelectedItem);

        if (position >= 0)
        {
            for (var index = position + 1; index < all.Count && replacement is null; index++)
            {
                replacement = eligible.Contains(all[index]) ? all[index] : null;
            }

            for (var index = position - 1; index >= 0 && replacement is null; index--)
            {
                replacement = eligible.Contains(all[index]) ? all[index] : null;
            }
        }
        else if (eligible.Count > 0)
        {
            replacement = eligible[Math.Clamp(_pendingSelectionIndex, 0, eligible.Count - 1)];
        }

        Select(replacement);

        if (focused && replacement is not null)
        {
            _ = FocusOwner?.Focus(replacement);
        }

        _pendingSelectionIndex = -1;
    }

    private void SynchronizeEntries()
    {
        var items = CollectAllItems();
        var currentItems = new HashSet<NavigationViewItem>(items, ReferenceEqualityComparer.Instance);
        var groups = new HashSet<NavigationViewGroup>(ReferenceEqualityComparer.Instance);

        foreach (var child in _itemsStack.Children.Concat(_footerStack.Children))
        {
            if (child is NavigationViewGroup group)
            {
                _ = groups.Add(group);
            }
        }

        foreach (var item in _subscribedItems.Except(currentItems).ToArray())
        {
            item.Invoked -= OnItemInvoked;
            item.PropertyChanged -= OnItemPropertyChanged;
            _ = _subscribedItems.Remove(item);
        }

        foreach (var item in currentItems.Except(_subscribedItems))
        {
            item.Invoked += OnItemInvoked;
            item.PropertyChanged += OnItemPropertyChanged;
            _ = _subscribedItems.Add(item);
        }

        foreach (var group in _subscribedGroups.Except(groups).ToArray())
        {
            group.ExpandedChanged -= OnGroupChanged;
            group.StructureChanging -= OnGroupChanging;
            group.StructureChanged -= OnGroupChanged;
            _ = _subscribedGroups.Remove(group);
        }

        foreach (var group in groups.Except(_subscribedGroups))
        {
            group.ExpandedChanged += OnGroupChanged;
            group.StructureChanging += OnGroupChanging;
            group.StructureChanged += OnGroupChanged;
            _ = _subscribedGroups.Add(group);
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
            previous.IsTabStop = false;
        }

        SelectedItem = item;

        item?.CommitSelection(true);

        EnsureRovingTabStop();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
