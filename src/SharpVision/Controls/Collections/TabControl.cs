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
    private int _selectedIndex = -1;
    private Rune? _dividerGlyph;
    private Rune? _underlineGlyph;

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
            EnumValidation.ValidateDefined(value);

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

    /// <summary>Gets or sets the local tab-divider glyph.</summary>
    /// <exception cref="ArgumentException">The value is a terminal control or is not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached tab control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tab control is disposed.</exception>
    public Rune DividerGlyph
    {
        get => _dividerGlyph ?? ControlGlyphs.Separators.TabDivider.Value;
        set => SetOptionalGlyph(ref _dividerGlyph, value, nameof(DividerGlyph));
    }

    /// <summary>Gets or sets the local selected-tab underline glyph.</summary>
    /// <exception cref="ArgumentException">The value is a terminal control or is not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached tab control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tab control is disposed.</exception>
    public Rune UnderlineGlyph
    {
        get => _underlineGlyph ?? ControlGlyphs.Separators.TabUnderline.Value;
        set => SetOptionalGlyph(ref _underlineGlyph, value, nameof(UnderlineGlyph));
    }

    /// <summary>Gets or sets the foreground of header dividers.</summary>
    /// <exception cref="ArgumentException">The value is transparent.</exception>
    /// <exception cref="InvalidOperationException">The attached tab control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tab control is disposed.</exception>
    public Color? DividerColor
    {
        get;
        set
        {
            ColorValidation.ValidatePaint(value, nameof(value));
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Gets or sets the foreground of the selection indicator.</summary>
    /// <exception cref="ArgumentException">The value is transparent.</exception>
    /// <exception cref="InvalidOperationException">The attached tab control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tab control is disposed.</exception>
    public Color? SelectionIndicatorColor
    {
        get;
        set
        {
            ColorValidation.ValidatePaint(value, nameof(value));
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Clears both local tab glyphs to the code-owned defaults.</summary>
    /// <exception cref="InvalidOperationException">The attached tab control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The tab control is disposed.</exception>
    public void ResetGlyphs()
    {
        VerifyMutable();
        _ = ResetOptionalGlyph(ref _dividerGlyph, nameof(DividerGlyph));
        _ = ResetOptionalGlyph(ref _underlineGlyph, nameof(UnderlineGlyph));
    }

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

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height < _headerStripHeight)
        {
            return;
        }

        var inherited = NormalStyle;
        var dividerStyle = inherited.WithForeground(DividerColor ?? Theme?.ResolveColor(ThemeColor.ControlBorder) ?? Color.Default);
        var indicatorStyle = inherited.WithForeground(SelectionIndicatorColor ?? Theme?.ResolveColor(ThemeColor.Accent) ?? Color.Default);
        var separators = ControlGlyphs.Separators;
        var divider = CellGlyphResolver.Resolve(DividerGlyph, separators.TabDivider.Fallback, CellPolicy.AmbiguousWidth);
        var underline = CellGlyphResolver.Resolve(UnderlineGlyph, separators.TabUnderline.Fallback, CellPolicy.AmbiguousWidth);

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

    internal void AddItem(TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        VerifyMutable();
        var requestedPresentation = new TabItemPresentation(item.Visibility, item.Width, item.Height);
        var header = new TabHeader(item.Header)
        {
            IsEnabled = item.IsEnabled,
            Visibility = requestedPresentation.Visibility,
        };
        header.Activated += OnHeaderActivated;
        InsertItemControl(ItemControlCount, item);

        try
        {
            _headers.Children.Add(header);
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

        if (_selectedIndex < 0 && FindEligible(0, 1) is var first and >= 0)
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

        var wasSelected = idx == _selectedIndex;
        var previousSelectedIndex = _selectedIndex;
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
            var target = FindNearestEligible(Math.Min(idx, ItemControlCount - 1));
            CommitSelectionAfterMutation(target, previousSelectedIndex);
        }
        else
        {
            if (idx < _selectedIndex)
            {
                CommitSelectionAfterMutation(_selectedIndex - 1, previousSelectedIndex);
                return true;
            }

            ApplyPresentation();
        }

        return true;
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

        if (!item.IsClosable)
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
        Select(-1);

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

        CommitSelection(index, _selectedIndex);
    }

    private void CommitSelectionAfterMutation(int index, int previousIndex)
    {
        VerifyMutable();
        _selectedIndex = -1;
        CommitSelection(index, previousIndex);
    }

    private void CommitSelection(int index, int previousIndex)
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
        NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure);
        SelectionChanged?.Invoke(this, new TabSelectionChangedEventArgs(previousIndex, index));
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

                item.Visibility = visibility;
            }
        }
        finally
        {
            _updatingPresentation = false;
        }
    }

    private int FindEligible(int start, int direction)
    {
        for (var index = start; index >= 0 && index < ItemControlCount; index += direction)
        {
            if (IsEligible(index))
            {
                return index;
            }
        }

        return -1;
    }

    private int FindEligibleWrapped(int direction)
    {
        if (ItemControlCount == 0)
        {
            return -1;
        }

        var origin = _selectedIndex >= 0 ? _selectedIndex : direction > 0 ? -1 : 0;

        for (var offset = 1; offset <= ItemControlCount; offset++)
        {
            var index = (origin + (direction * offset)) % ItemControlCount;

            if (index < 0)
            {
                index += ItemControlCount;
            }

            if (IsEligible(index))
            {
                return index;
            }
        }

        return -1;
    }

    private bool TryNavigate(Code code)
    {
        var index = -1;

        if (code == Code.Left)
        {
            index = FindEligibleWrapped(-1);
        }
        else if (code == Code.Right)
        {
            index = FindEligibleWrapped(1);
        }
        else if (code == Code.Home)
        {
            index = FindEligible(0, 1);
        }
        else if (code == Code.End)
        {
            index = FindEligible(ItemControlCount - 1, -1);
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
        return item.IsEnabled && RequestedVisibility(item) == Visibility.Visible;
    }

    private Visibility RequestedVisibility(TabItem item) =>
        _requestedPresentations.TryGetValue(item, out var presentation) ? presentation.Visibility : item.Visibility;

    private void SelectNearest(int index)
    {
        var target = FindNearestEligible(index);
        Select(target);
    }

    private int FindNearestEligible(int index)
    {
        var successor = FindEligible(Math.Max(0, index), 1);
        return successor >= 0 ? successor : FindEligible(Math.Min(index - 1, ItemControlCount - 1), -1);
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (sender is not TabItem item)
        {
            return;
        }

        if (eventArgs.PropertyName is not nameof(TabItem.Header) and not nameof(Visibility) and not nameof(IsEnabled))
        {
            return;
        }

        var index = IndexOfItemControl(item);

        if (index < 0)
        {
            return;
        }

        var header = HeaderAt(index);

        if (eventArgs.PropertyName == nameof(TabItem.Header))
        {
            header.Header = item.Header;
            return;
        }

        if (eventArgs.PropertyName == nameof(Visibility))
        {
            if (_updatingPresentation)
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

        if (index == _selectedIndex && !IsEligible(index))
        {
            SelectNearest(index);
        }
        else if (_selectedIndex < 0)
        {
            var first = FindEligible(0, 1);
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
