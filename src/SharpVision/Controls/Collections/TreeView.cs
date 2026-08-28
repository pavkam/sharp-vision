// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using Layout;

using Scrolling;

using SharpVision.Controls.Input;
using SharpVision.Terminal.Input;

using LayoutStack = Layout.Stack;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;
using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Displays hierarchical data as an expandable and collapsible tree of items.</summary>
[PublicAPI]
public sealed class TreeView: CompositeControlBase, IStyled<TreeViewStyle>
{
    private readonly StyleSlot<TreeViewStyle> _style;

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached tree view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    public TreeViewStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation, including the
    /// loading and failed status colors and glyphs a synthetic status row draws.</summary>
    public TreeViewStyle ActualStyle => _style.Actual;

    private readonly LayoutStack _itemsStack;
    private readonly StyleSlot<ScrollBarStyle> _scrollBarStyle;
    private readonly CurrentItemNavigator _navigator;
    private readonly HashSet<TreeViewItem> _selectedItems = [];
    private TreeViewItem? _selectionAnchor;
    private readonly List<ControlBase> _flatBuffer = [];

    // Preorder snapshot of every currently owned item - including collapsed-but-attached branches
    // FlattenVisible structurally cannot include - refreshed only inside RebuildFlatList, the one
    // place a genuine structural change already pays for a full walk. Selection bookkeeping reads
    // these instead of re-walking the tree on every keyboard navigation keystroke.
    private readonly List<TreeViewItem> _ownedItems = [];
    private readonly HashSet<TreeViewItem> _ownedItemsSet = [];

    // Set when NotifyStructureChanged defers a rebuild for an open batch, so the cache above is
    // known-stale relative to the live tree. CommitSelection refreshes on demand before it reads
    // either collection, so a SelectAll/SetSelected called mid-batch still sees every item added
    // since BeginUpdate instead of silently filtering it out as not-yet-owned.
    private bool _ownedItemsStale;

    // Counts full-tree preorder walks (RefreshOwnedItems calls), not wall-clock timing, so a
    // performance test can assert keyboard navigation triggers none.
    internal int OwnedItemsWalkCount;

    private readonly List<TreeViewItemCollection> _walkItems = [];
    private readonly List<int> _walkIndex = [];
    private HashSet<TreeViewItem> _hooked = [];
    private HashSet<TreeViewItem> _hookedNext = [];
    private int _selectionVersion;
    private bool _rebuildScheduled;
    private int _batchUpdateDepth;
    private readonly Queue<TreeViewItem> _pendingChildLoads = new();
    private int _activeChildLoads;

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
    /// reaching into private presentation trees.
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
    [NonNegativeValue]
    public int HorizontalOffset
    {
        get => _itemsStack.HorizontalOffset;
        set => _itemsStack.HorizontalOffset = value;
    }

    /// <summary>Gets or sets the valid vertical content offset of the generated scroll container.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached tree view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    [NonNegativeValue]
    public int VerticalOffset
    {
        get => _itemsStack.VerticalOffset;
        set => _itemsStack.VerticalOffset = value;
    }

    /// <summary>Gets or sets the non-negative wheel-scroll increment in cells forwarded to the
    /// generated scroll container.</summary>
    /// <remarks>
    /// Keyboard navigation always moves by exactly one item regardless of this value - only the
    /// mouse wheel consults it.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached tree view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    [NonNegativeValue]
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
    /// <exception cref="InvalidOperationException">The attached tree view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    [NonNegativeValue]
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
        _style = InitializeStyle(TreeViewStyle.Definition);
        _scrollBarStyle = InitializePartStyle(
            ScrollBarStyle.ForwardingDefinition,
            nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, _itemsStack, nameof(ScrollBarStyle));
        Items = new TreeViewItemCollection { Owner = this };
        IsFocusable = true;
        IsTabStop = true;
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

    /// <summary>Gets or sets the currently selected item, or null.</summary>
    /// <remarks>
    /// Assigning a non-null item calls <see cref="SelectItem"/>; assigning null calls
    /// <see cref="ClearSelection"/>. Both paths run the same ownership check, cancellation, and
    /// selection-mode normalization as direct input, so this setter is a thin two-way binding seam
    /// rather than a raw field write.
    /// </remarks>
    /// <exception cref="ArgumentException">The assigned item is not owned by this tree view.</exception>
    public TreeViewItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (ReferenceEquals(_selectedItem, value))
            {
                return;
            }

            if (value is null)
            {
                ClearSelection();
            }
            else
            {
                SelectItem(value);
            }
        }
    }

    private TreeViewItem? _selectedItem;

    /// <summary>Gets an immutable snapshot of the current selection in stable tree order.</summary>
    public IReadOnlyList<TreeViewItem> SelectedItems => CollectSelectedItems().AsReadOnly();

    /// <summary>Gets or sets whether the tree permits no, one, or many selected items.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="TreeSelectionMode"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached tree view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    public TreeSelectionMode SelectionMode
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The selection mode is unknown.");

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
                var first = FindFirstInTreeOrder(_selectedItems);
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

            var previousValue = field;
            var previous = ActualCheckMark;
            field = value;
            var current = ActualCheckMark;
            field = previousValue;

            // The mark occupies cells ahead of the header, so a width change moves every row's
            // text and needs a measure pass rather than a repaint.
            var impact = previous.Width != current.Width
                ? InvalidationImpact.Measure
                : previous == current
                    ? InvalidationImpact.None
                    : InvalidationImpact.Render;
            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                impact,
                () =>
                {
                    var live = ActualCheckMark;

                    if (previous != live)
                    {
                        NotifyPropertyChanged(nameof(ActualCheckMark), InvalidationImpact.None);
                        InvalidateItemChrome(previous.Width != live.Width
                            ? InvalidationImpact.Measure
                            : InvalidationImpact.Render);
                    }
                });
        }
    }

    /// <summary>Gets the check-mark presentation applied to items that do not override it.</summary>
    /// <remarks>
    /// The fallback is theme-resolved, not the static <c>CheckMark.Brackets</c> preset.
    /// <c>CheckBox</c> resolves its glyphs through <c>CheckBoxStyle.Definition</c>, which
    /// completes from the active theme's glyph family, so selecting a different family in a
    /// theme's root-level "glyphs" field - the documented way to restyle check marks - moves
    /// every CheckBox; without this fallback, a tree row would stay on the code-owned family,
    /// silently, on the same screen. <c>ForwardingDefinition</c> is the seam built for this: it
    /// hands over the narrow value without applying the checkbox appearance, which would recolor
    /// the whole row.
    /// </remarks>
    public CheckMark ActualCheckMark => CheckMark ?? ThemedCheckMark(Theme);

    /// <summary>Projects the theme-resolved checkbox mark family into the narrow shared value.</summary>
    /// <param name="theme">The inherited theme, or null before attachment.</param>
    /// <returns>The check mark a row renders when nothing overrides it.</returns>
    [Pure]
    internal static CheckMark ThemedCheckMark(Theme? theme)
    {
        var style = CheckBoxStyle.ForwardingDefinition.Resolve(null, theme);
        return new CheckMark(style.MarkStyle, style.Glyphs);
    }

    /// <summary>Gets or sets the number of cells per indentation level.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached tree view is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tree view is disposed.</exception>
    [NonNegativeValue]
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

    /// <summary>Gets or sets the text an unloaded item's inline synthetic row displays while its
    /// children are loading.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value contains a terminal control character.</exception>
    public string LoadingText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentException.ThrowIfContainsControls(value, nameof(value), "The loading text cannot contain terminal controls.");
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    } = "Loading…";

    /// <summary>Gets or sets the text a failed item's inline synthetic row displays, alongside its
    /// retry affordance.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value contains a terminal control character.</exception>
    public string LoadFailedText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentException.ThrowIfContainsControls(value, nameof(value), "The load-failed text cannot contain terminal controls.");
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    } = "Failed to load. Press Enter to retry.";

    /// <summary>Gets or sets the maximum number of child-loading requests this tree runs at once
    /// across every item; additional requests queue until a running one completes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    [ValueRange(1, int.MaxValue)]
    public int MaxConcurrentChildLoads
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 4;

    /// <summary>Requests a concurrent child-loading admission slot on behalf of one item.</summary>
    /// <param name="item">The item starting a request.</param>
    /// <returns>True when a slot was granted immediately; false when the item was queued instead.</returns>
    internal bool RequestLoadSlot(TreeViewItem item)
    {
        if (_activeChildLoads < MaxConcurrentChildLoads)
        {
            _activeChildLoads++;
            return true;
        }

        _pendingChildLoads.Enqueue(item);
        return false;
    }

    /// <summary>Releases one previously granted admission slot, and grants it to the next queued
    /// item still genuinely waiting for it.</summary>
    internal void ReleaseLoadSlot()
    {
        _activeChildLoads--;

        while (_pendingChildLoads.Count > 0)
        {
            var next = _pendingChildLoads.Dequeue();

            // A queued entry goes stale when its request is cancelled, superseded, or the item is
            // disposed while still waiting - skip it without consuming a slot.
            if (next.IsDisposed || !next.IsAwaitingLoadSlot || next.ChildState != TreeViewChildState.Loading)
            {
                continue;
            }

            _activeChildLoads++;
            next.GrantQueuedLoadSlot();
            return;
        }
    }

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
        if (_batchUpdateDepth > 0)
        {
            _ownedItemsStale = true;
            return;
        }

        if (_rebuildScheduled)
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

        if (!OwnsInvokedItem(item))
        {
            return;
        }

        _ = _navigator.SetCurrent(item);

        if (!OwnsInvokedItem(item))
        {
            return;
        }

        _ = ApplyInputSelection(item, modifiers);

        if (!OwnsInvokedItem(item))
        {
            return;
        }

        ItemInvoked?.Invoke(this, new TreeViewItemInvokedEventArgs(item, cause));
    }

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Bubble || !eventArgs.IsKeyDown)
        {
            return;
        }

        // Enter retries a failed load on the current item, or otherwise activates it. Space is a
        // selection/check action.
        if (eventArgs.IsInitialKeyDown && eventArgs.Stroke.Code == Code.Enter)
        {
            if (_navigator.Current is TreeViewItem { ChildState: TreeViewChildState.LoadFailed } failed)
            {
                if (eventArgs.Stroke.Modifiers.IsActivationEligible())
                {
                    _ = failed.ReloadChildrenAsync();
                    eventArgs.IsHandled = true;
                }

                return;
            }

            eventArgs.IsHandled = ActivateCurrent(eventArgs.Stroke.Modifiers);
            return;
        }

        if (eventArgs.IsInitialKeyDown &&
            eventArgs.Stroke.Code == Code.Character &&
            eventArgs.Stroke.Character == new Rune(' ') &&
            KeyboardModifierPolicy.IsCollectionSelectionEligible(eventArgs.Stroke.Modifiers))
        {
            if (_navigator.Current is TreeViewItem { IsCheckable: true } checkable)
            {
                checkable.SetCheckState(checkable.IsChecked != true, ActivationCause.Keyboard, propagate: true);
                eventArgs.IsHandled = true;
                return;
            }

            if (_navigator.Current is TreeViewItem toggle)
            {
                eventArgs.IsHandled = ApplyInputSelection(
                    toggle,
                    SelectionMode == TreeSelectionMode.Multiple ? Modifiers.Control : Modifiers.None);
                return;
            }
        }

        if (eventArgs.IsInitialKeyDown &&
            eventArgs.Stroke.Code == Code.Character &&
            eventArgs.Stroke.Character is { } character &&
            Rune.ToLowerInvariant(character) == new Rune('a') &&
            KeyboardModifierPolicy.MatchesCommand(eventArgs.Stroke.Modifiers, Modifiers.Control) &&
            SelectionMode == TreeSelectionMode.Multiple)
        {
            SelectAll();
            eventArgs.IsHandled = true;
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
                eventArgs.IsHandled = true;
            }

            return;
        }

        // PageUp/PageDown: move by a viewport's worth of realized item height. Handling the key
        // here - rather than leaving it unhandled - is what stops it from escaping to page an
        // enclosing scrollable container out from under the still-focused tree.
        if (eventArgs.Stroke.Code is Code.PageUp or Code.PageDown)
        {
            var visible = CollectVisibleItems();

            if (visible.Count > 0)
            {
                var target = StepPage(visible, eventArgs.Stroke.Code == Code.PageDown ? 1 : -1);
                _ = _navigator.SetCurrent(target);
                CommitCurrent(target, eventArgs.Stroke.Modifiers);
                eventArgs.IsHandled = true;
            }

            return;
        }

        // Left: collapse current or navigate to parent
        if (eventArgs.Stroke.Code == Code.Left)
        {
            eventArgs.IsHandled = HandleLeft();
            return;
        }

        // Right: expand current or navigate to first child
        if (eventArgs.Stroke.Code == Code.Right)
        {
            eventArgs.IsHandled = HandleRight();
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

        eventArgs.IsHandled = true;

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

            if (!OwnsInvokedItem(item))
            {
                return true;
            }

            _ = ApplyInputSelection(item, modifiers);

            // An incidental modifier still applies selection above, but does not commit an
            // invocation the user did not intend.
            if (modifiers.IsActivationEligible() && OwnsInvokedItem(item))
            {
                ItemInvoked?.Invoke(this, new TreeViewItemInvokedEventArgs(item, ActivationCause.Keyboard));
            }

            return true;
        }

        return false;
    }

    private bool OwnsInvokedItem(TreeViewItem item) =>
        !item.IsDisposed && ReferenceEquals(item.FindTreeView(), this);

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

        // A stored anchor is not necessarily a live Shift-range endpoint: RebuildFlatList
        // deliberately retains it across an ancestor collapse ("collapsing a branch does not erase
        // selection state"), but ShiftEligibleRange only ever resolves over currently-visible
        // items. Gating hasAnchor on visibility rather than mere non-nullness means a Shift-click
        // against a hidden anchor falls through to Resolve's plain-click branch instead of finding
        // no range and silently leaving both selection and anchor untouched - it re-anchors on the
        // clicked item so the next gesture behaves normally.
        var hasAnchor = _selectionAnchor is not null && CollectVisibleItems().Contains(_selectionAnchor);

        var (next, nextAnchor) = SelectionGesture<TreeViewItem>.Resolve(
            EqualityComparer<TreeViewItem>.Default,
            _selectedItems,
            hasAnchor,
            _selectionAnchor!,
            item,
            modifiers,
            SelectionMode == TreeSelectionMode.Multiple,
            ShiftEligibleRange);

        // Adopted unconditionally, even when the commit below is refused or cancelled by a
        // SelectionChanging handler - the anchor tracks the last gesture target, not the last
        // accepted one.
        _selectionAnchor = nextAnchor;

        return CommitSelection(next, cancellable: true);
    }

    private IEnumerable<TreeViewItem>? ShiftEligibleRange(TreeViewItem anchor, TreeViewItem target)
    {
        var visible = CollectVisibleItems();
        var start = IndexOf(visible, anchor);
        var end = IndexOf(visible, target);

        return start < 0 || end < 0
            ? null
            : visible
            .Skip(Math.Min(start, end))
            .Take(Math.Abs(end - start) + 1)
            .Cast<TreeViewItem>()
            .Where(static candidate => candidate.IsEnabled);
    }

    private bool CommitSelection(IEnumerable<TreeViewItem> items, bool cancellable = false)
    {
        // A batch left the ownership cache stale (SetSelected reads _ownedItemsSet below without
        // going through CollectAllItems first) - catch up before filtering, or an item added
        // since BeginUpdate would be rejected as not-yet-owned.
        EnsureOwnedItemsFresh();

        var next = new HashSet<TreeViewItem>(items);

        // Retention filters on ownership only - EffectiveIsEnabled walks the whole ancestor chain,
        // so a disabled ancestor would wipe every realized item's selection on the next rebuild
        // regardless of the item's own state. "Disabled items are never selected" is instead
        // enforced by the request paths themselves (SetSelected, ApplyInputSelection, SelectAll)
        // before they ever reach here. _ownedItemsSet gives O(1) membership instead of the O(tree
        // size) walk a fresh CollectAllItems() would cost on every single keyboard navigation
        // keystroke that reaches here.
        _ = next.RemoveWhere(item => !_ownedItemsSet.Contains(item));

        if (SelectionMode == TreeSelectionMode.None)
        {
            next.Clear();
        }
        else if (SelectionMode == TreeSelectionMode.Single && next.Count > 1)
        {
            var first = FindFirstInTreeOrder(next);
            next = first is null ? [] : [first];
        }

        var previous = SelectedItem;

        var committed = SelectionCommit<TreeViewItem>.TryCommit(
            _selectedItems,
            next,
            EqualityComparer<TreeViewItem>.Default,
            null,
            SelectionMode == TreeSelectionMode.None,
            ComputeSelectionDelta,
            cancellable,
            ref _selectionVersion,
            static (added, removed) => new TreeViewSelectionChangingEventArgs(added, removed),
            this,
            SelectionChanging,
            out var selection,
            out var added,
            out var removed);

        if (!committed)
        {
            return false;
        }

        var selectionVersion = _selectionVersion;

        _ = selection.RemoveWhere(item => item.IsDisposed || !_ownedItemsSet.Contains(item));

        foreach (var item in _selectedItems)
        {
            if (!item.IsDisposed)
            {
                item.CommitSelection(selection.Contains(item));
            }
        }

        foreach (var item in selection)
        {
            item.CommitSelection(true);
        }

        _selectedItems.Clear();
        _selectedItems.UnionWith(selection);
        _selectedItem = FindFirstInTreeOrder(selection);
        NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);

        if (_selectionVersion != selectionVersion)
        {
            return true;
        }

        NotifyPropertyChanged(nameof(SelectedItems), InvalidationImpact.Render);

        if (_selectionVersion != selectionVersion)
        {
            return true;
        }

        SelectionChanged?.Invoke(
            this,
            new TreeViewSelectionChangedEventArgs(previous, SelectedItem, added, removed));

        return true;
    }

    // Bounded by the size of the change, not the size of the tree: iterates only the (typically
    // tiny) current and selectable sets, then sorts just the resulting delta by TreeOrder for
    // stability. This relies on selectable already being filtered to owned items only - the
    // RemoveWhere in CommitSelection guarantees that - so "in selectable but not current" and "in
    // current but not selectable" together are exactly the added/removed sets, whether or not an
    // item left ownership entirely: a detached item can only ever appear in current, never in
    // selectable, so it is still correctly reported as removed by the second loop below.
    [Pure]
    private static (TreeViewItem[] Added, TreeViewItem[] Removed) ComputeSelectionDelta(
        IReadOnlySet<TreeViewItem> current,
        IReadOnlySet<TreeViewItem> selectable)
    {
        List<TreeViewItem> added = [];
        List<TreeViewItem> removed = [];

        foreach (var item in selectable)
        {
            if (!current.Contains(item))
            {
                added.Add(item);
            }
        }

        foreach (var item in current)
        {
            if (!selectable.Contains(item))
            {
                removed.Add(item);
            }
        }

        added.Sort(static (left, right) => left.TreeOrder.CompareTo(right.TreeOrder));
        removed.Sort(static (left, right) => left.TreeOrder.CompareTo(right.TreeOrder));

        return ([.. added], [.. removed]);
    }

    // Linear scan for the minimum TreeOrder, not a sort - callers only ever need the single
    // first-in-tree-order item, and the candidate set is bounded by selection size, never by tree
    // size.
    [Pure]
    private static TreeViewItem? FindFirstInTreeOrder(IEnumerable<TreeViewItem> items)
    {
        TreeViewItem? first = null;

        foreach (var item in items)
        {
            if (first is null || item.TreeOrder < first.TreeOrder)
            {
                first = item;
            }
        }

        return first;
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
        NotifyStructureChanged();
    }

    /// <summary>Notifies the tree that an item's <see cref="ControlBase.Visibility"/> changed,
    /// which can add or remove rows for the item itself and its subtree.</summary>
    internal void NotifyItemVisibilityChanged(TreeViewItem item)
    {
        _ = item;
        NotifyStructureChanged();
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
            // A synthetic TreeViewStatusRow shares the flattened list with real items so it
            // occupies the right row in scroll and layout order, but it never raises Invoked and
            // is not itself a navigable, selectable item - only TreeViewItem entries hook here.
            if (control is not TreeViewItem item)
            {
                continue;
            }

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

        // One preorder walk stamps every owned item's stable TreeOrder and refreshes the ownership
        // registry - selection bookkeeping below, and every keyboard navigation keystroke until
        // the next structural change, reads it instead of re-walking the tree.
        RefreshOwnedItems();

        // Repair selection only when nodes are detached; collapsing a branch does not erase state.
        // Ownership alone gates retention here too - EffectiveIsEnabled would make a disabled
        // ancestor wipe every realized item's selection on the next rebuild. _ownedItemsSet gives
        // O(1) membership instead of the O(tree size) List.Contains a fresh CollectAllItems() walk
        // would cost per lookup.
        var retained = _selectedItems.Where(_ownedItemsSet.Contains).ToArray();
        if (_selectionAnchor is not null && !_ownedItemsSet.Contains(_selectionAnchor))
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
            item.IsFocusable = false;
            item.IsTabStop = false;

            // Collapsed removes the row entirely, mirroring IsExpanded = false: zero rows for the
            // item and its subtree. Hidden keeps the item's own blank slot (per the box model's
            // existing per-row semantics) but still hides descendants, mirroring the ancestor
            // inheritance EffectiveIsVisible applies everywhere else - realization here is flat,
            // so that inheritance has to be walked explicitly instead of falling out of Parent.
            if (item.Visibility != Visibility.Collapsed)
            {
                destination.Add(item);

                if (item.IsExpanded &&
                    item.Visibility == Visibility.Visible &&
                    item.GetStatusRowIfNeeded() is { } statusRow)
                {
                    statusRow.Depth = depth + 1;
                    statusRow.IsEnabled = item.EffectiveIsEnabled;
                    destination.Add(statusRow);
                }
            }

            if (item.HasChildren && item.IsExpanded && item.Visibility == Visibility.Visible)
            {
                _walkItems.Add(item.Children);
                _walkIndex.Add(0);
            }
        }
    }

    private void OnItemInvoked(object? sender, ActivationEventArgs eventArgs)
    {
        // Keyboard activation reaches this bridge through ActivateFromOwner, which exists to
        // raise the item's own public Invoked event - ActivateCurrent already applies selection
        // and raises ItemInvoked itself, with the live stroke's modifiers, so repeating that here
        // would both double the invocation and use this item's stale last-pointer modifiers.
        if (eventArgs.Cause == ActivationCause.Keyboard)
        {
            return;
        }

        if (sender is TreeViewItem item)
        {
            NotifyItemInvoked(item, eventArgs.Cause, item.LastModifiers);
        }
    }

    // Accumulates realized item heights from the current position until the sum reaches the
    // committed viewport height, rather than treating the viewport's cell height as an item
    // count. A landing index that runs past either end is clamped into range.
    [Pure]
    private ControlBase StepPage(List<ControlBase> visible, int direction)
    {
        var index = _navigator.Current is { } current ? visible.IndexOf(current) : -1;

        if (index < 0)
        {
            index = direction > 0 ? -1 : visible.Count;
        }

        var target = PagingStep.TargetExtent(Viewport.Height, PageOverlap);
        var result = PagingStep.Accumulate(index, direction, visible.Count, target, i => visible[i].Bounds.Height, clamp: true);

        return visible[result];
    }

    [Pure]
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

    [Pure]
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
                if (!item.HasChildren)
                {
                    continue;
                }

                // Expanding an unloaded branch would fire a remote load - something ExpandAll never
                // promised to do, so it skips that branch entirely rather than materializing it.
                // Collapsing is always safe regardless of ChildState.
                if (expanded && item.ChildState == TreeViewChildState.Unloaded)
                {
                    continue;
                }

                item.IsExpanded = expanded;
                pending.Add(item.Children);
            }
        }
    }

    [Pure]
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

    // The only place a full preorder tree walk still happens. Called exclusively from
    // RebuildFlatList, which itself only runs on a genuine structural change - never on a
    // keyboard navigation keystroke - so every other reader below consults the resulting cache
    // instead of re-walking.
    private void RefreshOwnedItems()
    {
        OwnedItemsWalkCount++;
        _ownedItemsStale = false;
        _ownedItems.Clear();
        _ownedItemsSet.Clear();

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
            item.TreeOrder = _ownedItems.Count;
            _ownedItems.Add(item);
            _ = _ownedItemsSet.Add(item);
            pending.Add(item.Children);
            cursors.Add(0);
        }
    }

    // A fresh, independent snapshot of the cache RefreshOwnedItems maintains - callers that need
    // every owned item (SelectAll, by definition) get a copy safe to enumerate and filter without
    // paying for another tree walk. EnsureOwnedItemsFresh is a no-op outside an open batch, since
    // NotifyStructureChanged already rebuilds eagerly there.
    private List<TreeViewItem> CollectAllItems()
    {
        EnsureOwnedItemsFresh();
        return [.. _ownedItems];
    }

    // Catches up the ownership cache when a batch deferred its rebuild past a structural change -
    // called by every reader that needs guaranteed-current ownership, so a SelectAll or
    // SetSelected issued between BeginUpdate and EndUpdate still sees items added during the
    // batch instead of filtering them out as not-yet-owned.
    private void EnsureOwnedItemsFresh()
    {
        if (_ownedItemsStale)
        {
            RefreshOwnedItems();
        }
    }

    // Bounded by selection size, not tree size: sorts only the (typically tiny) selected set by
    // TreeOrder instead of walking every owned item and filtering down to the selected ones.
    private List<TreeViewItem> CollectSelectedItems()
    {
        var result = new List<TreeViewItem>(_selectedItems);
        result.Sort(static (left, right) => left.TreeOrder.CompareTo(right.TreeOrder));
        return result;
    }

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
