// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using SharpVision.Controls.Layout;
using SharpVision.Terminal.Input;

using DisplayText = Controls.Display.Text;
using LayoutStack = Controls.Layout.Stack;

/// <summary>Provides a sidebar navigation control with typed items, groups, header, and footer.</summary>
[PublicAPI]
public sealed class NavigationView: ScrollableCompositeControlBase
{
    private readonly LayoutStack _itemsStack;
    private readonly LayoutStack _footerStack;
    private readonly DisplayText _headerText;
    private readonly CurrentItemNavigator _navigator;

    // Shared, never-mutated stand-in for the navigable-entry snapshot on a keystroke that
    // HandleCurrentItemNavigation recognizes but never reads the collection for (Up/Down), so that
    // path allocates nothing beyond what CurrentItemNavigator.Move already needs internally.
    private static readonly List<ControlBase> _noNavigableEntries = [];
    private readonly RetainedPropertyOverrideService _itemPropertyOverrides;
    private readonly RetainedPropertyOverrideService _footerPropertyOverrides;
    private long _selectionVersion;
    private ControlBase? _trackedCurrent;
    private Rect _trackedCurrentLogicalBounds;

    /// <summary>The selected item's last committed position in the complete semantic item order.
    /// Ordinary unavailability repairs locate the retained item in that live order. This snapshot
    /// is reserved for an unexpected child-initiated detachment where the identity has already left
    /// the tree; committed host changes refresh it while the selection remains attached.</summary>
    private int _selectedIndex = -1;

    /// <summary>Gets the retained authored-presentation count used to prove metadata retires with ownership.</summary>
    internal int RequestedPresentationCount => _itemPropertyOverrides.Count + _footerPropertyOverrides.Count;

    /// <summary>Gets the private footer offset used to prove bounded footer exposure.</summary>
    internal int FooterVerticalOffset => _footerStack.VerticalOffset;

    /// <summary>Scrolls minimally to expose one owned entry, without requiring the caller to know
    /// about the private realized visual tree.</summary>
    /// <param name="item">The non-null owned navigation entry.</param>
    /// <returns>True when at least one offset changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item is not owned by this navigation view.</exception>
    /// <exception cref="InvalidOperationException">The attached navigation view is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The navigation view is disposed.</exception>
    public bool BringItemIntoView(NavigationViewItem item)
    {
        VerifyMutable();
        ArgumentNullException.ThrowIfNull(item);

        if (!ReferenceEquals(item.FindNavigationView(), this))
        {
            throw new ArgumentException("The item is not owned by this navigation view.", nameof(item));
        }

        var mainX = _itemsStack.HorizontalOffset;
        var mainY = _itemsStack.VerticalOffset;
        var footerX = _footerStack.HorizontalOffset;
        var footerY = _footerStack.VerticalOffset;
        _ = RevealEntry(item);
        return mainX != _itemsStack.HorizontalOffset ||
               mainY != _itemsStack.VerticalOffset ||
               footerX != _footerStack.HorizontalOffset ||
               footerY != _footerStack.VerticalOffset;
    }

    /// <summary>Initializes a quiet square navigation background with an empty item collection.</summary>
    public NavigationView()
    {
        EnableChromeAuthoring();
        _headerText = new DisplayText(string.Empty)
        {
            UseMnemonic = true,
            Visibility = Visibility.Collapsed,
        };

        _footerStack = new LayoutStack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never
        };
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
        _itemPropertyOverrides = new RetainedPropertyOverrideService(this, _itemsStack.Children.OwnedSlot);
        _footerPropertyOverrides = new RetainedPropertyOverrideService(this, _footerStack.Children.OwnedSlot);
        _itemsStack.BoundsChanged += OnNavigationHostBoundsChanged;
        _footerStack.BoundsChanged += OnNavigationHostBoundsChanged;

        InitializeContent(root);
        InitializeScrollableContent(_itemsStack);
        Items = new NavigationViewEntryCollection(this, isFooter: false);
        FooterItems = new NavigationViewEntryCollection(this, isFooter: true);
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.None;
        _ = AddHandler(Events.Key, OnKeyRouted);
        _ = AddHandler(Events.Pointer, OnPointerRouted);
    }

    /// <summary>Raised after the selected item changes.</summary>
    public event EventHandler<NavigationViewSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Gets or sets an optional bold header title.</summary>
    /// <exception cref="InvalidOperationException">The attached view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The view is disposed.</exception>
    public string? Header
    {
        get;
        set
        {
            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                InvalidationImpact.Measure,
                () =>
                {
                    _headerText.Content = string.IsNullOrEmpty(Header)
                        ? string.Empty
                        : $"<b>{DisplayText.Escape(Header)}</b>";
                    _headerText.Visibility = string.IsNullOrEmpty(Header) ? Visibility.Collapsed : Visibility.Visible;
                });
        }
    }

    /// <inheritdoc/>
    protected override string? AccessKeyText => Header;

    /// <inheritdoc/>
    internal override bool AddSelectableTextChildren(List<ControlBase> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        children.Add(_headerText);
        children.Add(_itemsStack);
        children.Add(_footerStack);
        return true;
    }

    /// <summary>Gets the typed main item collection.</summary>
    public NavigationViewEntryCollection Items { get; }

    /// <summary>Gets the typed footer item collection.</summary>
    public NavigationViewEntryCollection FooterItems { get; }

    /// <summary>Gets or sets whether Up and Down wrap across the first and last available entries.</summary>
    /// <exception cref="InvalidOperationException">The attached navigation view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The navigation view is disposed.</exception>
    public bool WrapNavigation
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
    }

    /// <summary>Gets the currently selected item, or null.</summary>
    public NavigationViewItem? SelectedItem { get; private set; }

    /// <summary>Selects a currently owned navigation item without moving keyboard focus.</summary>
    /// <param name="item">The non-null item owned by this navigation view.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="item"/> is not owned by this navigation view.</exception>
    /// <exception cref="InvalidOperationException">
    /// The attached view is mutated off-dispatcher, or <paramref name="item"/> is unavailable.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The view is disposed.</exception>
    public void SelectItem(NavigationViewItem item)
    {
        VerifyMutable();
        ArgumentNullException.ThrowIfNull(item);

        if (!ReferenceEquals(item.FindNavigationView(), this))
        {
            throw new ArgumentException("The item is not owned by this navigation view.", nameof(item));
        }

        if (!IsAvailable(item))
        {
            throw new InvalidOperationException("An unavailable navigation item cannot be selected.");
        }

        _ = SetCurrent(item);
        Select(item, ActivationCause.Programmatic);
    }

    /// <summary>Verifies the owner before a public collection validates candidate-specific state.</summary>
    internal void VerifyMutation() => VerifyMutable();

    /// <summary>Gets the item count for one section.</summary>
    internal int GetItemCount(bool isFooter) =>
        (isFooter ? _footerStack : _itemsStack).Children.Count;

    [Pure]
    private RetainedPropertyOverrideService PropertyOverrides(bool isFooter) =>
        isFooter ? _footerPropertyOverrides : _itemPropertyOverrides;

    /// <summary>Gets one item by index in a section.</summary>
    internal ControlBase GetItem(int index, bool isFooter) =>
        (isFooter ? _footerStack : _itemsStack).Children[index];

    /// <summary>Adds one typed entry to a section.</summary>
    internal void AddEntry(ControlBase entry, bool isFooter) => InsertEntry(GetItemCount(isFooter), entry, isFooter);

    /// <summary>Inserts one typed entry at a position in a section.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    internal void InsertEntry(int index, ControlBase entry, bool isFooter)
    {
        VerifyMutable();
        ValidateEntry(entry);
        var stack = isFooter ? _footerStack : _itemsStack;

        // Ownership is secured before any authored property is captured or
        // overwritten. A rejected insertion must leave the caller's object
        // exactly as it found it.
        stack.Children.Insert(index, entry);
        var lease = PropertyOverrides(isFooter).Acquire(
            entry,
            RetainedPropertyOverrides.IsFocusable,
            RetainedPropertyOverrides.IsTabStop);
        ConfigureEntry(stack, entry, lease);
    }

    /// <summary>Removes one typed entry from a section.</summary>
    internal bool RemoveEntry(ControlBase entry, bool isFooter) =>
        RemoveEntryCore(entry, isFooter, restorePresentation: true);

    private bool RemoveEntryCore(ControlBase entry, bool isFooter, bool restorePresentation)
    {
        VerifyMutable();
        ArgumentNullException.ThrowIfNull(entry);
        var stack = isFooter ? _footerStack : _itemsStack;

        if (!stack.Children.Contains(entry))
        {
            return false;
        }

        var repair = PrepareRemoval(entry);
        var propertyOverrides = PropertyOverrides(isFooter);
        var lease = propertyOverrides.Get(entry);
        _ = stack.Children.Remove(entry);

        UnsubscribeEntry(entry);
        CompleteRemoval(repair);

        if (restorePresentation)
        {
            propertyOverrides.Restore(lease);
        }
        else
        {
            propertyOverrides.Retire(lease);
        }

        return true;
    }

    /// <summary>Removes the owned entry at a position in a section.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current entries.</exception>
    internal void RemoveEntryAt(int index, bool isFooter)
    {
        VerifyMutable();
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
        VerifyMutable();
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

        stack.Children.Move(oldIndex, newIndex);
    }

    /// <summary>Gets the position of one entry within a section, or -1 when not owned there.</summary>
    internal int IndexOfEntry(ControlBase entry, bool isFooter) =>
        (isFooter ? _footerStack : _itemsStack).Children.IndexOf(entry);

    /// <summary>Replaces the owned entry at a position in a section, preserving position.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current entries.</exception>
    internal void ReplaceEntryAt(int index, ControlBase entry, bool isFooter)
    {
        VerifyMutable();
        ValidateEntry(entry);
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
        var oldLease = PropertyOverrides(isFooter).Get(old);

        stack.Children[index] = entry;

        UnsubscribeEntry(old);
        var lease = PropertyOverrides(isFooter).Acquire(
            entry,
            RetainedPropertyOverrides.IsFocusable,
            RetainedPropertyOverrides.IsTabStop);
        ConfigureEntry(stack, entry, lease);
        CompleteRemoval(repair);
        PropertyOverrides(isFooter).Restore(oldLease);
    }

    /// <summary>Detaches a top-level semantic entry before direct disposal publication begins.</summary>
    /// <param name="entry">The owned entry whose caller requested disposal.</param>
    internal void RemoveEntryForDisposal(ControlBase entry)
    {
        if (!RemoveEntryCore(entry, isFooter: false, restorePresentation: false))
        {
            _ = RemoveEntryCore(entry, isFooter: true, restorePresentation: false);
        }
    }

    // Repairs state only when a disposing entry initiated its own semantic removal. Ordinary API
    // removals run their position-aware repair after commit; the removed snapshot distinguishes
    // those paths without component-local mutation flags.
    private void OnEntryHostChanged(OwnedControlChange change)
    {
        if (!ContainsDisposingControl(change.Removed.Span))
        {
            return;
        }

        // This notification publishes from inside DisposeCore, which removes the item from
        // its owning slot before IsDisposed itself flips true — IsDisposing is what's already
        // set at this point.
        var currentRemoved = _navigator.Current is { IsDisposing: true } or { IsDisposed: true };

        if (currentRemoved)
        {
            _ = SetCurrent(null);
        }

        if (SelectedItem is { IsDisposing: true } or { IsDisposed: true })
        {
            RepairSelectionAndCurrent(FindAvailableAtSemanticIndex(_selectedIndex), currentRemoved);
        }
        else if (SelectedItem is { } selected)
        {
            _selectedIndex = CollectSemanticItems().IndexOf(selected);
        }
    }

    [Pure]
    private static bool ContainsDisposingControl(ReadOnlySpan<ControlBase> controls)
    {
        foreach (var control in controls)
        {
            if (control.IsDisposing || control.IsDisposed)
            {
                return true;
            }
        }

        return false;
    }

    // Captures whether the current-navigation or selected item is the root
    // being removed or one of its descendants, before detachment makes the
    // ancestor walk impossible. A group counts as its own root, so removing
    // an entire group (or clearing one) repairs a selected descendant the
    // same way removing that descendant directly would — this is also the
    // seam NavigationViewGroup uses to repair selection for removals that
    // never pass through RemoveEntry/ClearEntries at all.
    [Pure]
    internal NavigationViewRemovalRepair PrepareRemoval(ControlBase root)
    {
        var currentRemoved = _navigator.Current is { } current &&
                             (ReferenceEquals(current, root) || IsDescendantOf(current, root));
        var selectedRemoved = SelectedItem is { } selected &&
                              (ReferenceEquals(selected, root) || IsDescendantOf(selected, root));
        var selectedIndex = selectedRemoved ? CollectSemanticItems().IndexOf(SelectedItem!) : -1;

        return new NavigationViewRemovalRepair(currentRemoved, selectedRemoved, selectedIndex);
    }

    /// <summary>Captures repair state for descendants leaving a root that remains owned.</summary>
    [Pure]
    internal NavigationViewRemovalRepair PrepareDescendantRemoval(ControlBase root)
    {
        var currentRemoved = _navigator.Current is { } current && IsDescendantOf(current, root);
        var selectedRemoved = SelectedItem is { } selected && IsDescendantOf(selected, root);
        var selectedIndex = selectedRemoved ? CollectSemanticItems().IndexOf(SelectedItem!) : -1;
        return new NavigationViewRemovalRepair(currentRemoved, selectedRemoved, selectedIndex);
    }

    // Runs after detachment, using state captured by PrepareRemoval before
    // the removed subtree left the tree.
    internal void CompleteRemoval(NavigationViewRemovalRepair repair)
    {
        if (repair.IsCurrentRemoved)
        {
            _ = SetCurrent(null);
        }

        if (repair.IsSelectedRemoved)
        {
            RepairSelectionAndCurrent(FindAvailableAtSemanticIndex(repair.SelectedIndex), repair.IsCurrentRemoved);
        }
    }

    // A repaired selection that was also the keyboard-current entry becomes current itself, exactly
    // as the hidden-selection repair already does. Leaving current null here made the next arrow
    // key start from a section endpoint - Down jumped from the repaired selection to the very
    // first entry - instead of stepping relative to the entry the user can see is selected.
    private void RepairSelectionAndCurrent(NavigationViewItem? replacement, bool currentRemoved)
    {
        if (currentRemoved && replacement is not null)
        {
            _ = SetCurrent(replacement);
        }

        Select(replacement, ActivationCause.Programmatic);
    }

    private void ConfigureEntry(
        LayoutStack stack,
        ControlBase entry,
        RetainedPropertyOverrideLease lease)
    {
        lease.SetLive(RetainedControlProperty.IsFocusable, false);

        if (!IsCommitted(stack, entry, lease))
        {
            return;
        }

        lease.SetLive(RetainedControlProperty.IsTabStop, false);

        if (!IsCommitted(stack, entry, lease))
        {
            return;
        }

        if (entry is NavigationViewItem item)
        {
            item.Invoked += OnItemInvoked;
        }
        else if (entry is NavigationViewGroup group)
        {
            group.VisibilityChanged += OnGroupVisibilityChanged;
        }
    }

    [Pure]
    private static bool IsCommitted(
        LayoutStack stack,
        ControlBase entry,
        RetainedPropertyOverrideLease lease) =>
        stack.Children.Contains(entry) &&
        lease.IsCurrent;

    private void UnsubscribeEntry(ControlBase entry)
    {
        if (entry is NavigationViewItem item)
        {
            item.Invoked -= OnItemInvoked;
        }
        else if (entry is NavigationViewGroup group)
        {
            group.VisibilityChanged -= OnGroupVisibilityChanged;
        }
    }

    /// <summary>Clears all entries in a section.</summary>
    internal void ClearEntries(bool isFooter)
    {
        VerifyMutable();
        var stack = isFooter ? _footerStack : _itemsStack;
        var repair = PrepareRemoval(stack);
        var entries = stack.Children.ToArray();
        var leases = entries.Select(PropertyOverrides(isFooter).Get).ToArray();
        stack.Children.Clear();

        foreach (var child in entries)
        {
            UnsubscribeEntry(child);
        }

        CompleteRemoval(repair);

        foreach (var lease in leases)
        {
            PropertyOverrides(isFooter).Restore(lease);
        }
    }

    /// <summary>Updates the selected item when a child receives focus externally.</summary>
    internal void NotifyItemFocused(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _ = SetCurrent(item);
        Select(item, ActivationCause.Programmatic);
    }

    /// <summary>Commits an item activated by a grouped child through the owning view, threading the
    /// originating activation cause.</summary>
    /// <param name="item">The non-null item owned by this navigation view.</param>
    /// <param name="cause">The defined activation cause that invoked the item.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    internal void NotifyItemInvoked(NavigationViewItem item, ActivationCause cause)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (ReferenceEquals(item.FindNavigationView(), this))
        {
            _ = SetCurrent(item);
            Select(item, cause);
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

        if (!Focus() || !ReferenceEquals(item.FindNavigationView(), this))
        {
            return false;
        }

        _ = SetCurrent(item);

        if (!ReferenceEquals(item.FindNavigationView(), this))
        {
            return false;
        }

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

        if (!Focus() || !ReferenceEquals(group.FindNavigationView(), this))
        {
            return false;
        }

        NotifyGroupInvoked(group);

        if (!ReferenceEquals(group.FindNavigationView(), this))
        {
            return false;
        }

        group.IsExpanded = !group.IsExpanded;
        return true;
    }

    /// <summary>Commits one pointer-targeted group as the current keyboard entry.</summary>
    /// <param name="group">The non-null owned group.</param>
    internal void NotifyGroupInvoked(NavigationViewGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        _ = SetCurrent(group);
    }

    /// <summary>Repairs selection after a retained group's own visibility changes, or after it
    /// collapses and hides its descendants.</summary>
    /// <param name="group">The non-null owned group whose visibility or expansion changed.</param>
    internal void NotifyGroupVisibilityChanged(NavigationViewGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // Parking current on the group itself keeps it pointed at something still visible when a
        // descendant disappears because the group collapsed - the group's own visibility is
        // untouched by IsExpanded. A direct Visibility change on the group can take the group itself
        // out of view too, so parking there would leave current on an invisible entry; null lets
        // navigation fall back to the same first-navigable-entry recovery ActivateCurrent already
        // performs for a null current.
        if (_navigator.Current is { } current &&
            (ReferenceEquals(current, group) || IsDescendantOf(current, group)))
        {
            _ = SetCurrent(IsAvailable(group) ? group : null);
        }

        if (SelectedItem is null || IsAvailable(SelectedItem))
        {
            return;
        }

        Select(FindAvailableAdjacentTo(SelectedItem), ActivationCause.Programmatic);
    }

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Bubble || !eventArgs.IsKeyDown)
        {
            return;
        }

        if (eventArgs.IsInitialKeyDown &&
            (eventArgs.Stroke.Code == Code.Enter ||
             (eventArgs.Stroke.Code == Code.Character && eventArgs.Stroke.Character == new Rune(' '))))
        {
            if (!eventArgs.Stroke.Modifiers.IsActivationEligible())
            {
                return;
            }

            eventArgs.IsHandled = ActivateCurrent();
            return;
        }

        if (!KeyboardModifierPolicy.IsScalarNavigationEligible(eventArgs.Stroke.Modifiers))
        {
            return;
        }

        // Home, End, PageUp, PageDown, Up, and Down share the current-item keyboard skeleton;
        // building the navigable-entry snapshot only when one of those codes needs it keeps Up/Down
        // from paying for a second snapshot on top of the one CurrentItemNavigator.Move already
        // takes internally.
        var code = eventArgs.Stroke.Code;

        var entries = code is Code.Home or Code.End or Code.PageUp or Code.PageDown
            ? CollectNavigableEntries()
            : _noNavigableEntries;

        if (HandleCurrentItemNavigation(
                eventArgs,
                _navigator,
                entries,
                Viewport.Height,
                PageOverlap,
                index => entries[index].Bounds.Height,
                WrapNavigation,
                (target, _) => CommitCurrent(target)))
        {
            return;
        }

        if (code == Code.Left)
        {
            eventArgs.IsHandled = HandleLeft();
            return;
        }

        if (code == Code.Right)
        {
            eventArgs.IsHandled = HandleRight();
        }
    }

    private bool HandleLeft()
    {
        if (_navigator.Current is NavigationViewGroup { IsExpanded: true } group)
        {
            group.IsExpanded = false;
            return true;
        }

        if (_navigator.Current is NavigationViewItem item && FindOwningGroup(item) is { } owner)
        {
            _ = SetCurrent(owner);
            return true;
        }

        return false;
    }

    private bool HandleRight()
    {
        if (_navigator.Current is not NavigationViewGroup group)
        {
            return false;
        }

        if (!group.IsExpanded)
        {
            group.IsExpanded = true;
            return true;
        }

        for (var index = 0; index < group.ItemCount; index++)
        {
            var item = group.ItemAt(index);

            if (!IsAvailable(item))
            {
                continue;
            }

            MoveCurrent(item);
            return true;
        }

        return false;
    }

    private void OnPointerRouted(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Preview || eventArgs.IsHandled)
        {
            return;
        }

        eventArgs.IsHandled = HandleScrollWheel(eventArgs);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            foreach (var group in _itemsStack.Children.Concat(_footerStack.Children).OfType<NavigationViewGroup>())
            {
                group.RetirePresentationMetadataForOwnerDisposal();
            }

            _itemPropertyOverrides.Dispose();
            _footerPropertyOverrides.Dispose();
            SelectionChanged = null;
            _ = SetCurrent(null);
            Select(null, ActivationCause.Programmatic);
        }
    }

    private void OnItemInvoked(object? sender, ActivationEventArgs eventArgs)
    {
        if (sender is NavigationViewItem item)
        {
            NotifyItemInvoked(item, eventArgs.Cause);
        }
    }

    // Reacts only to the group's own Visibility setter running, never to IsExpanded flipping the
    // group's internal stack - the two never fire for the same transition, so this never
    // double-repairs alongside the IsExpanded-driven call already routed through the same method.
    private void OnGroupVisibilityChanged(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is NavigationViewGroup group)
        {
            NotifyGroupVisibilityChanged(group);
        }
    }

    private bool ActivateCurrent()
    {
        if (_navigator.Current is { } stale && !IsAvailable(stale))
        {
            _ = SetCurrent(null);
        }

        if (_navigator.Current is null)
        {
            var entries = CollectNavigableEntries();

            if (entries.Count == 0)
            {
                return false;
            }

            _ = SetCurrent(entries[0]);
        }

        if (_navigator.Current is NavigationViewGroup group && IsAvailable(group))
        {
            group.IsExpanded = !group.IsExpanded;
            return true;
        }

        if (_navigator.Current is NavigationViewItem item && IsAvailable(item))
        {
            item.ActivateFromOwner(ActivationCause.Keyboard);
            return true;
        }

        return false;
    }

    // Right entering a group's first available child moves current and commits it in one step, the
    // same way HandleCurrentItemNavigation's own commit callback does for Home, End, PageUp,
    // PageDown, and Up/Down. Moving through SetCurrent and then committing separately would track -
    // and so reveal - the same entry twice in one dispatch; the second reveal is pure redundancy,
    // since nothing between the two can change where the entry sits.
    private void MoveCurrent(ControlBase target)
    {
        _ = _navigator.SetCurrent(target);
        CommitCurrent(target);
    }

    // Every caller reaches this from OnKeyRouted, so the committed selection always
    // originates from a keyboard-driven navigation stroke.
    private void CommitCurrent(ControlBase current)
    {
        if (current is NavigationViewItem item)
        {
            Select(item, ActivationCause.Keyboard);
        }

        TrackCurrent(current);
    }

    private bool SetCurrent(ControlBase? current)
    {
        var changed = _navigator.SetCurrent(current);
        TrackCurrent(current);
        return changed;
    }

    private void TrackCurrent(ControlBase? current)
    {
        if (!ReferenceEquals(_navigator.Current, current))
        {
            return;
        }

        if (!ReferenceEquals(_trackedCurrent, current))
        {
            _trackedCurrent?.BoundsChanged -= OnCurrentBoundsChanged;

            _trackedCurrent = current;
            _trackedCurrentLogicalBounds = current is null ? default : GetLogicalBounds(current);

            _trackedCurrent?.BoundsChanged += OnCurrentBoundsChanged;
        }

        if (current is not null && !current.IsDisposed && !current.IsDisposing)
        {
            _ = RevealEntry(current);
        }
    }

    private bool RevealEntry(ControlBase entry)
    {
        return IsDescendantOf(entry, _itemsStack)
            ? _itemsStack.BringIntoView(entry)
            : IsDescendantOf(entry, _footerStack) && _footerStack.BringIntoView(entry);
    }

    private Rect GetLogicalBounds(ControlBase entry)
    {
        var bounds = entry.Bounds;
        var stack = IsDescendantOf(entry, _itemsStack) ? _itemsStack : _footerStack;
        return new Rect(
            bounds.X.Add(stack.HorizontalOffset),
            bounds.Y.Add(stack.VerticalOffset),
            bounds.Width,
            bounds.Height);
    }

    private void OnCurrentBoundsChanged(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is ControlBase current && ReferenceEquals(current, _navigator.Current))
        {
            var logicalBounds = GetLogicalBounds(current);

            // Scrolling translates arranged child bounds by the inverse offset. The logical
            // bounds stay unchanged in that case, so revealing the current entry would merely
            // undo an intentional wheel, scrollbar, or programmatic scroll.
            if (logicalBounds == _trackedCurrentLogicalBounds)
            {
                return;
            }

            _trackedCurrentLogicalBounds = logicalBounds;
            _ = RevealEntry(current);
        }
    }

    private void OnNavigationHostBoundsChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (_navigator.Current is { IsDisposed: false, IsDisposing: false } current)
        {
            _ = RevealEntry(current);
        }
    }

    private void Select(NavigationViewItem? item, ActivationCause cause)
    {
        if (ReferenceEquals(SelectedItem, item))
        {
            return;
        }

        var version = ++_selectionVersion;
        var previous = SelectedItem;

        if (previous is { IsDisposed: false })
        {
            previous.CommitSelection(false);

            if (_selectionVersion != version)
            {
                return;
            }
        }

        previous?.PropertyChanged -= OnSelectedItemAvailabilityChanged;

        SelectedItem = item;
        _selectedIndex = item is null ? -1 : CollectSemanticItems().IndexOf(item);

        item?.PropertyChanged += OnSelectedItemAvailabilityChanged;
        item?.CommitSelection(true);

        if (_selectionVersion != version)
        {
            return;
        }

        NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);

        if (_selectionVersion != version || !ReferenceEquals(SelectedItem, item))
        {
            return;
        }

        SelectionChanged?.Invoke(this, new NavigationViewSelectionChangedEventArgs(previous, item, cause));
    }

    // Reacts only to the selected item's own Visibility setter running - group collapse instead
    // flips the group's internal stack's Visibility, so this handler and NotifyGroupVisibilityChanged
    // never both fire for the same collapse.
    private void OnSelectedItemAvailabilityChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName is not nameof(EffectiveIsVisible) and
            not nameof(EffectiveIsEnabled))
        {
            return;
        }

        if (SelectedItem is null)
        {
            return;
        }

        if (IsAvailable(SelectedItem))
        {
            _selectedIndex = CollectSemanticItems().IndexOf(SelectedItem);
            return;
        }

        if (eventArgs.PropertyName == nameof(EffectiveIsEnabled))
        {
            if (ReferenceEquals(_navigator.Current, SelectedItem))
            {
                _ = SetCurrent(null);
            }

            return;
        }

        var replacement = FindAvailableAdjacentTo(SelectedItem);
        _ = SetCurrent(replacement);
        Select(replacement, ActivationCause.Programmatic);
    }

    [Pure]
    private List<NavigationViewItem> CollectSemanticItems()
    {
        List<NavigationViewItem> result = [];
        CollectSemanticFrom(_itemsStack, result);
        CollectSemanticFrom(_footerStack, result);
        return result;
    }

    [Pure]
    private NavigationViewItem? FindAvailableAdjacentTo(NavigationViewItem selected)
    {
        var entries = CollectSemanticItems();
        var selectedIndex = entries.IndexOf(selected);

        for (var index = selectedIndex + 1; index < entries.Count; index++)
        {
            if (IsAvailable(entries[index]))
            {
                return entries[index];
            }
        }

        for (var index = selectedIndex - 1; index >= 0; index--)
        {
            if (IsAvailable(entries[index]))
            {
                return entries[index];
            }
        }

        return null;
    }

    [Pure]
    private NavigationViewItem? FindAvailableAtSemanticIndex(int selectedIndex)
    {
        var entries = CollectSemanticItems();

        for (var index = Math.Max(0, selectedIndex); index < entries.Count; index++)
        {
            if (IsAvailable(entries[index]))
            {
                return entries[index];
            }
        }

        for (var index = Math.Min(selectedIndex - 1, entries.Count - 1); index >= 0; index--)
        {
            if (IsAvailable(entries[index]))
            {
                return entries[index];
            }
        }

        return null;
    }

    [Pure]
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

    private static void CollectSemanticFrom(LayoutStack stack, List<NavigationViewItem> result)
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

    [Pure]
    private NavigationViewGroup? FindOwningGroup(NavigationViewItem item)
    {
        foreach (var group in _itemsStack.Children.Concat(_footerStack.Children).OfType<NavigationViewGroup>())
        {
            for (var index = 0; index < group.ItemCount; index++)
            {
                if (ReferenceEquals(group.ItemAt(index), item))
                {
                    return group;
                }
            }
        }

        return null;
    }

    [Pure]
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

    private static void ValidateEntry(ControlBase entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry is not NavigationViewItem and not NavigationViewGroup and not NavigationViewSeparator)
        {
            throw new ArgumentException(
                "A navigation entry must be an item, group, or separator.",
                nameof(entry));
        }
    }

    [Pure]
    private bool IsAvailable(ControlBase entry) =>
        !entry.IsDisposed &&
        entry.EffectiveIsVisible &&
        entry.EffectiveIsEnabled &&
        (entry is NavigationViewItem item
            ? ReferenceEquals(item.FindNavigationView(), this)
            : entry is NavigationViewGroup group && ReferenceEquals(group.FindNavigationView(), this));
}
