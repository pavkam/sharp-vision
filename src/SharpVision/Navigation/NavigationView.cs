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
    private bool _isWritingEntryFocusPolicy;
    private long _presentationVersion;
    private long _selectionVersion;
    private ControlBase? _trackedCurrent;
    private Rect _trackedCurrentLogicalBounds;

    /// <summary>The selected item's last committed position in the complete semantic item order.
    /// Ordinary unavailability repairs locate the retained item in that live order. This snapshot
    /// is reserved for an unexpected child-initiated detachment where the identity has already left
    /// the tree; committed host changes refresh it while the selection remains attached.</summary>
    private int _selectedIndex = -1;

    /// <summary>Gets the retained authored-presentation count used to prove metadata retires with ownership.</summary>
    internal int RequestedPresentationCount => _requestedPresentations.Count;

    /// <summary>Gets the private footer offset used to prove bounded footer exposure.</summary>
    internal int FooterVerticalOffset => _footerStack.VerticalOffset;

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
    /// The view republishes the generated container's transition with itself as sender, so a
    /// consumer can observe scroll position without reaching into private presentation trees.
    /// </remarks>
    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

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

    /// <summary>Gets or sets the non-negative wheel-scroll increment in cells forwarded to the
    /// generated scroll container.</summary>
    /// <remarks>
    /// Keyboard navigation always moves by exactly one entry regardless of this value - only the
    /// mouse wheel consults it.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached navigation view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The navigation view is disposed.</exception>
    public int LineSize
    {
        get => _itemsStack.LineSize;
        set
        {
            var previous = _itemsStack.LineSize;
            _itemsStack.LineSize = value;

            if (previous != _itemsStack.LineSize)
            {
                NotifyPropertyChanged(nameof(LineSize), InvalidationImpact.None);
            }
        }
    }

    /// <summary>Gets or sets the non-negative cells of context retained between page commands.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached navigation view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The navigation view is disposed.</exception>
    public int PageOverlap
    {
        get => _itemsStack.PageOverlap;
        set
        {
            var previous = _itemsStack.PageOverlap;
            _itemsStack.PageOverlap = value;

            if (previous != _itemsStack.PageOverlap)
            {
                NotifyPropertyChanged(nameof(PageOverlap), InvalidationImpact.None);
            }
        }
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
        _itemsStack.BoundsChanged += OnNavigationHostBoundsChanged;
        _footerStack.BoundsChanged += OnNavigationHostBoundsChanged;
        _itemsStack.PropertyChanged += OnItemsStackPropertyChanged;
        _itemsStack.ScrollChanged += OnItemsStackScrollChanged;

        InitializeContent(root);
        _scrollBarStyle = InitializePartStyle(
            ScrollBarStyle.ForwardingDefinition,
            nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, _itemsStack, nameof(ScrollBarStyle));
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
        Select(item);
    }

    /// <summary>Verifies the owner before a public collection validates candidate-specific state.</summary>
    internal void VerifyMutation() => VerifyMutable();

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
        VerifyMutable();
        ValidateEntry(entry);
        var stack = isFooter ? _footerStack : _itemsStack;

        // Ownership is secured before any authored property is captured or
        // overwritten. A rejected insertion must leave the caller's object
        // exactly as it found it.
        stack.Children.Insert(index, entry);
        var presentation = new NavigationEntryPresentation(
            entry.IsFocusable,
            entry.IsTabStop,
            ++_presentationVersion);
        _requestedPresentations.Add(entry, presentation);
        ConfigureEntry(stack, entry, presentation.Version);
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
        var presentation = TakePresentation(entry);

        _isHandlingKnownRemoval = true;

        try
        {
            _ = stack.Children.Remove(entry);
        }
        finally
        {
            _isHandlingKnownRemoval = false;
        }

        UnsubscribeEntry(entry);
        CompleteRemoval(repair);

        if (restorePresentation && presentation is { } requested)
        {
            RestorePresentation(entry, requested);
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

        _isHandlingKnownRemoval = true;

        try
        {
            stack.Children[index] = entry;
        }
        finally
        {
            _isHandlingKnownRemoval = false;
        }

        UnsubscribeEntry(old);
        var oldPresentation = TakePresentation(old);
        var presentation = new NavigationEntryPresentation(
            entry.IsFocusable,
            entry.IsTabStop,
            ++_presentationVersion);
        _requestedPresentations.Add(entry, presentation);
        ConfigureEntry(stack, entry, presentation.Version);
        CompleteRemoval(repair);

        if (oldPresentation is { } requested)
        {
            RestorePresentation(old, requested);
        }
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

        RetireDetachedPresentationMetadata();

        // This notification publishes from inside DisposeCore, which removes the item from
        // its owning slot before IsDisposed itself flips true — IsDisposing is what's already
        // set at this point.
        if (_navigator.Current is { IsDisposing: true } or { IsDisposed: true })
        {
            _ = SetCurrent(null);
        }

        if (SelectedItem is { IsDisposing: true } or { IsDisposed: true })
        {
            Select(FindAvailableAtSemanticIndex(_selectedIndex));
        }
        else if (SelectedItem is { } selected)
        {
            _selectedIndex = CollectSemanticItems().IndexOf(selected);
        }
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
            Select(FindAvailableAtSemanticIndex(repair.SelectedIndex));
        }
    }

    private NavigationEntryPresentation? TakePresentation(ControlBase entry)
        => _requestedPresentations.Remove(entry, out var presentation) ? presentation : null;

    private void ConfigureEntry(LayoutStack stack, ControlBase entry, long presentationVersion)
    {
        entry.IsFocusable = false;

        if (!IsCommitted(stack, entry, presentationVersion))
        {
            return;
        }

        entry.IsTabStop = false;

        if (!IsCommitted(stack, entry, presentationVersion))
        {
            return;
        }

        entry.PropertyChanged += OnEntryFocusPolicyChanged;

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
    private bool IsCommitted(LayoutStack stack, ControlBase entry, long presentationVersion) =>
        stack.Children.Contains(entry) &&
        _requestedPresentations.TryGetValue(entry, out var presentation) &&
        presentation.Version == presentationVersion;

    private void UnsubscribeEntry(ControlBase entry)
    {
        entry.PropertyChanged -= OnEntryFocusPolicyChanged;

        if (entry is NavigationViewItem item)
        {
            item.Invoked -= OnItemInvoked;
        }
        else if (entry is NavigationViewGroup group)
        {
            group.VisibilityChanged -= OnGroupVisibilityChanged;
        }
    }

    private void OnEntryFocusPolicyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (_isWritingEntryFocusPolicy ||
            sender is not ControlBase entry ||
            !_requestedPresentations.TryGetValue(entry, out var presentation))
        {
            return;
        }

        if (eventArgs.PropertyName == nameof(IsFocusable))
        {
            _requestedPresentations[entry] = presentation.WithFocusable(entry.IsFocusable);

            if (entry.IsFocusable)
            {
                WriteEntryFocusPolicy(entry, isFocusable: true);
            }
        }
        else if (eventArgs.PropertyName == nameof(IsTabStop))
        {
            _requestedPresentations[entry] = presentation.WithTabStop(entry.IsTabStop);

            if (entry.IsTabStop)
            {
                WriteEntryFocusPolicy(entry, isFocusable: false);
            }
        }
    }

    private void WriteEntryFocusPolicy(ControlBase entry, bool isFocusable)
    {
        if (entry.IsDisposed || entry.IsDisposing)
        {
            return;
        }

        _isWritingEntryFocusPolicy = true;

        try
        {
            if (isFocusable)
            {
                entry.IsFocusable = false;
            }
            else
            {
                entry.IsTabStop = false;
            }
        }
        finally
        {
            _isWritingEntryFocusPolicy = false;
        }
    }

    private void RetireDetachedPresentationMetadata()
    {
        foreach (var entry in _requestedPresentations.Keys.ToArray())
        {
            if (!ReferenceEquals(entry.Parent, _itemsStack) &&
                !ReferenceEquals(entry.Parent, _footerStack))
            {
                _ = _requestedPresentations.Remove(entry);
            }
        }
    }

    private static void RestorePresentation(ControlBase entry, NavigationEntryPresentation presentation)
    {
        if (entry.Parent is not null || entry.IsDisposed || entry.IsDisposing)
        {
            return;
        }

        entry.IsFocusable = presentation.IsFocusable;

        if (!entry.IsDisposed && !entry.IsDisposing)
        {
            entry.IsTabStop = presentation.IsTabStop;
        }
    }

    /// <summary>Clears all entries in a section.</summary>
    internal void ClearEntries(bool isFooter)
    {
        VerifyMutable();
        var stack = isFooter ? _footerStack : _itemsStack;
        var repair = PrepareRemoval(stack);
        var entries = stack.Children.ToArray();
        var presentations = new List<(ControlBase Entry, NavigationEntryPresentation Presentation)>();

        foreach (var child in entries)
        {
            if (TakePresentation(child) is { } presentation)
            {
                presentations.Add((child, presentation));
            }
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

        foreach (var child in entries)
        {
            UnsubscribeEntry(child);
        }

        CompleteRemoval(repair);

        foreach (var (entry, presentation) in presentations)
        {
            RestorePresentation(entry, presentation);
        }
    }

    /// <summary>Updates the selected item when a child receives focus externally.</summary>
    internal void NotifyItemFocused(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _ = SetCurrent(item);
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
            _ = SetCurrent(item);
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

        Select(FindAvailableAdjacentTo(SelectedItem));
    }

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Bubble || !eventArgs.IsKeyDown)
        {
            return;
        }

        int direction;

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

        if (eventArgs.Stroke.Code is Code.Home or Code.End)
        {
            var endpoints = CollectNavigableEntries();

            if (endpoints.Count > 0)
            {
                var target = eventArgs.Stroke.Code == Code.Home ? endpoints[0] : endpoints[^1];
                _ = SetCurrent(target);
                CommitCurrent(target);
                eventArgs.IsHandled = true;
            }

            return;
        }

        // PageUp/PageDown: move by a viewport's worth of realized item height. Handling the key
        // here - rather than leaving it unhandled - is what stops it from escaping to page an
        // enclosing scrollable container out from under the still-focused view.
        if (eventArgs.Stroke.Code is Code.PageUp or Code.PageDown)
        {
            var entries = CollectNavigableEntries();

            if (entries.Count > 0)
            {
                var target = StepPage(entries, eventArgs.Stroke.Code == Code.PageDown ? 1 : -1);
                _ = SetCurrent(target);
                CommitCurrent(target);
                eventArgs.IsHandled = true;
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

        eventArgs.IsHandled = true;

        if (_navigator.Move(direction, wrap: false) && _navigator.Current is { } current)
        {
            CommitCurrent(current);
        }
    }

    private void OnPointerRouted(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Preview ||
            eventArgs.IsHandled ||
            eventArgs.Pointer.Action != PointerAction.Wheel)
        {
            return;
        }

        var x = (int) Math.Clamp((long) eventArgs.Pointer.WheelX * LineSize, int.MinValue, int.MaxValue);
        var y = (int) Math.Clamp(-(long) eventArgs.Pointer.WheelY * LineSize, int.MinValue, int.MaxValue);
        eventArgs.IsHandled = _itemsStack.ScrollBy(x, y, ScrollCause.Wheel);
    }

    private void OnItemsStackPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName is nameof(Extent) or nameof(Viewport) or
            nameof(HorizontalOffset) or nameof(VerticalOffset))
        {
            NotifyPropertyChanged(eventArgs.PropertyName, InvalidationImpact.None);
        }
    }

    private void OnItemsStackScrollChanged(object? sender, ScrollChangedEventArgs eventArgs)
    {
        _ = sender;
        ScrollChanged?.Invoke(this, eventArgs);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            _itemsStack.PropertyChanged -= OnItemsStackPropertyChanged;
            _itemsStack.ScrollChanged -= OnItemsStackScrollChanged;
            foreach (var group in _requestedPresentations.Keys.OfType<NavigationViewGroup>())
            {
                group.RetirePresentationMetadataForOwnerDisposal();
            }

            _requestedPresentations.Clear();
            SelectionChanged = null;
            ScrollChanged = null;
            _ = SetCurrent(null);
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

    private void CommitCurrent(ControlBase current)
    {
        if (current is NavigationViewItem item)
        {
            Select(item);
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

    private void Select(NavigationViewItem? item)
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

        SelectionChanged?.Invoke(this, new NavigationViewSelectionChangedEventArgs(previous, item));
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
        Select(replacement);
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

    // Accumulates realized entry heights from the current position until the sum reaches the
    // committed viewport height, rather than treating the viewport's cell height as an entry
    // count. A landing index that runs past either end is clamped into range.
    [Pure]
    private ControlBase StepPage(List<ControlBase> entries, int direction)
    {
        var index = _navigator.Current is { } current ? entries.IndexOf(current) : -1;

        if (index < 0)
        {
            index = direction > 0 ? -1 : entries.Count;
        }

        var target = PagingStep.TargetExtent(Viewport.Height, PageOverlap);
        var result = PagingStep.Accumulate(index, direction, entries.Count, target, i => entries[i].Bounds.Height, clamp: true);

        return entries[result];
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
