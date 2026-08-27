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
    private readonly Dictionary<TabItem, TabItemPresentation> _requestedPresentations = [];
    private readonly HashSet<TabItem> _closeRequestsInFlight = [];
    private readonly StyleSlot<TabControlStyle> _style;
    private readonly LayoutStack _headers;
    private readonly LayoutStack _stack;
    private bool _isWritingItemHeight;
    private bool _isWritingItemWidth;
    private int _presentationDepth;
    private TabItem? _writingItem;
    private Length _writingHeight;
    private Length _writingWidth;
    private Visibility _writingVisibility;
    private int _selectedIndex = -1;
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
        Items = new TabItemCollection(this);
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.Continue;
        ConfigureHeaderOverflow();
    }

    /// <summary>Raised after the selected tab index changes.</summary>
    public event EventHandler<TabSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised before a closeable tab is removed; handlers may cancel the request.</summary>
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
            bounds.Y + contentOffset,
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

        var requestedPresentation = new TabItemPresentation(item.Visibility, item.Width, item.Height);
        var header = new TabHeader(item.HeaderText)
        {
            IsEnabled = item.IsEnabled,
            Visibility = requestedPresentation.Visibility,
            Width = HeaderWidth,
        };
        header.Activated += OnHeaderActivated;
        InsertItemControl(index, item);

        try
        {
            _headers.Children.Insert(index, header);
        }
        catch
        {
            _ = RemoveItemControl(item);
            header.Dispose();
            throw;
        }

        _headersByItem.Add(item, header);
        _requestedPresentations.Add(item, requestedPresentation);
        item.PropertyChanged += OnItemPropertyChanged;
        WriteItemWidth(item, Length.Percent(100));

        if (!IsCommitted(item, header))
        {
            return;
        }

        WriteItemHeight(item, Length.Percent(100));

        if (!IsCommitted(item, header))
        {
            return;
        }

        // The newly inserted page never displaces an already-selected page: the
        // selected item keeps its identity, so only its numeric index shifts.
        if (_selectedIndex >= index)
        {
            _selectedIndex++;
            _selectionVersion++;
            NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure);
        }

        if (_selectedIndex < 0 && SingleSelectionIndex.FindLinear(0, 1, ItemControlCount, IsEligible) is var first and >= 0)
        {
            Select(first);
        }
        else
        {
            ApplyPresentation();
        }
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

        RemoveItemAtCore(idx);
        return true;
    }

    /// <summary>Detaches an owned page before its caller-initiated disposal publication begins.</summary>
    /// <param name="item">The page whose direct disposal was requested.</param>
    internal void RemoveItemForDisposal(TabItem item) => _ = RemoveItem(item);

    internal void RemoveItemAt(int index)
    {
        VerifyMutable();

        if ((uint) index >= (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index,
                "The removal index is outside the tab control.");
        }

        RemoveItemAtCore(index);
    }

    private void RemoveItemAtCore(int idx)
    {
        var item = ItemAt(idx);
        var wasSelected = idx == _selectedIndex;
        var previousSelectedIndex = _selectedIndex;
        // Captured before detachment: once the item is removed, its old index
        // may resolve to a different surviving item or be out of range.
        var previousSelectedItem = _selectedIndex >= 0 ? ItemAt(_selectedIndex) : null;
        var header = HeaderAt(idx);
        item.PropertyChanged -= OnItemPropertyChanged;
        header.Activated -= OnHeaderActivated;
        _ = _headersByItem.Remove(item);
        _ = _requestedPresentations.Remove(item, out var requestedPresentation);
        header.CommitSelection(false);
        _ = RemoveItemControl(item);
        _ = _headers.Children.Remove(header);
        header.Dispose();

        if (wasSelected)
        {
            var target = SingleSelectionIndex.FindNearest(Math.Min(idx, ItemControlCount - 1), ItemControlCount, IsEligible);
            CommitSelectionAfterMutation(target, previousSelectedIndex, previousSelectedItem);
        }
        else
        {
            if (idx < _selectedIndex)
            {
                // Mirrors InsertItem's symmetric case: the selected page's identity is
                // unaffected by removing an earlier page, so only its numeric index shifts
                // silently, with no SelectionChanged.
                _selectedIndex--;
                _selectionVersion++;
                NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure);
            }

            ApplyPresentation();
        }

        RestorePresentation(item, requestedPresentation);
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
        var requestedPresentation = new TabItemPresentation(item.Visibility, item.Width, item.Height);
        var newHeader = new TabHeader(item.HeaderText)
        {
            IsEnabled = item.IsEnabled,
            Visibility = requestedPresentation.Visibility,
            Width = HeaderWidth,
        };
        newHeader.Activated += OnHeaderActivated;

        // The item host validates and atomically detaches the old item while
        // attaching the new one; only after that succeeds is the parallel
        // header strip touched, so a rejected candidate never desynchronizes it.
        ReplaceItemControl(index, item);

        try
        {
            _headers.Children[index] = newHeader;
        }
        catch
        {
            ReplaceItemControl(index, old);
            newHeader.Dispose();
            throw;
        }

        old.PropertyChanged -= OnItemPropertyChanged;
        oldHeader.Activated -= OnHeaderActivated;
        _ = _headersByItem.Remove(old);
        _ = _requestedPresentations.Remove(old, out var oldPresentation);
        oldHeader.CommitSelection(false);
        oldHeader.Dispose();

        _headersByItem.Add(item, newHeader);
        _requestedPresentations.Add(item, requestedPresentation);
        item.PropertyChanged += OnItemPropertyChanged;
        try
        {
            WriteItemWidth(item, Length.Percent(100));

            if (!IsCommitted(item, newHeader))
            {
                return;
            }

            WriteItemHeight(item, Length.Percent(100));

            if (!IsCommitted(item, newHeader))
            {
                return;
            }

            if (wasSelected)
            {
                _selectedIndex = -1;
                _selectionVersion++;
                var target = IsEligible(index)
                    ? index
                    : SingleSelectionIndex.FindNearest(index, ItemControlCount, IsEligible);
                CommitSelection(target, previousSelectedIndex, previousSelectedItem);
            }
            else
            {
                ApplyPresentation();
            }
        }
        finally
        {
            RestorePresentation(old, oldPresentation);
        }
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

        var item = ItemAt(oldIndex);
        var header = HeaderAt(oldIndex);

        // A genuine reposition, not remove+insert through the public surface: the
        // item and header keep their identity, subscriptions, and presentation
        // state, and the selected item's identity never changes, so no
        // SelectionChanged fires — only the numeric SelectedIndex may shift.
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(() => MoveItemControl(oldIndex, newIndex), ref failure);
        ExceptionAggregation.Capture(() => _headers.Children.Move(oldIndex, newIndex), ref failure);

        var previousSelectedIndex = _selectedIndex;

        if (_selectedIndex == oldIndex)
        {
            _selectedIndex = newIndex;
        }
        else if (oldIndex < _selectedIndex && _selectedIndex <= newIndex)
        {
            _selectedIndex--;
        }
        else if (newIndex <= _selectedIndex && _selectedIndex < oldIndex)
        {
            _selectedIndex++;
        }

        if (_selectedIndex != previousSelectedIndex)
        {
            _selectionVersion++;
            NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure);
        }

        ApplyPresentation();
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

        try
        {
            CloseRequested?.Invoke(this, request);
        }
        finally
        {
            _ = _closeRequestsInFlight.Remove(item);
        }

        return IndexOfItemControl(item) < 0 || (!request.Cancel && RemoveItem(item));
    }

    internal void ClearItems()
    {
        VerifyMutable();
        var headers = new TabHeader[_headers.Children.Count];
        var previousSelectedIndex = _selectedIndex;
        var previousSelectedItem = previousSelectedIndex >= 0 ? ItemAt(previousSelectedIndex) : null;

        for (var index = 0; index < ItemControlCount; index++)
        {
            var item = ItemAt(index);
            var header = HeaderAt(index);
            item.PropertyChanged -= OnItemPropertyChanged;
            header.Activated -= OnHeaderActivated;
            _ = _headersByItem.Remove(item);
            header.CommitSelection(false);
            headers[index] = header;
        }

        var presentations = _requestedPresentations.ToArray();
        _requestedPresentations.Clear();
        ClearItemControls();
        _headers.Children.Clear();
        CommitSelectionAfterMutation(-1, previousSelectedIndex, previousSelectedItem);

        foreach (var header in headers)
        {
            header.Dispose();
        }

        foreach (var (item, presentation) in presentations)
        {
            RestorePresentation(item, presentation);
        }
    }

    // Restoration runs only after ownership, headers, and selection have reached their final
    // snapshot. Caller PropertyChanged handlers can therefore mutate the collection without
    // resuming an obsolete outer transaction against a detached item or disposed header.
    private static void RestorePresentation(TabItem item, TabItemPresentation presentation)
    {
        item.Visibility = presentation.Visibility;
        item.Width = presentation.Width;
        item.Height = presentation.Height;
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

    private void CommitSelectionAfterMutation(int index, int previousIndex, TabItem? previousItem)
    {
        VerifyMutable();
        _selectedIndex = -1;
        _selectionVersion++;
        CommitSelection(index, previousIndex, previousItem);
    }

    private void CommitSelection(int index, int previousIndex, TabItem? previousItem)
    {

        if (index >= 0 && !IsEligible(index))
        {
            throw new InvalidOperationException("An unavailable tab page cannot be selected.");
        }

        if (_selectedIndex == index && previousIndex == index)
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
        _presentationDepth++;
        var previousWritingItem = _writingItem;
        var previousWritingVisibility = _writingVisibility;

        try
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

                var parentWritingItem = _writingItem;
                var parentWritingVisibility = _writingVisibility;

                try
                {
                    _writingItem = item;
                    _writingVisibility = visibility;
                    item.Visibility = visibility;
                }
                finally
                {
                    _writingItem = parentWritingItem;
                    _writingVisibility = parentWritingVisibility;
                }
            }
        }
        finally
        {
            _writingItem = previousWritingItem;
            _writingVisibility = previousWritingVisibility;
            _presentationDepth--;
        }
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
        _requestedPresentations.TryGetValue(item, out var presentation) ? presentation.Visibility : item.Visibility;

    /// <summary>Captures an owned page's authored width while preserving the fill-page presentation.</summary>
    /// <param name="item">The page receiving the public request.</param>
    /// <param name="value">The requested width.</param>
    /// <returns>True when this owner consumed the request; otherwise, false.</returns>
    internal bool TryHandleItemWidthRequest(TabItem item, Length value)
    {
        if (_isWritingItemWidth && ReferenceEquals(_writingItem, item) && value == _writingWidth)
        {
            return false;
        }

        if (!_requestedPresentations.TryGetValue(item, out var presentation) || IndexOfItemControl(item) < 0)
        {
            return false;
        }

        _requestedPresentations[item] = presentation.WithWidth(value);
        return true;
    }

    /// <summary>Captures an owned page's authored height while preserving the fill-page presentation.</summary>
    /// <param name="item">The page receiving the public request.</param>
    /// <param name="value">The requested height.</param>
    /// <returns>True when this owner consumed the request; otherwise, false.</returns>
    internal bool TryHandleItemHeightRequest(TabItem item, Length value)
    {
        if (_isWritingItemHeight && ReferenceEquals(_writingItem, item) && value == _writingHeight)
        {
            return false;
        }

        if (!_requestedPresentations.TryGetValue(item, out var presentation) || IndexOfItemControl(item) < 0)
        {
            return false;
        }

        _requestedPresentations[item] = presentation.WithHeight(value);
        return true;
    }

    /// <summary>Captures an owned page's authored visibility even when its private live
    /// presentation already has the same value.</summary>
    /// <param name="item">The page receiving the public request.</param>
    /// <param name="value">The requested visibility.</param>
    /// <returns>True when this owner consumed the request; otherwise, false.</returns>
    internal bool TryHandleItemVisibilityRequest(TabItem item, Visibility value)
    {
        if (_presentationDepth > 0 &&
            ReferenceEquals(_writingItem, item) &&
            value == _writingVisibility)
        {
            return false;
        }

        if (!_requestedPresentations.TryGetValue(item, out var presentation))
        {
            return false;
        }

        var index = IndexOfItemControl(item);

        if (index < 0)
        {
            return false;
        }

        _requestedPresentations[item] = presentation.WithVisibility(value);
        HeaderAt(index).Visibility = value;
        RepairSelectionAfterAvailabilityChange(index);
        ApplyPresentation();
        return true;
    }

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
    {
        var previousItem = _writingItem;
        var previousWriting = _isWritingItemWidth;
        var previousValue = _writingWidth;
        _writingItem = item;
        _isWritingItemWidth = true;
        _writingWidth = value;

        try
        {
            item.Width = value;
        }
        finally
        {
            _writingItem = previousItem;
            _isWritingItemWidth = previousWriting;
            _writingWidth = previousValue;
        }
    }

    private void WriteItemHeight(TabItem item, Length value)
    {
        var previousItem = _writingItem;
        var previousWriting = _isWritingItemHeight;
        var previousValue = _writingHeight;
        _writingItem = item;
        _isWritingItemHeight = true;
        _writingHeight = value;

        try
        {
            item.Height = value;
        }
        finally
        {
            _writingItem = previousItem;
            _isWritingItemHeight = previousWriting;
            _writingHeight = previousValue;
        }
    }

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
            // _presentationDepth only says a write from ApplyPresentation is in flight, not that
            // this particular notification is that write: a consumer can reassign Visibility from
            // inside this very notification, which reenters here before ApplyPresentation unwinds.
            // Attribute the write by comparing against what ApplyPresentation actually wrote for this
            // item - a mismatch means a consumer's reentrant request, which must be honored.
            if (_presentationDepth > 0 && ReferenceEquals(_writingItem, item) && item.Visibility == _writingVisibility)
            {
                return;
            }

            if (_requestedPresentations.TryGetValue(item, out var presentation))
            {
                _requestedPresentations[item] =
                    new TabItemPresentation(item.Visibility, presentation.Width, presentation.Height);
            }

            header.Visibility = item.Visibility;
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
