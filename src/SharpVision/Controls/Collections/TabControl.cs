// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using System.ComponentModel;

using SharpVision.Terminal.Input;

using LayoutStack = Layout.Stack;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;
using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Arranges typed tab pages and coordinates header rendering and keyboard selection.</summary>
[PublicAPI]
public sealed class TabControl: ItemsControl, IStyled<TabControlStyle>
{
    private const int _headerRowHeight = 1;
    private const int _selectionIndicatorRowHeight = 1;
    private const int _headerStripHeight = _headerRowHeight + _selectionIndicatorRowHeight;
    private const int _headerSeparatorWidth = 1;
    private readonly Dictionary<TabItem, TabHeader> _headersByItem = [];
    private readonly RetainedPropertyOverrideService _propertyOverrides;
    private readonly HashSet<TabItem> _closeRequestsInFlight = [];
    private readonly StyleSlot<TabControlStyle> _style;
    private readonly LayoutStack _headers;
    private readonly LayoutStack _stack;
    private int _selectedIndex = -1;
    private long _closeRequestVersion;
    private long _selectionVersion;

    /// <summary>Initializes an empty tab control with typed managed pages.</summary>
    public TabControl()
    {
        EnableChromeAuthoring();
        _style = InitializeStyle(TabControlStyle.Definition);
        _headers = new LayoutStack
        {
            Height = Length.Cells(_headerRowHeight),
            HorizontalAlignment = HorizontalAlignment.Left,
            Orientation = Orientation.Horizontal,
            Spacing = _headerSeparatorWidth,
        };
        _stack = new LayoutStack { Orientation = Orientation.Vertical };
        var headersSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: false,
                partKey: "headers",
                InvalidationImpact.Measure),
            capacity: 1);
        headersSlot.Add(_headers);
        InitializeItemsHost(_stack);
        _propertyOverrides = new RetainedPropertyOverrideService(
            this,
            ItemControlsSlot,
            OnAuthoredPropertyRequest);
        Items = new TabItemCollection(this);
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.Continue;
        ConfigureHeaderOverflow();
    }

    /// <summary>Raised after the selected tab index changes while the control is live; disposal
    /// settles selection without publishing a transition.</summary>
    public event EventHandler<TabSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised before a closeable tab is removed; handlers may cancel the request, which
    /// stops delivery to later subscribers.</summary>
    public event EventHandler<TabCloseRequestedEventArgs>? CloseRequested;

    /// <summary>Gets the typed managed tab pages.</summary>
    public TabItemCollection Items { get; }

    /// <summary>Gets or sets the width applied to each retained header; automatic uses header content.</summary>
    /// <exception cref="InvalidOperationException">The attached tab control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tab control is disposed.</exception>
    public Length HeaderWidth
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
                    for (var index = 0; index < _headers.Children.Count; index++)
                    {
                        _headers.Children[index].Width = HeaderWidth;
                    }
                });
        }
    } = Length.Auto;

    /// <summary>Gets or sets whether headers clip or horizontally scroll when they exceed available width.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached tab control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tab control is disposed.</exception>
    public TabHeaderOverflowPolicy HeaderOverflowPolicy
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value);

            if (!SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                return;
            }

            ConfigureHeaderOverflow();
        }
    } = TabHeaderOverflowPolicy.Clip;

    /// <summary>Gets or selects the active page index, or -1 for no selection.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than -1 or is outside the current item range.</exception>
    /// <exception cref="InvalidOperationException">The attached tab control is mutated off-dispatcher, or the target page is unavailable.</exception>
    /// <exception cref="ObjectDisposedException">The tab control is disposed.</exception>
    [ValueRange(-1, int.MaxValue)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < -1 || (value >= 0 && value >= ItemControlCount))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "The selected index is outside the tab control.");
            }

            Select(value);
        }
    }

    /// <summary>Gets the selected page, or sets one owned page as selected; null clears selection.</summary>
    /// <remarks>Setting a page not owned by this tab control clears selection, matching <see cref="SelectedIndex"/>'s -1.</remarks>
    /// <exception cref="InvalidOperationException">The attached tab control is mutated off-dispatcher, or the target page is unavailable.</exception>
    /// <exception cref="ObjectDisposedException">The tab control is disposed.</exception>
    public TabItem? SelectedItem
    {
        get => _selectedIndex < 0 ? null : ItemAt(_selectedIndex);
        set => SelectedIndex = value is null ? -1 : IndexOfItem(value);
    }

    /// <summary>Gets or sets the complete developer-authored tab-strip style, or null for the
    /// theme-resolved one.</summary>
    /// <exception cref="InvalidOperationException">The attached tab control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tab control is disposed.</exception>
    public TabControlStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the resolved tab-strip style: the local <see cref="Style"/> when assigned,
    /// otherwise the theme-resolved default falling back to the "control" role section.</summary>
    public TabControlStyle ActualStyle => _style.Actual;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        ApplyPresentation();

        var headers = MeasureChild(_headers, new Constraint(constraint.Width, _headerRowHeight));
        var contentConstraint = new Constraint(constraint.Width,
            constraint.Height.HasValue ? Math.Max(0, constraint.Height.Value - _headerStripHeight) : null);
        var content = base.MeasureOverride(contentConstraint);

        return new Size(
            Math.Max(headers.Width, content.Width),
            (int) Math.Min(int.MaxValue, (long) _headerStripHeight + content.Height));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        ApplyPresentation();
        var headerHeight = Math.Min(_headerRowHeight, bounds.Height);
        var headerBounds = new Rect(bounds.X, bounds.Y, bounds.Width, headerHeight);
        ArrangeChild(_headers, headerBounds, ResolvedAxes.Both);

        if (RevealSelectedHeader())
        {
            ArrangeChild(_headers, headerBounds, ResolvedAxes.Both);
        }

        var contentOffset = Math.Min(_headerStripHeight, bounds.Height);
        base.ArrangeOverride(new Rect(
            bounds.X,
            bounds.Y.Add(contentOffset),
            bounds.Width,
            Math.Max(0, bounds.Height - _headerStripHeight)));
    }

    // Drawn after RenderChildren (which paints _headers, the header Stack's own opaque
    // background) rather than in OnRenderContent, which runs before it and had every
    // divider glyph overwritten by that background on the very next frame.
    /// <inheritdoc/>
    internal override void RenderOverlay(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var inherited = NormalStyle;

        // Resolved through ActualStyle rather than the appearance path, which flattens a style to
        // AppearanceStates and drops every non-appearance member. These four used to be control
        // properties defaulting to the internal ControlGlyphs registry and to semantic roles picked
        // at this draw site, so a theme could not reach any of them.
        var style = ActualStyle;
        var dividerStyle = inherited.WithForeground(ResolveColor(style.DividerColor, Theme));
        var indicatorStyle = inherited.WithForeground(ResolveColor(style.SelectionIndicatorColor, Theme));
        var separators = ControlGlyphs.Separators;
        var divider = style.DividerGlyph.Resolve(separators.TabDivider.Fallback, CellPolicy.AmbiguousWidth);
        var underline = style.UnderlineGlyph.Resolve(separators.TabUnderline.Fallback, CellPolicy.AmbiguousWidth);

        for (var index = 0; index < _headers.Children.Count; index++)
        {
            var header = HeaderAt(index);

            if (header.Visibility == Visibility.Visible &&
                HasVisibleHeaderAfter(index) &&
                header.Bounds.Right < ContentBounds.Right)
            {
                canvas.DrawRune(
                    divider,
                    new Point(header.Bounds.Right, ContentBounds.Y),
                    dividerStyle,
                    BackgroundMode.Transparent);
            }
        }

        if (ContentBounds.Height >= _headerStripHeight)
        {
            for (var lx = ContentBounds.X; lx < ContentBounds.Right; lx++)
            {
                canvas.DrawRune(
                    underline,
                    new Point(lx, ContentBounds.Y + _headerRowHeight),
                    indicatorStyle,
                    BackgroundMode.Transparent);
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        base.OnEvent(eventArgs);

        if (eventArgs.IsHandled)
        {
            return;
        }

        if (eventArgs is not KeyEventArgs { IsKeyDown: true } key)
        {
            return;
        }

        if (key.Stroke.Modifiers != Modifiers.None)
        {
            return;
        }

        eventArgs.IsHandled = key.IsInitialKeyDown && key.Stroke.Code == Code.Delete
            ? _selectedIndex >= 0 && RequestClose(ItemAt(_selectedIndex))
            : TryNavigate(key.Stroke.Code);
    }

    /// <inheritdoc/>
    internal override bool AddSelectableTextChildren(List<ControlBase> children)
    {
        ArgumentNullException.ThrowIfNull(children);

        if (SelectedItem is { } selectedItem)
        {
            children.Add(selectedItem);
        }

        return true;
    }

    [NonNegativeValue]
    internal int ItemCount => ItemControlCount;

    [Pure]
    internal TabItem ItemAt([NonNegativeValue] int index) => (TabItem) GetItemControl(index);

    [Pure]
    internal TabHeader HeaderAt([NonNegativeValue] int index) => (TabHeader) _headers.Children[index];

    internal void AddItem(TabItem item) => InsertItem(ItemControlCount, item);

    internal void InsertItem(int index, TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        VerifyMutable();

        if ((uint) index > (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index,
                "The insertion index is outside the tab control.");
        }

        var requestedVisibility = item.Visibility;
        var header = new TabHeader(item.HeaderText)
        {
            IsEnabled = item.IsEnabled,
            Visibility = requestedVisibility,
            Width = HeaderWidth,
        };
        header.Activated += OnHeaderActivated;
        var nextItems = new List<ControlBase>(ItemControlsSlot.Items);
        nextItems.Insert(index, item);
        var nextHeaders = new List<ControlBase>(_headers.Children);
        nextHeaders.Insert(index, header);
        var previousSelectedIndex = _selectedIndex;
        var previousSelectedItem = previousSelectedIndex >= 0 ? ItemAt(previousSelectedIndex) : null;
        var stagedSelectedIndex = previousSelectedIndex;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        var committed = false;
        ExceptionAggregation.Capture(
            () => OwnedControlRegistry.CommitCompound(
                () =>
                {
                    committed = true;
                    _headersByItem.Add(item, header);
                    _ = _propertyOverrides.Acquire(
                        item,
                        RetainedPropertyOverrides.Visibility,
                        RetainedPropertyOverrides.Width,
                        RetainedPropertyOverrides.Height);
                    item.PropertyChanged += OnItemPropertyChanged;

                    if (previousSelectedIndex >= index)
                    {
                        stagedSelectedIndex = previousSelectedIndex + 1;
                    }
                    else if (previousSelectedIndex < 0)
                    {
                        stagedSelectedIndex = SingleSelectionIndex.FindLinear(0, 1, ItemControlCount, IsEligible);
                    }

                    if (stagedSelectedIndex != previousSelectedIndex)
                    {
                        _selectedIndex = stagedSelectedIndex;
                        _selectionVersion++;
                    }
                },
                (ItemControlsSlot, nextItems),
                (_headers.Children.OwnedSlot, nextHeaders)),
            ref failure);

        if (!committed)
        {
            header.Activated -= OnHeaderActivated;
            header.Dispose();
            failure?.Throw();
            return;
        }

        ExceptionAggregation.Capture(() => WriteItemWidth(item, Length.Percent(100)), ref failure);

        if (!IsCommitted(item, header))
        {
            failure?.Throw();
            return;
        }

        ExceptionAggregation.Capture(() => WriteItemHeight(item, Length.Percent(100)), ref failure);

        if (!IsCommitted(item, header))
        {
            failure?.Throw();
            return;
        }

        ExceptionAggregation.Capture(
            () =>
            {
                if (previousSelectedIndex >= 0 && stagedSelectedIndex != previousSelectedIndex)
                {
                    // The newly inserted page never displaces an already-selected page: the
                    // selected item keeps its identity, so only its numeric index shifts.
                    NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure);
                    ApplyPresentation();
                }
                else if (previousSelectedIndex < 0 && stagedSelectedIndex >= 0)
                {
                    CommitSelection(
                        stagedSelectedIndex,
                        previousSelectedIndex,
                        previousSelectedItem,
                        force: true);
                }
                else
                {
                    ApplyPresentation();
                }
            },
            ref failure);

        failure?.Throw();
    }

    internal bool RemoveItem(TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        VerifyMutable();
        var idx = IndexOfItemControl(item);
        if (idx < 0)
        {
            return false;
        }

        RemoveItemAtCore(idx, restorePresentation: true);
        return true;
    }

    /// <summary>Detaches an owned page before its caller-initiated disposal publication begins.</summary>
    /// <param name="item">The page whose direct disposal was requested.</param>
    internal void RemoveItemForDisposal(TabItem item)
    {
        var index = IndexOfItemControl(item);

        if (index >= 0)
        {
            RemoveItemAtCore(index, restorePresentation: false);
        }
    }

    internal void RemoveItemAt(int index)
    {
        VerifyMutable();

        if ((uint) index >= (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index,
                "The removal index is outside the tab control.");
        }

        RemoveItemAtCore(index, restorePresentation: true);
    }

    private void RemoveItemAtCore(int idx, bool restorePresentation)
    {
        var item = ItemAt(idx);
        var wasSelected = idx == _selectedIndex;
        var previousSelectedIndex = _selectedIndex;
        // Captured before detachment: once the item is removed, its old index
        // may resolve to a different surviving item or be out of range.
        var previousSelectedItem = _selectedIndex >= 0 ? ItemAt(_selectedIndex) : null;
        var header = HeaderAt(idx);
        var propertyLease = _propertyOverrides.Get(item);
        var nextItems = new List<ControlBase>(ItemControlsSlot.Items);
        nextItems.RemoveAt(idx);
        var nextHeaders = new List<ControlBase>(_headers.Children);
        nextHeaders.RemoveAt(idx);
        var stagedSelectedIndex = previousSelectedIndex;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        var committed = false;
        ExceptionAggregation.Capture(
            () => OwnedControlRegistry.CommitCompound(
                () =>
                {
                    committed = true;
                    item.PropertyChanged -= OnItemPropertyChanged;
                    header.Activated -= OnHeaderActivated;
                    _ = _headersByItem.Remove(item);

                    if (wasSelected)
                    {
                        stagedSelectedIndex = SingleSelectionIndex.FindNearest(
                            Math.Min(idx, ItemControlCount - 1),
                            ItemControlCount,
                            IsEligible);
                    }
                    else if (idx < previousSelectedIndex)
                    {
                        stagedSelectedIndex = previousSelectedIndex - 1;
                    }

                    if (stagedSelectedIndex != previousSelectedIndex)
                    {
                        _selectedIndex = stagedSelectedIndex;
                        _selectionVersion++;
                    }
                },
                (ItemControlsSlot, nextItems),
                (_headers.Children.OwnedSlot, nextHeaders)),
            ref failure);

        if (!committed)
        {
            failure?.Throw();
            return;
        }

        ExceptionAggregation.Capture(
            () =>
            {
                if (wasSelected)
                {
                    CommitSelection(
                        stagedSelectedIndex,
                        previousSelectedIndex,
                        previousSelectedItem,
                        force: true);
                }
                else
                {
                    if (stagedSelectedIndex != previousSelectedIndex)
                    {
                        // Mirrors InsertItem's symmetric case: the selected page's identity is
                        // unaffected by removing an earlier page, so only its numeric index shifts
                        // silently, with no SelectionChanged.
                        NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure);
                    }

                    ApplyPresentation();
                }
            },
            ref failure);
        ExceptionAggregation.Capture(() => header.CommitSelection(false), ref failure);
        ExceptionAggregation.Capture(header.Dispose, ref failure);
        ExceptionAggregation.Capture(
            () =>
            {
                if (restorePresentation)
                {
                    _propertyOverrides.Restore(propertyLease);
                }
                else
                {
                    _propertyOverrides.Retire(propertyLease);
                }
            },
            ref failure);
        failure?.Throw();
    }

    internal void ReplaceItem(int index, TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        VerifyMutable();

        if ((uint) index >= (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index,
                "The replacement index is outside the tab control.");
        }

        var old = ItemAt(index);

        if (ReferenceEquals(old, item))
        {
            return;
        }

        var wasSelected = index == _selectedIndex;
        var previousSelectedIndex = _selectedIndex;
        var previousSelectedItem = _selectedIndex >= 0 ? ItemAt(_selectedIndex) : null;
        var oldHeader = HeaderAt(index);
        var oldPropertyLease = _propertyOverrides.Get(old);
        var requestedVisibility = item.Visibility;
        var newHeader = new TabHeader(item.HeaderText)
        {
            IsEnabled = item.IsEnabled,
            Visibility = requestedVisibility,
            Width = HeaderWidth,
        };
        newHeader.Activated += OnHeaderActivated;

        var nextItems = new List<ControlBase>(ItemControlsSlot.Items) { [index] = item };
        var nextHeaders = new List<ControlBase>(_headers.Children) { [index] = newHeader };
        var stagedSelectedIndex = previousSelectedIndex;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        var committed = false;
        ExceptionAggregation.Capture(
            () => OwnedControlRegistry.CommitCompound(
                () =>
                {
                    committed = true;
                    old.PropertyChanged -= OnItemPropertyChanged;
                    oldHeader.Activated -= OnHeaderActivated;
                    _ = _headersByItem.Remove(old);
                    _headersByItem.Add(item, newHeader);
                    _ = _propertyOverrides.Acquire(
                        item,
                        RetainedPropertyOverrides.Visibility,
                        RetainedPropertyOverrides.Width,
                        RetainedPropertyOverrides.Height);
                    item.PropertyChanged += OnItemPropertyChanged;

                    if (wasSelected)
                    {
                        stagedSelectedIndex = IsEligible(index)
                            ? index
                            : SingleSelectionIndex.FindNearest(index, ItemControlCount, IsEligible);
                        _selectedIndex = stagedSelectedIndex;
                        _selectionVersion++;
                    }
                },
                (ItemControlsSlot, nextItems),
                (_headers.Children.OwnedSlot, nextHeaders)),
            ref failure);

        if (!committed)
        {
            newHeader.Activated -= OnHeaderActivated;
            newHeader.Dispose();
            failure?.Throw();
            return;
        }

        ExceptionAggregation.Capture(() => oldHeader.CommitSelection(false), ref failure);
        ExceptionAggregation.Capture(oldHeader.Dispose, ref failure);
        ExceptionAggregation.Capture(() => WriteItemWidth(item, Length.Percent(100)), ref failure);

        if (IsCommitted(item, newHeader))
        {
            ExceptionAggregation.Capture(() => WriteItemHeight(item, Length.Percent(100)), ref failure);
        }

        if (IsCommitted(item, newHeader))
        {
            ExceptionAggregation.Capture(
                () =>
                {
                    if (wasSelected)
                    {
                        CommitSelection(
                            stagedSelectedIndex,
                            previousSelectedIndex,
                            previousSelectedItem,
                            force: true);
                    }
                    else
                    {
                        ApplyPresentation();
                    }
                },
                ref failure);
        }

        ExceptionAggregation.Capture(() => _propertyOverrides.Restore(oldPropertyLease), ref failure);
        failure?.Throw();
    }

    internal void MoveItem(int oldIndex, int newIndex)
    {
        VerifyMutable();

        if ((uint) oldIndex >= (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(oldIndex), oldIndex,
                "The source index is outside the tab control.");
        }

        if ((uint) newIndex >= (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(newIndex), newIndex,
                "The destination index is outside the tab control.");
        }

        if (oldIndex == newIndex)
        {
            return;
        }

        // A genuine reposition, not remove+insert through the public surface: the
        // item and header keep their identity, subscriptions, and presentation
        // state, and the selected item's identity never changes, so no
        // SelectionChanged fires — only the numeric SelectedIndex may shift.
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        var nextItems = new List<ControlBase>(ItemControlsSlot.Items);
        var item = nextItems[oldIndex];
        nextItems.RemoveAt(oldIndex);
        nextItems.Insert(newIndex, item);
        var nextHeaders = new List<ControlBase>(_headers.Children);
        var header = nextHeaders[oldIndex];
        nextHeaders.RemoveAt(oldIndex);
        nextHeaders.Insert(newIndex, header);
        var previousSelectedIndex = _selectedIndex;
        var stagedSelectedIndex = previousSelectedIndex;

        if (previousSelectedIndex == oldIndex)
        {
            stagedSelectedIndex = newIndex;
        }
        else if (oldIndex < previousSelectedIndex && previousSelectedIndex <= newIndex)
        {
            stagedSelectedIndex--;
        }
        else if (newIndex <= previousSelectedIndex && previousSelectedIndex < oldIndex)
        {
            stagedSelectedIndex++;
        }

        ExceptionAggregation.Capture(
            () => OwnedControlRegistry.CommitCompound(
                () =>
                {
                    if (stagedSelectedIndex != previousSelectedIndex)
                    {
                        _selectedIndex = stagedSelectedIndex;
                        _selectionVersion++;
                    }
                },
                (ItemControlsSlot, nextItems),
                (_headers.Children.OwnedSlot, nextHeaders)),
            ref failure);

        if (stagedSelectedIndex != previousSelectedIndex)
        {
            ExceptionAggregation.Capture(
                () => NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure),
                ref failure);
        }

        ExceptionAggregation.Capture(ApplyPresentation, ref failure);
        failure?.Throw();
    }

    /// <summary>Requests closure of a closeable tab and removes it when not cancelled.</summary>
    /// <param name="item">The owned closeable tab page.</param>
    /// <returns><see langword="true"/> when the request completes with the tab no longer owned.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item is not owned by this control.</exception>
    /// <exception cref="InvalidOperationException">The control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool RequestClose(TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        VerifyMutable();

        if (IndexOfItemControl(item) < 0)
        {
            throw new ArgumentException("The tab must be owned by this control.", nameof(item));
        }

        if (!item.IsClosable)
        {
            return false;
        }

        if (!_closeRequestsInFlight.Add(item))
        {
            return false;
        }

        var request = new TabCloseRequestedEventArgs(item);
        var closeRequestVersion = unchecked(++_closeRequestVersion);

        try
        {
            RaiseCloseRequested(request, closeRequestVersion);
        }
        finally
        {
            _ = _closeRequestsInFlight.Remove(item);
        }

        return IndexOfItemControl(item) < 0 || (!request.Cancel && RemoveItem(item));
    }

    private void RaiseCloseRequested(TabCloseRequestedEventArgs eventArgs, long closeRequestVersion)
    {
        var handlers = CloseRequested;

        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList())
        {
            if (eventArgs.Cancel ||
                closeRequestVersion != _closeRequestVersion ||
                IndexOfItemControl(eventArgs.Item) < 0)
            {
                break;
            }

            var handler = (EventHandler<TabCloseRequestedEventArgs>) subscriber;
            handler(this, eventArgs);
        }
    }

    internal void ClearItems() => ClearItems(disposing: false);

    private void ClearItems(bool disposing)
    {
        VerifyMutable();

        if (ItemControlCount == 0)
        {
            return;
        }

        var items = new TabItem[ItemControlCount];
        var propertyLeases = new RetainedPropertyOverrideLease[ItemControlCount];
        var headers = new TabHeader[_headers.Children.Count];
        var previousSelectedIndex = _selectedIndex;
        var previousSelectedItem = previousSelectedIndex >= 0 ? ItemAt(previousSelectedIndex) : null;

        for (var index = 0; index < ItemControlCount; index++)
        {
            var item = ItemAt(index);
            var header = HeaderAt(index);
            items[index] = item;
            propertyLeases[index] = _propertyOverrides.Get(item);
            headers[index] = header;
        }

        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        var committed = false;
        ExceptionAggregation.Capture(CommitSnapshots, ref failure);

        if (!committed)
        {
            failure?.Throw();
            return;
        }

        if (!disposing)
        {
            ExceptionAggregation.Capture(
                () => CommitSelection(-1, previousSelectedIndex, previousSelectedItem, force: true),
                ref failure);
        }

        foreach (var header in headers)
        {
            ExceptionAggregation.Capture(() => header.CommitSelection(false), ref failure);
            ExceptionAggregation.Capture(header.Dispose, ref failure);
        }

        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];

            if (!disposing)
            {
                var propertyLease = propertyLeases[index];
                ExceptionAggregation.Capture(() => _propertyOverrides.Restore(propertyLease), ref failure);
            }

            if (disposing)
            {
                ExceptionAggregation.Capture(item.DisposeAfterUnavailable, ref failure);
            }
        }

        failure?.Throw();
        return;

        void CommitSnapshots()
        {
            if (disposing)
            {
                OwnedControlRegistry.CommitCompoundForOwnerDisposal(
                    SynchronizeState,
                    (ItemControlsSlot, Array.Empty<ControlBase>()),
                    (_headers.Children.OwnedSlot, Array.Empty<ControlBase>()));
            }
            else
            {
                OwnedControlRegistry.CommitCompound(
                    SynchronizeState,
                    (ItemControlsSlot, Array.Empty<ControlBase>()),
                    (_headers.Children.OwnedSlot, Array.Empty<ControlBase>()));
            }
        }

        void SynchronizeState()
        {
            committed = true;

            for (var index = 0; index < items.Length; index++)
            {
                var item = items[index];
                var header = headers[index];
                item.PropertyChanged -= OnItemPropertyChanged;
                header.Activated -= OnHeaderActivated;
            }

            _headersByItem.Clear();
            _selectedIndex = -1;
            _selectionVersion++;
        }
    }

    /// <inheritdoc/>
    private protected override void OnItemsControlDisposing()
    {
        ClearItems(disposing: true);
        _propertyOverrides.Dispose();
    }

    [Pure]
    private bool IsCommitted(TabItem item, TabHeader header) =>
        IndexOfItemControl(item) >= 0 &&
        _headersByItem.TryGetValue(item, out var currentHeader) &&
        ReferenceEquals(currentHeader, header);

    [Pure]
    private bool HasVisibleHeaderAfter(int index)
    {
        for (var candidate = index + 1; candidate < _headers.Children.Count; candidate++)
        {
            if (HeaderAt(candidate).Visibility == Visibility.Visible)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            SelectionChanged = null;
            CloseRequested = null;
        }
    }

    private void OnHeaderActivated(object? sender, ActivationEventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is not TabHeader header)
        {
            return;
        }

        var index = _headers.Children.IndexOf(header);

        if (index >= 0 && IsEligible(index))
        {
            Select(index);
        }
    }

    private void Select(int index)
    {
        VerifyMutable();

        var previousItem = _selectedIndex >= 0 ? ItemAt(_selectedIndex) : null;
        CommitSelection(index, _selectedIndex, previousItem);
    }

    private void CommitSelection(
        int index,
        int previousIndex,
        TabItem? previousItem,
        bool force = false)
    {
        if (index >= 0 && !IsEligible(index))
        {
            throw new InvalidOperationException("An unavailable tab page cannot be selected.");
        }

        if (!force && _selectedIndex == index && previousIndex == index)
        {
            return;
        }

        if (_selectedIndex >= 0 && _selectedIndex < ItemControlCount)
        {
            HeaderAt(_selectedIndex).CommitSelection(false);
        }

        _selectedIndex = index;
        var selectionVersion = ++_selectionVersion;

        if (_selectedIndex >= 0)
        {
            HeaderAt(_selectedIndex).CommitSelection(true);
        }

        if (selectionVersion != _selectionVersion)
        {
            return;
        }

        ApplyPresentation();

        if (selectionVersion != _selectionVersion)
        {
            return;
        }

        if (HeaderOverflowPolicy == TabHeaderOverflowPolicy.Scroll && _selectedIndex >= 0)
        {
            _ = _headers.BringIntoView(HeaderAt(_selectedIndex));
        }

        var currentItem = _selectedIndex >= 0 ? ItemAt(_selectedIndex) : null;

        NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure);

        if (selectionVersion != _selectionVersion)
        {
            return;
        }

        NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Measure);

        if (selectionVersion != _selectionVersion)
        {
            return;
        }

        SelectionChanged?.Invoke(
            this,
            new TabSelectionChangedEventArgs(previousIndex, index, previousItem, currentItem));
    }

    [Pure]
    internal int IndexOfItem(TabItem item)
    {
        for (var index = 0; index < ItemControlCount; index++)
        {
            if (ReferenceEquals(ItemAt(index), item))
            {
                return index;
            }
        }

        return -1;
    }

    private void ApplyPresentation()
    {
        for (var index = 0; index < ItemControlCount; index++)
        {
            var item = ItemAt(index);
            var visibility = index == _selectedIndex && RequestedVisibility(item) == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (visibility == Visibility.Collapsed)
            {
                item.ClearPresentedContent();
            }

            _propertyOverrides.Get(item).SetLive(RetainedControlProperty.Visibility, visibility);
        }
    }

    private void OnAuthoredPropertyRequest(ControlBase control, RetainedControlProperty property)
    {
        if (property != RetainedControlProperty.Visibility || control is not TabItem item)
        {
            return;
        }

        var index = IndexOfItemControl(item);

        if (index < 0)
        {
            return;
        }

        HeaderAt(index).Visibility = RequestedVisibility(item);
        RepairSelectionAfterAvailabilityChange(index);
        ApplyPresentation();
    }

    private bool TryNavigate(Code code)
    {
        var index = -1;

        if (code == Code.Left)
        {
            index = SingleSelectionIndex.FindWrapped(_selectedIndex, -1, ItemControlCount, IsEligible);
        }
        else if (code == Code.Right)
        {
            index = SingleSelectionIndex.FindWrapped(_selectedIndex, 1, ItemControlCount, IsEligible);
        }
        else if (code == Code.Home)
        {
            index = SingleSelectionIndex.FindLinear(0, 1, ItemControlCount, IsEligible);
        }
        else if (code == Code.End)
        {
            index = SingleSelectionIndex.FindLinear(ItemControlCount - 1, -1, ItemControlCount, IsEligible);
        }

        return TrySelect(index);
    }

    private bool TrySelect(int index)
    {
        if (index < 0)
        {
            return false;
        }

        Select(index);
        return true;
    }

    [Pure]
    private bool IsEligible(int index)
    {
        var item = ItemAt(index);
        return item.IsEnabled && RequestedVisibility(item) == Visibility.Visible;
    }

    [Pure]
    private Visibility RequestedVisibility(TabItem item) =>
        _propertyOverrides.Get(item).GetAuthored<Visibility>(RetainedControlProperty.Visibility);

    private void RepairSelectionAfterAvailabilityChange(int index)
    {
        if (index == _selectedIndex && !IsEligible(index))
        {
            SelectNearest(index);
        }
        else if (_selectedIndex < 0)
        {
            var first = SingleSelectionIndex.FindLinear(0, 1, ItemControlCount, IsEligible);

            if (first >= 0)
            {
                Select(first);
            }
        }
    }

    private void WriteItemWidth(TabItem item, Length value)
        => _propertyOverrides.Get(item).SetLive(RetainedControlProperty.Width, value);

    private void WriteItemHeight(TabItem item, Length value)
        => _propertyOverrides.Get(item).SetLive(RetainedControlProperty.Height, value);

    private void SelectNearest(int index)
    {
        var target = SingleSelectionIndex.FindNearest(index, ItemControlCount, IsEligible);
        Select(target);
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (sender is not TabItem item)
        {
            return;
        }

        if (eventArgs.PropertyName is not nameof(TabItem.HeaderText) and not nameof(Visibility) and not nameof(IsEnabled))
        {
            return;
        }

        var index = IndexOfItemControl(item);

        if (index < 0)
        {
            return;
        }

        var header = HeaderAt(index);

        if (eventArgs.PropertyName == nameof(TabItem.HeaderText))
        {
            header.Text = item.HeaderText;
            return;
        }

        if (eventArgs.PropertyName == nameof(Visibility))
        {
            if (_propertyOverrides.Get(item).IsWriting(RetainedControlProperty.Visibility))
            {
                return;
            }

            header.Visibility = RequestedVisibility(item);
        }
        else if (eventArgs.PropertyName == nameof(IsEnabled))
        {
            header.IsEnabled = item.IsEnabled;
        }
        else
        {
            return;
        }

        RepairSelectionAfterAvailabilityChange(index);

        ApplyPresentation();
    }

    private void ConfigureHeaderOverflow()
    {
        _headers.AutoScroll = HeaderOverflowPolicy == TabHeaderOverflowPolicy.Scroll;
        _headers.ScrollBars = _headers.AutoScroll ? ScrollBars.Horizontal : ScrollBars.None;
        _headers.ShowScrollBars = ShowScrollBars.Never;
        _ = RevealSelectedHeader();
    }

    private bool RevealSelectedHeader()
    {
        if (HeaderOverflowPolicy != TabHeaderOverflowPolicy.Scroll ||
            _selectedIndex < 0 ||
            _selectedIndex >= _headers.Children.Count)
        {
            return false;
        }

        var before = _headers.HorizontalOffset;
        _ = _headers.BringIntoView(HeaderAt(_selectedIndex));
        return before != _headers.HorizontalOffset;
    }

}
