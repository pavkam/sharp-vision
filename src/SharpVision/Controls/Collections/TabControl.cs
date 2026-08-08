// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using System.ComponentModel;

using SharpVision.Terminal.Input;

using LayoutStack = Layout.Stack;

/// <summary>Arranges typed tab pages and coordinates header rendering and keyboard selection.</summary>
[PublicAPI]
public sealed class TabControl: ItemsControl
{
    private const int _headerRowHeight = 1;
    private const int _selectionIndicatorRowHeight = 1;
    private const int _headerStripHeight = _headerRowHeight + _selectionIndicatorRowHeight;
    private const int _headerSeparatorWidth = 1;
    private readonly Dictionary<TabItem, TabItemPresentation> _requestedPresentations = [];
    private readonly LayoutStack _headers;
    private readonly LayoutStack _stack;
    private bool _updatingPresentation;
    private TabItem? _writingItem;
    private Visibility _writingVisibility;
    private int _selectedIndex = -1;

    /// <summary>Initializes an empty tab control with typed managed pages.</summary>
    public TabControl()
    {
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
        Focusable = true;
        TabStop = true;
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
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);

            for (var index = 0; index < _headers.Children.Count; index++)
            {
                _headers.Children[index].Width = value;
            }
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
            value.ValidateDefined();

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
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    }

    /// <summary>Gets the resolved tab-strip style: the local <see cref="Style"/> when assigned,
    /// otherwise the active theme's "tabControl" section over the code-owned default.</summary>
    public TabControlStyle ActualStyle => TabControlStyle.Definition.Resolve(Style, Theme);

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
        ArrangeChild(
            _headers,
            new Rect(bounds.X, bounds.Y, bounds.Width, headerHeight),
            ResolvedAxes.Both);
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
        if (Bounds.Width == 0 || Bounds.Height < _headerStripHeight)
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

        for (var index = 0; index < _headers.Children.Count - 1; index++)
        {
            var header = HeaderAt(index);

            if (header.Visibility == Visibility.Visible && header.Bounds.Right < Bounds.Right)
            {
                canvas.DrawRune(
                    divider,
                    new Point(header.Bounds.Right, Bounds.Y),
                    dividerStyle,
                    BackgroundMode.Transparent);
            }
        }

        for (var lx = Bounds.X; lx < Bounds.Right; lx++)
        {
            canvas.DrawRune(underline, new Point(lx, Bounds.Y + _headerRowHeight), indicatorStyle, BackgroundMode.Transparent);
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        base.OnEvent(eventArgs);

        if (eventArgs.Handled)
        {
            return;
        }

        if (eventArgs is not KeyEventArgs { Stroke.Action: KeyAction.Press } key)
        {
            return;
        }

        eventArgs.Handled = key.Stroke.Code == Code.Delete
            ? _selectedIndex >= 0 && RequestClose(ItemAt(_selectedIndex))
            : TryNavigate(key.Stroke.Code);
    }

    internal int ItemCount => ItemControlCount;
    internal TabItem ItemAt(int index) => (TabItem) GetItemControl(index);
    internal TabHeader HeaderAt(int index) => (TabHeader) _headers.Children[index];

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
            Enabled = item.Enabled,
            Visibility = requestedPresentation.Visibility,
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

        _requestedPresentations.Add(item, requestedPresentation);
        item.PropertyChanged += OnItemPropertyChanged;
        item.Width = Length.Percent(100);
        item.Height = Length.Percent(100);
        header.Width = HeaderWidth;

        // The newly inserted page never displaces an already-selected page: the
        // selected item keeps its identity, so only its numeric index shifts.
        if (_selectedIndex >= index)
        {
            _selectedIndex++;
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
        RestorePresentation(item);
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
                NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure);
            }

            ApplyPresentation();
        }
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
            Enabled = item.Enabled,
            Visibility = requestedPresentation.Visibility,
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
        RestorePresentation(old);
        oldHeader.CommitSelection(false);
        oldHeader.Dispose();

        _requestedPresentations.Add(item, requestedPresentation);
        item.PropertyChanged += OnItemPropertyChanged;
        item.Width = Length.Percent(100);
        item.Height = Length.Percent(100);
        newHeader.Width = HeaderWidth;

        if (wasSelected)
        {
            _selectedIndex = -1;
            var target = IsEligible(index) ? index : SingleSelectionIndex.FindNearest(index, ItemControlCount, IsEligible);
            CommitSelection(target, previousSelectedIndex, previousSelectedItem);
        }
        else
        {
            ApplyPresentation();
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
        RemoveItemControlAt(oldIndex);
        _headers.Children.RemoveAt(oldIndex);
        InsertItemControl(newIndex, item);
        _headers.Children.Insert(newIndex, header);

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
            NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure);
        }

        ApplyPresentation();
    }

    /// <summary>Requests closure of a closeable tab and removes it when not cancelled.</summary>
    /// <param name="item">The owned closeable tab page.</param>
    /// <returns><see langword="true"/> when the tab was removed.</returns>
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

        if (!item.Closable)
        {
            return false;
        }

        var request = new TabCloseRequestedEventArgs(item);
        CloseRequested?.Invoke(this, request);
        return !request.Cancel && RemoveItem(item);
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
            RestorePresentation(item);
            header.CommitSelection(false);
            headers[index] = header;
        }

        ClearItemControls();
        _headers.Children.Clear();
        CommitSelectionAfterMutation(-1, previousSelectedIndex, previousSelectedItem);

        foreach (var header in headers)
        {
            header.Dispose();
        }
    }

    // Runs before the item is detached so the restored values are the ones
    // observed by the caller immediately afterward, not an intermediate state
    // still overwritten by this control's private presentation policy. Without
    // this, a detached item kept Width/Height pinned to Percent(100) and
    // Visibility pinned to whatever page happened to be selected last — and
    // because AddItem captures an item's *current* Visibility as its next
    // owner's requested visibility, a Collapsed leftover made the item
    // permanently unselectable in any later TabControl.
    private void RestorePresentation(TabItem item)
    {
        if (!_requestedPresentations.Remove(item, out var presentation))
        {
            return;
        }

        item.Visibility = presentation.Visibility;
        item.Width = presentation.Width;
        item.Height = presentation.Height;
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

        if (_selectedIndex >= 0)
        {
            HeaderAt(_selectedIndex).CommitSelection(true);
        }

        ApplyPresentation();
        if (HeaderOverflowPolicy == TabHeaderOverflowPolicy.Scroll && _selectedIndex >= 0)
        {
            _ = _headers.BringIntoView(HeaderAt(_selectedIndex));
        }

        var currentItem = _selectedIndex >= 0 ? ItemAt(_selectedIndex) : null;

        NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure);
        NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Measure);
        SelectionChanged?.Invoke(
            this,
            new TabSelectionChangedEventArgs(previousIndex, index, previousItem, currentItem));
    }

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
        if (_updatingPresentation)
        {
            return;
        }

        _updatingPresentation = true;

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

                _writingItem = item;
                _writingVisibility = visibility;
                item.Visibility = visibility;
            }
        }
        finally
        {
            _writingItem = null;
            _updatingPresentation = false;
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

    private bool IsEligible(int index)
    {
        var item = ItemAt(index);
        return item.Enabled && RequestedVisibility(item) == Visibility.Visible;
    }

    private Visibility RequestedVisibility(TabItem item) =>
        _requestedPresentations.TryGetValue(item, out var presentation) ? presentation.Visibility : item.Visibility;

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

        if (eventArgs.PropertyName is not nameof(TabItem.HeaderText) and not nameof(Visibility) and not nameof(Enabled))
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
            // _updatingPresentation only says a write from ApplyPresentation is in flight, not that
            // this particular notification is that write: a consumer can reassign Visibility from
            // inside this very notification, which reenters here before ApplyPresentation unwinds.
            // Attribute the write by comparing against what ApplyPresentation actually wrote for this
            // item - a mismatch means a consumer's reentrant request, which must be honored.
            if (_updatingPresentation && ReferenceEquals(_writingItem, item) && item.Visibility == _writingVisibility)
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
        else if (eventArgs.PropertyName == nameof(Enabled))
        {
            header.Enabled = item.Enabled;
        }
        else
        {
            return;
        }

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

        ApplyPresentation();
    }

    private void ConfigureHeaderOverflow()
    {
        _headers.AutoScroll = HeaderOverflowPolicy == TabHeaderOverflowPolicy.Scroll;
        _headers.ScrollBars = _headers.AutoScroll ? ScrollBars.Horizontal : ScrollBars.None;
        _headers.ShowScrollBars = ShowScrollBars.Never;
    }

}
