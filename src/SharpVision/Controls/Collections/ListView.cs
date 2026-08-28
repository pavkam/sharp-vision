// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using System.Collections.ObjectModel;

using Scrolling;

using SharpVision.Terminal.Input;

using DisplayText = Display.Text;
using GenericList = List<object?>;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;
using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;
using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Defines a focusable fully realized item selection control with scrolling.</summary>
[SuppressMessage(
    "Naming",
    "CA1710:Identifiers should have correct suffix",
    Justification = "ListView is the approved concise terminal control name, not a collection implementation.")]
[PublicAPI]
public sealed class ListView: ItemsControl
{
    private readonly ListViewHost _stack;
    private readonly StyleSlot<ScrollBarStyle> _scrollBarStyle;
    private readonly GenericList _items = [];
    private readonly ReadOnlyCollection<object?> _itemsView;
    private readonly HashSet<int> _selection = [];
    private readonly GenericList _selectedItems = [];
    private readonly ReadOnlyCollection<object?> _selectedView;

    // Parallel to _items, consulted only while RowHeight is set. Populated opportunistically at
    // realize/derealize time; an index never realized stays null and IsIndexAvailable treats that
    // as optimistically eligible, since FindEligible already tolerates skipping into unavailable
    // rows without crashing.
    private readonly List<bool?> _availability = [];
    private int _pendingSelectionReveal = -1;
    private int _selectionVersion;
    private int _selectionAnchor = -1;

    /// <summary>Initializes an empty single-selection ListView with a text template and continuous background.</summary>
    public ListView()
    {
        _itemsView = _items.AsReadOnly();
        _selectedView = _selectedItems.AsReadOnly();
        _stack = new ListViewHost
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded
        };
        _stack.ScrollChanged += OnHostScrollChanged;
        InitializeItemsHost(_stack);
        _scrollBarStyle = InitializePartStyle(
            ScrollBarStyle.ForwardingDefinition,
            nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, _stack, nameof(ScrollBarStyle));
        _ = AddHandler(Events.Key, OnKeyRouted);
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.None;
    }

    /// <summary>Raised before a changed selection commits and may cancel user or programmatic changes.</summary>
    public event EventHandler<ListSelectionChangingEventArgs>? SelectionChanging;

    /// <summary>Raised after a changed selection commits.</summary>
    public event EventHandler<ListSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised after semantic Enter or primary pointer invocation.</summary>
    public event EventHandler<ItemInvokedEventArgs>? ItemInvoked;

    /// <summary>Gets or atomically sets an owned snapshot of borrowed item values.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">Template output is null, disposed, attached, or duplicated.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    public IReadOnlyList<object?> Items
    {
        get => _itemsView;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();
            var copied = Copy(value);

            if (RowHeight is int height)
            {
                ReplaceVirtualized(copied, replaceItems: true);
            }
            else
            {
                var realized = Build(copied, ItemTemplate);
                Replace(copied, realized, replaceItems: true);
            }
        }
    }

    /// <summary>Gets or atomically sets the non-null detached-control factory.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">Candidate output is null, disposed, attached, or duplicated.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    public ItemTemplate ItemTemplate
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();

            if (ReferenceEquals(field, value))
            {
                return;
            }

            if (RowHeight is not null)
            {
                _ = SetPropertyAndSynchronize(
                    ref field,
                    value,
                    InvalidationImpact.Measure,
                    () => ReplaceVirtualized(_items, replaceItems: false));
                return;
            }

            var realized = Build(_items, value);
            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                InvalidationImpact.Measure,
                () => Replace(_items, realized, replaceItems: false));
        }
    } = DefaultTemplate;

    /// <summary>Gets or sets the fixed per-row cell height that opts into virtualized viewport-window
    /// realization; <see langword="null"/> retains today's eager realization.</summary>
    /// <remarks>
    /// A <see langword="null"/> value (the default) keeps eager, byte-for-byte realization -
    /// ComboBox, the file-picker dialogs, and every existing showcase and test embed a
    /// <see cref="ListView"/> on that guarantee. Assigning a positive value opts into windowed
    /// realization: only items inside the current viewport plus a bounded overscan margin are ever
    /// realized, and every arranged row is clipped to exactly this height. A template whose natural
    /// height differs from <see cref="RowHeight"/> is not supported in windowed mode - use
    /// <see langword="null"/> for variable-height templates. Toggling this property on an already
    /// populated ListView reconciles realization immediately: turning windowing on derealizes every
    /// row outside the freshly computed window, and turning it back off fully re-realizes the
    /// collection through <see cref="ItemTemplate"/> again, the same as reassigning <see cref="Items"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    public int? RowHeight
    {
        get;
        set
        {
            if (value is <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "RowHeight must be a positive cell count.");
            }

            VerifyMutable();

            if (field == value)
            {
                return;
            }

            var wasVirtualized = field is not null;
            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                InvalidationImpact.Measure,
                () =>
                {
                    _stack.RowHeight = RowHeight;

                    if (RowHeight is not null)
                    {
                        Rewindow();
                    }
                    else if (wasVirtualized)
                    {
                        Replace(_items, Build(_items, ItemTemplate), replaceItems: false);
                    }
                });
        }
    }

    /// <summary>Gets or sets whether no, one, or many indexes may be selected.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    public ListSelectionMode SelectionMode
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

            HashSet<int> normalized = [.. _selection];

            if (value == ListSelectionMode.None)
            {
                normalized.Clear();
            }
            else if (value == ListSelectionMode.Single && normalized.Count > 1)
            {
                var retained = normalized.Min();
                normalized.Clear();
                _ = normalized.Add(retained);
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
            // Mode transitions invalidate pending proposals even when normalization leaves selection unchanged.
            _selectionVersion++;
            _ = ApplySelection(normalized, cancellable: false, requireAvailability: false);
        }
    } = ListSelectionMode.Single;

    /// <summary>Gets or sets which pointer gesture raises <see cref="ItemInvoked"/>.</summary>
    /// <remarks>
    /// In <see cref="ListItemInvocation.DoubleClick"/> mode, a multi-click held with Control or
    /// Shift only toggles or extends selection - matching those modifiers' own selection-gesture
    /// semantics - and never raises <see cref="ItemInvoked"/>, even once the click count reaches a
    /// multi-click. A plain (unmodified) double-click still raises it.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    public ListItemInvocation ItemInvocation
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The item invocation mode is unknown.");

            VerifyMutable();
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = ListItemInvocation.SingleClick;

    /// <summary>Gets the lowest selected index, or sets, activates, and minimally reveals one
    /// exclusive index; -1 clears selection without moving the viewport.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than -1 or outside Items.</exception>
    /// <exception cref="InvalidOperationException">A non-negative value is assigned in None mode.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    [ValueRange(-1, int.MaxValue)]
    public int SelectedIndex
    {
        get => _selection.Count == 0 ? -1 : _selection.Min();
        set
        {
            if (value < -1 || value >= Items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "SelectedIndex is outside Items.");
            }

            if (value >= 0 && SelectionMode == ListSelectionMode.None)
            {
                throw new InvalidOperationException("ListSelectionMode.None does not accept a selected index.");
            }

            VerifyMutable();

            if (value >= 0 && !IsIndexAvailable(value))
            {
                return;
            }

            HashSet<int> next = value < 0 ? [] : [value];
            var accepted = ApplySelection(next, cancellable: true) || _selection.SetEquals(next);

            if (IsDisposed)
            {
                return;
            }

            if (accepted && _selection.SetEquals(next) && value >= 0)
            {
                CommitProgrammaticSelectionTarget(value);
            }
            else if (accepted && _selection.SetEquals(next) && value < 0)
            {
                _pendingSelectionReveal = -1;
            }
        }
    }

    /// <summary>Gets the lowest selected item, or sets one exclusive selection by value; null clears selection.</summary>
    public object? SelectedItem
    {
        get => SelectedIndex < 0 ? null : Items[SelectedIndex];
        set => SelectedIndex = value is null ? -1 : IndexOfItem(value);
    }

    /// <summary>Gets one stable owner-backed read-only view in ascending index order.</summary>
    public IReadOnlyList<object?> SelectedItems => _selectedView;

    /// <summary>Gets the active navigation and keyboard-selection index, or -1 when no item is active.</summary>
    [ValueRange(-1, int.MaxValue)]
    public int ActiveIndex { get; private set; } = -1;

    /// <summary>Raised after the composed scroll container's offset commits.</summary>
    /// <remarks>
    /// The scrolling items host is a private retained part; this forwards its
    /// <see cref="Container.ScrollChanged"/> so a consumer can observe scroll position without
    /// reaching into private presentation trees.
    /// </remarks>
    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged
    {
        add => _stack.ScrollChanged += value;
        remove => _stack.ScrollChanged -= value;
    }

    /// <summary>Gets the committed non-negative content extent of the composed scroll container.</summary>
    public Size Extent => _stack.Extent;

    /// <summary>Gets the committed non-negative visible extent of the composed scroll container.</summary>
    public Size Viewport => _stack.Viewport;

    /// <summary>Gets or sets the composed horizontal scroll offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    [NonNegativeValue]
    public int HorizontalOffset
    {
        get => _stack.HorizontalOffset;
        set => _stack.HorizontalOffset = value;
    }

    /// <summary>Gets or sets the composed vertical scroll offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    [NonNegativeValue]
    public int VerticalOffset
    {
        get => _stack.VerticalOffset;
        set => _stack.VerticalOffset = value;
    }

    /// <summary>Gets or sets the non-negative wheel-scroll increment in cells forwarded to the
    /// composed viewport.</summary>
    /// <remarks>
    /// Keyboard navigation always moves by exactly one item regardless of <see cref="RowHeight"/>
    /// or this value - only the mouse wheel consults it. Applications wanting the wheel to step by
    /// whole rows with a windowed <see cref="RowHeight"/> can set <see cref="LineSize"/> to the row
    /// height.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    [NonNegativeValue]
    public int LineSize
    {
        get => _stack.LineSize;
        set
        {
            var previous = _stack.LineSize;
            _stack.LineSize = value;

            if (previous != _stack.LineSize)
            {
                NotifyPropertyChanged(nameof(LineSize), InvalidationImpact.None);
            }
        }
    }

    /// <summary>Gets or sets the non-negative cells of context retained between page commands.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    [NonNegativeValue]
    public int PageOverlap
    {
        get => _stack.PageOverlap;
        set
        {
            var previous = _stack.PageOverlap;
            _stack.PageOverlap = value;

            if (previous != _stack.PageOverlap)
            {
                NotifyPropertyChanged(nameof(PageOverlap), InvalidationImpact.None);
            }
        }
    }

    /// <summary>Scrolls the composed scroll container by signed cell deltas with saturation and
    /// endpoint clamping.</summary>
    /// <param name="x">The requested horizontal delta.</param>
    /// <param name="y">The requested vertical delta.</param>
    /// <param name="cause">The defined input path.</param>
    /// <returns>True when at least one offset changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    public bool ScrollBy(int x, int y, ScrollCause cause = ScrollCause.Programmatic) =>
        _stack.ScrollBy(x, y, cause);

    /// <summary>Scrolls minimally to expose one item by position, without requiring the caller to
    /// know about the private realized visual tree.</summary>
    /// <param name="index">The valid zero-based item position.</param>
    /// <returns>True when at least one offset changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside Items.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    public bool BringIntoView(int index)
    {
        ValidateIndex(index);
        VerifyMutable();

        if (RowHeight is int height)
        {
            var before = VerticalOffset;
            ScrollIndexIntoView(index, height);
            Rewindow();
            return VerticalOffset != before;
        }

        return _stack.BringIntoView(GetItemControl(index));
    }

    /// <summary>Gets or sets the axes available to the composed overflow host.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    public ScrollBars ScrollBars
    {
        get => _stack.ScrollBars;
        set
        {
            VerifyMutable();

            if (_stack.ScrollBars == value)
            {
                return;
            }

            _stack.ScrollBars = value;
            NotifyPropertyChanged(nameof(ScrollBars), InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets the common scrollbar reservation policy for enabled axes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get => _stack.ShowScrollBars;
        set
        {
            VerifyMutable();

            if (_stack.ShowScrollBars == value)
            {
                return;
            }

            _stack.ShowScrollBars = value;
            NotifyPropertyChanged(nameof(ShowScrollBars), InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets the complete local style for generated scrollbars.</summary>
    /// <exception cref="InvalidOperationException">The attached ListView is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    public ScrollBarStyle? ScrollBarStyle
    {
        get => _scrollBarStyle.Local;
        set => _scrollBarStyle.Local = value;
    }

    /// <summary>Gets the resolved generated-scrollbar style.</summary>
    public ScrollBarStyle ActualScrollBarStyle => _scrollBarStyle.Actual;

    /// <summary>Changes one programmatic index without replacing other Multiple selections;
    /// selecting also activates and minimally reveals that index.</summary>
    /// <param name="index">The contained item index.</param>
    /// <param name="selected">Whether the index should be selected.</param>
    /// <returns>True when a changed selection committed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside Items.</exception>
    /// <exception cref="InvalidOperationException">Selection is requested in None mode.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    public bool SetSelected(int index, bool selected)
    {
        ValidateIndex(index);

        if (selected && SelectionMode == ListSelectionMode.None)
        {
            throw new InvalidOperationException("ListSelectionMode.None does not accept selection.");
        }

        VerifyMutable();

        if (selected && !IsIndexAvailable(index))
        {
            return false;
        }

        HashSet<int> next = [.. _selection];

        if (selected)
        {
            if (SelectionMode == ListSelectionMode.Single)
            {
                next.Clear();
            }

            _ = next.Add(index);
        }
        else
        {
            _ = next.Remove(index);
        }

        var changed = ApplySelection(next, cancellable: true);

        if (changed && selected && _selection.SetEquals(next))
        {
            CommitProgrammaticSelectionTarget(index);
        }

        return changed;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) => MeasureChild(_stack, constraint);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0 || !this.HasOpaqueFill(GetAppearanceState()))
        {
            return;
        }

        canvas.Clear(Bounds, ResolvedStyle);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        ArrangeChild(_stack, bounds, ResolvedAxes.Both);

        // _stack's own arrange transaction has already closed by this point - reconciling here
        // catches every case that changed viewport, offset, or extent without a genuine
        // interactive scroll (first layout, resize, RowHeight/Items churn while off-window),
        // without ever mutating _stack.Children while _stack.IsArranging is still true.
        Rewindow();
        RevealPendingSelection();
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

    private static ControlBase DefaultTemplate(object? item) =>
        new DisplayText(Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty);

    [Pure]
    private static object?[] Copy(IReadOnlyList<object?> values)
    {
        Debug.Assert(values is not null, "ListView copy requires a non-null source.");

        var result = new object?[values.Count];

        for (var index = 0; index < result.Length; index++)
        {
            result[index] = values[index];
        }

        return result;
    }

    [MustUseReturnValue]
    private static ListItem[] Build(IReadOnlyList<object?> items, ItemTemplate template)
    {
        Debug.Assert(items is not null, "ListView build requires a non-null item source.");
        Debug.Assert(template is not null, "ListView build requires a non-null template.");

        var controls = new ControlBase[items.Count];
        var unique = new HashSet<ControlBase>(ReferenceEqualityComparer.Instance);

        try
        {
            for (var index = 0; index < controls.Length; index++)
            {
                var control = template(items[index]);

                if (control is null || !unique.Add(control))
                {
                    throw new ArgumentException("ItemTemplate must return one unique non-null control.",
                        nameof(template));
                }

                if (control.Parent is not null || control.Dispatcher is not null || control.IsDisposed)
                {
                    throw new ArgumentException("ItemTemplate controls must be detached and undisposed.",
                        nameof(template));
                }

                control.ValidateAttachment();
                controls[index] = control;
            }

            var result = new ListItem[controls.Length];

            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new ListItem(index, controls[index]);
            }

            Debug.Assert(result.Length == items.Count, "Every item must realize to one ListItem.");

            return result;
        }
        catch
        {
            foreach (var control in unique)
            {
                if (control.Parent is null && !control.IsDisposed)
                {
                    control.Dispose();
                }
            }

            throw;
        }
    }

    private void Replace(IReadOnlyList<object?> items, ListItem[] realized, bool replaceItems)
    {
        Debug.Assert(realized is not null, "ListView replacement requires realized items.");
        Debug.Assert(realized.Length == items.Count, "Realized items must match the source count.");

        var previousItems = _items.ToArray();
        var previousSelection = _selection.Order().ToArray();
        var previousActiveIndex = ActiveIndex;
        var previousAnchor = _selectionAnchor;

        // Re-realization invalidates pending proposals that may still reference an old wrapper.
        _selectionVersion++;
        List<ListItem> previous = [];

        for (var index = 0; index < ItemControlCount; index++)
        {
            var item = (ListItem) GetItemControl(index);
            item.Activated -= OnActivated;
            item.AvailabilityChanged -= OnItemAvailabilityChanged;
            previous.Add(item);
        }

        ReplaceItemControls(realized);

        foreach (var item in previous)
        {
            item.Dispose();
        }

        if (replaceItems)
        {
            _items.Clear();

            for (var index = 0; index < items.Count; index++)
            {
                _items.Add(items[index]);
            }
        }

        ResetAvailability();
        _stack.ItemCount = _items.Count;

        var indexMap = BuildIndexMap(previousItems, _items);
        var normalized = MapIndexes(previousSelection, indexMap);

        if (SelectionMode == ListSelectionMode.Single && normalized.Count > 1)
        {
            var retained = normalized.Min();
            normalized.Clear();
            _ = normalized.Add(retained);
        }

        var nextActiveIndex = MapIndex(previousActiveIndex, indexMap);

        if (nextActiveIndex < 0 || !IsIndexAvailable(nextActiveIndex))
        {
            nextActiveIndex = FindAvailableActiveIndex(previousActiveIndex);
        }

        var nextAnchor = MapIndex(previousAnchor, indexMap);

        if (nextAnchor < 0 && previousAnchor >= 0)
        {
            nextAnchor = nextActiveIndex;
        }

        foreach (var item in realized)
        {
            item.Activated += OnActivated;
            item.AvailabilityChanged += OnItemAvailabilityChanged;
            item.CommitSelection(_selection.Contains(item.Index));
        }

        // ApplySelection itself unconditionally bumps _selectionVersion once when it accepts a
        // commit, so the expected post-call version is the pre-call version plus one when it
        // returns true - not the unchanged pre-call version. ApplySelection may also synchronously
        // re-enter this list (a SelectionChanging handler that mutates Items again) before returning
        // here; when that happens, the reentrant call bumps the version further still, and it has
        // already recomputed active index/anchor/selected-items against the newer state, so this
        // now-stale continuation must be skipped rather than overwriting that newer state.
        var selectionVersion = _selectionVersion;
        var applied = ApplySelection(normalized, cancellable: false, requireAvailability: false);

        if (_selectionVersion == selectionVersion + (applied ? 1 : 0))
        {
            SetActiveIndex(nextActiveIndex);
            _selectionAnchor = nextAnchor;
            RefreshSelectedItems();
            NotifyPropertyChanged(nameof(Items), InvalidationImpact.Measure);
        }
    }

    /// <summary>Replaces the complete item snapshot without eagerly realizing every row - the
    /// virtualized counterpart to <see cref="Replace"/>, taken whenever <see cref="RowHeight"/> is
    /// set. Only the bounded currently-realized window is disposed and rebuilt; the rest of the
    /// collection is never handed to <see cref="ItemTemplate"/> until it scrolls into view.</summary>
    private void ReplaceVirtualized(IReadOnlyList<object?> items, bool replaceItems)
    {
        var previousItems = _items.ToArray();
        var previousSelection = _selection.Order().ToArray();
        var previousActiveIndex = ActiveIndex;
        var previousAnchor = _selectionAnchor;

        _selectionVersion++;

        for (var position = ItemControlCount - 1; position >= 0; position--)
        {
            var item = (ListItem) GetItemControl(position);
            item.Activated -= OnActivated;
            item.AvailabilityChanged -= OnItemAvailabilityChanged;
            RemoveItemControlAt(position);
            item.Dispose();
        }

        if (replaceItems)
        {
            _items.Clear();

            for (var index = 0; index < items.Count; index++)
            {
                _items.Add(items[index]);
            }
        }

        ResetAvailability();
        _stack.ItemCount = _items.Count;

        var indexMap = BuildIndexMap(previousItems, _items);
        var normalized = MapIndexes(previousSelection, indexMap);

        if (SelectionMode == ListSelectionMode.Single && normalized.Count > 1)
        {
            var retained = normalized.Min();
            normalized.Clear();
            _ = normalized.Add(retained);
        }

        var nextActiveIndex = MapIndex(previousActiveIndex, indexMap);

        if (nextActiveIndex < 0 || !IsIndexAvailable(nextActiveIndex))
        {
            nextActiveIndex = FindAvailableActiveIndex(previousActiveIndex);
        }

        var nextAnchor = MapIndex(previousAnchor, indexMap);

        if (nextAnchor < 0 && previousAnchor >= 0)
        {
            nextAnchor = nextActiveIndex;
        }

        _selection.Clear();
        _selection.UnionWith(normalized);
        RemapPendingSelectionReveal(nextActiveIndex);
        ActiveIndex = nextActiveIndex;
        _selectionAnchor = nextAnchor;
        RefreshSelectedItems();
        Rewindow();
        NotifyPropertyChanged(nameof(ActiveIndex), InvalidationImpact.Render);
        NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Render);
        NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);
        NotifyPropertyChanged(nameof(SelectedItems), InvalidationImpact.Render);
        NotifyPropertyChanged(nameof(Items), InvalidationImpact.Measure);
    }

    private void ResetAvailability()
    {
        _availability.Clear();

        for (var index = 0; index < _items.Count; index++)
        {
            _availability.Add(null);
        }
    }

    private void OnHostScrollChanged(object? sender, ScrollChangedEventArgs eventArgs)
    {
        if (eventArgs.Cause is ScrollCause.Content or ScrollCause.Resize)
        {
            // Both causes originate from this container's own ResolveContentSlot, called while
            // this container's arrange transaction is still open - realizing or derealizing
            // children there risks the exact non-convergence hazard Container.cs documents for
            // lazy child creation during layout. A generous overscan margin absorbs ordinary
            // resize deltas instead; the window catches up fully on the next genuine scroll.
            return;
        }

        Rewindow();
    }

    /// <summary>Reconciles realized rows against the current viewport, offset, and overscan
    /// margin. A no-op in eager mode. Must only ever be called outside an active measure or
    /// arrange transaction - see <see cref="OnHostScrollChanged"/>.</summary>
    private void Rewindow()
    {
        if (RowHeight is not int height)
        {
            return;
        }

        var viewportRows = Math.Max(1, Viewport.Height / height);
        var first = _items.Count == 0 ? 0 : Math.Max(0, (VerticalOffset / height) - viewportRows);
        var last = _items.Count == 0
            ? -1
            : Math.Min(
                _items.Count - 1,
                ((VerticalOffset + Math.Max(0, Viewport.Height - 1)) / height) + viewportRows);

        for (var position = ItemControlCount - 1; position >= 0; position--)
        {
            var realizedItem = (ListItem) GetItemControl(position);

            if (realizedItem.Index >= first && realizedItem.Index <= last)
            {
                continue;
            }

            _availability[realizedItem.Index] = realizedItem.IsAvailable;
            realizedItem.Activated -= OnActivated;
            realizedItem.AvailabilityChanged -= OnItemAvailabilityChanged;
            RemoveItemControlAt(position);
            realizedItem.Dispose();
        }

        var origin = _stack.RowOrigin;
        var offset = VerticalOffset;
        var horizontalOffset = HorizontalOffset;

        for (var index = first; index <= last; index++)
        {
            var listItem = ItemAt(index);

            if (listItem is null)
            {
                listItem = BuildSingle(index, _items[index], ItemTemplate);
                InsertItemControl(FindRealizedInsertPosition(index), listItem);
                listItem.Activated += OnActivated;
                listItem.AvailabilityChanged += OnItemAvailabilityChanged;
                _availability[index] = listItem.IsAvailable;

                if (!listItem.IsAvailable)
                {
                    var itemCount = _items.Count;
                    RepairIneligibleIndex(index);

                    if (IsDisposed || _items.Count != itemCount || !ReferenceEquals(ItemAt(index), listItem))
                    {
                        return;
                    }
                }

                listItem.CommitSelection(_selection.Contains(index));

                if (index == ActiveIndex)
                {
                    listItem.SetCurrentState(true);
                }
            }

            // _stack's own arrange transaction may already have closed for this pass (a
            // structural mutation triggered from an interactive ScrollChanged, or one raised by
            // Insert/Remove/ReplaceVirtualized outside layout entirely), so every row still in the
            // window - not only a newly realized one - is measured and arranged directly rather
            // than waiting for a future container-level pass to reach it. A row that was already
            // realized before this offset changed still has its prior position otherwise: only
            // ScrollChanged fired, not a full container arrange, so nothing else would ever move
            // it. RowOrigin is the pre-offset viewport rect from the last genuine layout pass, so
            // subtracting the CURRENT offset here (not whatever offset was in effect when RowOrigin
            // was last committed) keeps this correct across a scroll with no accompanying layout
            // call. The next real layout pass re-arranges every realized row unconditionally
            // regardless, so any staleness here is only ever a transient bridge state.
            // Widened to the greater of the total content extent and the viewport width so the
            // row's right edge still reaches the scrollable content's true right edge regardless
            // of how far the content has scrolled - matching what the real (non-Rewindow) arrange
            // path does via Container.ResolveContentSlot, which resolves scrollable content width
            // as Math.Max(Extent.Width, viewport.Width), independent of the current offset. Using
            // origin.Width.Add(horizontalOffset) instead would measure/arrange the row narrower
            // than a full layout pass at every offset except HorizontalOffset == MaximumX(),
            // shifting width-dependent content (Right/Stretch alignment, Star-sized columns) to
            // the wrong position after any interactive scroll.
            var rowWidth = Math.Max(Extent.Width, origin.Width);

            listItem.Measure(new Constraint(rowWidth, height));
            ListViewHost.ArrangeRow(
                listItem,
                new Rect(
                    origin.X.Add(-horizontalOffset),
                    origin.Y.Add(index.Multiply(height)).Add(-offset),
                    rowWidth,
                    height));
        }
    }

    [Pure]
    private int FindRealizedInsertPosition(int index)
    {
        var position = 0;

        while (position < ItemControlCount && ((ListItem) GetItemControl(position)).Index < index)
        {
            position++;
        }

        return position;
    }

    /// <summary>Scrolls minimally so a logical index's arithmetic row slot is fully inside the
    /// viewport, without requiring a realized descendant the way <see cref="Container.BringIntoView"/>
    /// does.</summary>
    private void ScrollIndexIntoView(int index, int height)
    {
        var current = VerticalOffset;
        var target = PagingStep.IndexIntoViewOffset(index, height, current, Viewport.Height, Extent.Height);

        if (target != current)
        {
            VerticalOffset = target;
        }
    }

    /// <summary>Inserts one item at a validated position without rebuilding the entire list.</summary>
    /// <param name="index">The insertion position from zero through <see cref="Items"/>.<see cref="IReadOnlyCollection{T}.Count"/>.</param>
    /// <param name="item">The borrowed item value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    /// <exception cref="ArgumentException">Template output is null, disposed, attached, or duplicated.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    internal void InsertItem([NonNegativeValue] int index, object? item)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _items.Count);
        VerifyMutable();

        var height = RowHeight;
        var listItem = height is null ? BuildSingle(index, item, ItemTemplate) : null;
        _selectionVersion++;
        _items.Insert(index, item);
        _availability.Insert(index, null);
        _stack.ItemCount = _items.Count;

        if (listItem is not null)
        {
            InsertItemControl(index, listItem);
        }

        // Every realized row at or after the insertion point shifts by one logical index.
        // Matching by value (not by host position) is a strict generalization of the eager-only
        // "position equals index" shortcut this loop replaced - the two agree whenever every index
        // is realized, and only the by-value form stays correct once some are not.
        for (var position = 0; position < ItemControlCount; position++)
        {
            var realizedItem = (ListItem) GetItemControl(position);

            if (!ReferenceEquals(realizedItem, listItem) && realizedItem.Index >= index)
            {
                realizedItem.Index++;
            }
        }

        if (listItem is not null)
        {
            listItem.Activated += OnActivated;
            listItem.AvailabilityChanged += OnItemAvailabilityChanged;
            listItem.CommitSelection(false);
        }

        var selectionShifted = false;
        HashSet<int> shifted = [];

        foreach (var selected in _selection)
        {
            selectionShifted |= selected >= index;
            _ = shifted.Add(selected >= index ? selected + 1 : selected);
        }

        _selection.Clear();
        _selection.UnionWith(shifted);

        var nextActiveIndex = ActiveIndex >= index ? ActiveIndex + 1 : ActiveIndex;

        if (_selectionAnchor >= index)
        {
            _selectionAnchor++;
        }

        if (height is int rowHeight)
        {
            // Every realized row at or after the insertion point already shifted its logical
            // Index above, but the viewport's own offset is still numerically unchanged - an
            // insertion at or above the current first-visible row would otherwise silently swap
            // which logical item that offset now points at. Compensating before Rewindow() lets
            // it realize the window around the already-corrected offset in one pass.
            if (index <= VerticalOffset / rowHeight)
            {
                // A plain ScrollBy would clamp against Extent, which only refreshes on a layout
                // pass that has not run since _items.Count changed above - it would still reflect
                // the pre-insertion item count. Fixed RowHeight makes the true post-insertion
                // maximum knowable arithmetically right now, so that computed bound is supplied
                // directly instead of trusting the stale Extent.
                var maximumY = Math.Max(0, _items.Count.Multiply(rowHeight) - Viewport.Height);
                _ = _stack.ScrollByKnownMaximum(rowHeight, maximumY, ScrollCause.Content);
            }

            Rewindow();
        }

        SetActiveIndex(nextActiveIndex);
        RefreshSelectedItems();

        if (selectionShifted)
        {
            NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Render);
            NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);
            NotifyPropertyChanged(nameof(SelectedItems), InvalidationImpact.Render);
        }

        NotifyPropertyChanged(nameof(Items), InvalidationImpact.Measure);
    }

    /// <summary>Removes one item by position without rebuilding the entire list.</summary>
    /// <param name="index">The valid zero-based item position.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside <see cref="Items"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    internal void RemoveItem([NonNegativeValue] int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _items.Count);
        VerifyMutable();

        var previousSelection = _selection.Order().ToArray();
        var listItem = ItemAt(index);

        if (listItem is not null)
        {
            listItem.Activated -= OnActivated;
            listItem.AvailabilityChanged -= OnItemAvailabilityChanged;
            _ = RemoveItemControl(listItem);
            listItem.Dispose();
        }

        _items.RemoveAt(index);
        _availability.RemoveAt(index);
        _stack.ItemCount = _items.Count;

        for (var position = 0; position < ItemControlCount; position++)
        {
            var realizedItem = (ListItem) GetItemControl(position);

            if (realizedItem.Index > index)
            {
                realizedItem.Index--;
            }
        }

        _selectionVersion++;
        HashSet<int> shifted = [];

        foreach (var selected in _selection)
        {
            if (selected < index)
            {
                _ = shifted.Add(selected);
            }
            else if (selected > index)
            {
                _ = shifted.Add(selected - 1);
            }
        }

        HashSet<int> mappedSelection = [];

        foreach (var selected in previousSelection)
        {
            var mapped = selected == index
                ? -1
                : selected > index
                    ? selected - 1
                    : selected;

            if (mapped >= 0 && shifted.Contains(mapped))
            {
                _ = mappedSelection.Add(mapped);
            }
        }

        var added = shifted.Except(mappedSelection).Order().ToArray();
        var removed = previousSelection.Where(selected =>
            selected == index ||
            !mappedSelection.Contains(selected > index ? selected - 1 : selected)).Order().ToArray();

        // ApplySelection itself unconditionally bumps _selectionVersion once when it accepts a
        // commit, so the expected post-call version is the pre-call version plus one when it
        // returns true - not the unchanged pre-call version. ApplySelection may also synchronously
        // re-enter this list (a SelectionChanging handler that mutates Items again) before returning
        // here; when that happens, the reentrant call bumps the version further still, and it has
        // already recomputed active index/anchor/selected-items against the newer state, so this
        // now-stale continuation must be skipped rather than overwriting that newer state.
        var selectionVersion = _selectionVersion;
        var applied = ApplySelection(shifted, cancellable: false, added, removed, requireAvailability: false);
        var nextActiveIndex = ActiveIndex > index
            ? ActiveIndex - 1
            : ActiveIndex >= _items.Count
                ? _items.Count - 1
                : ActiveIndex;

        if (!IsIndexAvailable(nextActiveIndex))
        {
            nextActiveIndex = FindAvailableActiveIndex(nextActiveIndex);
        }

        if (RowHeight is int height)
        {
            // Mirrors InsertItem's compensation, with a strict comparison: removing the row
            // currently at the first-visible position itself leaves no matching logical item to
            // re-anchor to, so only a removal strictly above the first-visible row shifts the
            // offset - the row that slides up into the removed slot becomes the new first-visible
            // row, which is the same outcome an unshifted offset already produces.
            if (index < VerticalOffset / height)
            {
                // See InsertItem's identical comment: Extent is still the stale, pre-removal
                // value at this point, so the true post-removal maximum is computed directly
                // rather than trusting a plain ScrollBy's clamp against it.
                var maximumY = Math.Max(0, _items.Count.Multiply(height) - Viewport.Height);
                _ = _stack.ScrollByKnownMaximum(-height, maximumY, ScrollCause.Content);
            }

            Rewindow();
        }

        if (_selectionVersion == selectionVersion + (applied ? 1 : 0))
        {
            SetActiveIndex(nextActiveIndex);

            if (_selectionAnchor == index)
            {
                _selectionAnchor = _selection.Contains(ActiveIndex) ? ActiveIndex : -1;
            }
            else if (_selectionAnchor > index)
            {
                _selectionAnchor--;
            }
            else if (_selectionAnchor >= _items.Count)
            {
                _selectionAnchor = _items.Count - 1;
            }

            RefreshSelectedItems();
            NotifyPropertyChanged(nameof(Items), InvalidationImpact.Measure);
        }
    }

    /// <summary>Replaces one item value and its realized control at a validated position.</summary>
    /// <param name="index">The valid zero-based item position.</param>
    /// <param name="item">The replacement borrowed item value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside <see cref="Items"/>.</exception>
    /// <exception cref="ArgumentException">Template output is null, disposed, attached, or duplicated.</exception>
    /// <exception cref="InvalidOperationException">The attached ListView is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The ListView is disposed.</exception>
    internal void ReplaceItem([NonNegativeValue] int index, object? item)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _items.Count);
        VerifyMutable();

        var previousValue = _items[index];
        var previous = ItemAt(index);

        if (previous is not null)
        {
            var replacement = BuildSingle(index, item, ItemTemplate);
            var position = IndexOfItemControl(previous);
            previous.Activated -= OnActivated;
            previous.AvailabilityChanged -= OnItemAvailabilityChanged;
            ReplaceItemControl(position, replacement);
            previous.Dispose();
            replacement.Activated += OnActivated;
            replacement.AvailabilityChanged += OnItemAvailabilityChanged;
            replacement.CommitSelection(_selection.Contains(index) && Equals(previousValue, item));
        }

        _items[index] = item;
        _availability[index] = null;

        HashSet<int> normalized = [.. _selection];

        if (!Equals(previousValue, item))
        {
            _ = normalized.Remove(index);
        }

        // ApplySelection itself unconditionally bumps _selectionVersion once when it accepts a
        // commit, so the expected post-call version is the pre-call version plus one when it
        // returns true - not the unchanged pre-call version. ApplySelection may also synchronously
        // re-enter this list (a SelectionChanging handler that mutates Items again) before returning
        // here; when that happens, the reentrant call bumps the version further still, and it has
        // already recomputed active index/anchor/selected-items against the newer state, so this
        // now-stale continuation must be skipped rather than overwriting that newer state.
        var selectionVersion = _selectionVersion;
        var applied = ApplySelection(normalized, cancellable: false, requireAvailability: false);

        if (_selectionVersion == selectionVersion + (applied ? 1 : 0))
        {
            if (_selectionAnchor == index && !Equals(previousValue, item))
            {
                _selectionAnchor = _selection.Contains(ActiveIndex) ? ActiveIndex : -1;
            }

            var nextActiveIndex = IsIndexAvailable(ActiveIndex)
                ? ActiveIndex
                : FindAvailableActiveIndex(ActiveIndex);
            SetActiveIndex(nextActiveIndex);
            RefreshSelectedItems();
            NotifyPropertyChanged(nameof(Items), InvalidationImpact.Measure);
        }
    }

    [MustUseReturnValue]
    private static ListItem BuildSingle([NonNegativeValue] int index, object? item, ItemTemplate template)
    {
        var control = template(item) ??
            throw new ArgumentException(
                "ItemTemplate must return a non-null control.",
                nameof(template));

        if (control.Parent is not null || control.Dispatcher is not null || control.IsDisposed)
        {
            throw new ArgumentException(
                "ItemTemplate controls must be detached and undisposed.",
                nameof(template));
        }

        control.ValidateAttachment();
        return new ListItem(index, control);
    }

    [Pure]
    private static int[] BuildIndexMap(
        object?[] previousItems,
        GenericList nextItems)
    {
        var map = new int[previousItems.Length];
        Array.Fill(map, -1);
        Dictionary<object, Queue<int>> buckets = [];
        Queue<int>? nullBucket = null;

        for (var index = 0; index < nextItems.Count; index++)
        {
            var item = nextItems[index];

            if (item is null)
            {
                nullBucket ??= [];
                nullBucket.Enqueue(index);
                continue;
            }

            if (!buckets.TryGetValue(item, out var indexes))
            {
                indexes = [];
                buckets.Add(item, indexes);
            }

            indexes.Enqueue(index);
        }

        for (var index = 0; index < previousItems.Length; index++)
        {
            var item = previousItems[index];
            var indexes = item is null
                ? nullBucket
                : buckets.TryGetValue(item, out var bucket) ? bucket : null;

            if (indexes is { Count: > 0 })
            {
                map[index] = indexes.Dequeue();
            }
        }

        return map;
    }

    [Pure]
    private static HashSet<int> MapIndexes(IReadOnlyList<int> indexes, IReadOnlyList<int> indexMap)
    {
        HashSet<int> mapped = [];

        foreach (var index in indexes)
        {
            var nextIndex = MapIndex(index, indexMap);

            if (nextIndex >= 0)
            {
                _ = mapped.Add(nextIndex);
            }
        }

        return mapped;
    }

    [Pure]
    private static int MapIndex(int previousIndex, IReadOnlyList<int> indexMap) =>
        (uint) previousIndex < (uint) indexMap.Count ? indexMap[previousIndex] : -1;

    [Pure]
    private int FindAvailableActiveIndex(int index)
    {
        if (_items.Count == 0 || index < 0)
        {
            return -1;
        }

        var clamped = Math.Min(index, _items.Count - 1);
        var last = _items.Count - 1;
        var maximumDistance = Math.Max(clamped, last - clamped);

        for (var distance = 0; distance <= maximumDistance; distance++)
        {
            var lower = clamped - distance;

            if (lower >= 0 && IsIndexAvailable(lower))
            {
                return lower;
            }

            if (distance <= last - clamped)
            {
                var higher = clamped + distance;

                if (distance > 0 && IsIndexAvailable(higher))
                {
                    return higher;
                }
            }
        }

        return -1;
    }

    private bool ApplySelection(
        HashSet<int> next,
        bool cancellable,
        int[]? stableAdded = null,
        int[]? stableRemoved = null,
        bool requireAvailability = true)
    {
        // IsIndexAvailable factors in EffectiveIsVisible for a realized row, which is false for
        // every row while an ancestor (a closed Popup, a collapsed tab) hides the whole list — that
        // is not a genuine veto on a value the list is only retaining across a structural remap
        // (Items/ItemTemplate reassignment, insert, remove, replace), so those call sites pass
        // requireAvailability: false. A fresh interactive or programmatic selection request still
        // requires the target to be genuinely available. An unrealized index in virtualized mode has
        // no live row to ask, so this optimistically assumes eligible rather than silently dropping
        // it from selection just because it is currently off-window.
        var committed = SelectionCommit<int>.TryCommit(
            _selection,
            next,
            EqualityComparer<int>.Default,
            requireAvailability ? IsIndexAvailable : null,
            SelectionMode == ListSelectionMode.None,
            (current, selectable) => (
                stableAdded ?? [.. selectable.Except(current).Order()],
                stableRemoved ?? [.. current.Except(selectable).Order()]),
            cancellable,
            ref _selectionVersion,
            static (added, removed) => new ListSelectionChangingEventArgs(added, removed),
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

        _selection.Clear();
        _selection.UnionWith(selection);

        for (var position = 0; position < ItemControlCount; position++)
        {
            var realizedItem = (ListItem) GetItemControl(position);
            realizedItem.CommitSelection(_selection.Contains(realizedItem.Index));
        }

        RefreshSelectedItems();
        NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Render);

        if (_selectionVersion != selectionVersion)
        {
            return true;
        }

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

        if (added.Length > 0 || removed.Length > 0)
        {
            SelectionChanged?.Invoke(this, new ListSelectionChangedEventArgs(added, removed));
        }

        return true;
    }

    private void RefreshSelectedItems()
    {
        _selectedItems.Clear();

        foreach (var index in _selection.Order())
        {
            if (index < Items.Count)
            {
                _selectedItems.Add(Items[index]);
            }
        }
    }

    /// <summary>Tracks the item that received focus without mutating selection.</summary>
    internal void NotifyItemFocused(ListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Debug.Assert(IndexOfItemControl(item) >= 0, "A focused ListItem is a realized child of this ListView.");

        SetActiveIndex(item.Index);
        _ = _stack.BringIntoView(item);
    }

    /// <summary>Moves the owned current item for a selector-level navigation key.</summary>
    /// <param name="code">The navigation key.</param>
    /// <returns><see langword="true"/> when the key moved current item; otherwise, <see langword="false"/>.</returns>
    internal bool MoveCurrent(Code code)
    {
        var target = ResolveMove(code);

        if (target is null)
        {
            return false;
        }

        CommitCurrent(target);
        return true;
    }

    /// <summary>Activates the owned current item without transferring focus to that item.</summary>
    /// <param name="cause">The semantic activation source.</param>
    /// <param name="key">The activating key, or null for non-key activation.</param>
    /// <param name="modifiers">The modifiers captured with <paramref name="key"/>.</param>
    /// <returns><see langword="true"/> when an available current item was activated; otherwise, <see langword="false"/>.</returns>
    internal bool ActivateCurrent(ActivationCause cause, Code? key, Modifiers modifiers)
    {
        var index = ActiveIndex >= 0 && ActiveIndex < _items.Count && IsIndexAvailable(ActiveIndex)
            ? ActiveIndex
            : FindEligible(0, 1);

        if (index < 0)
        {
            return false;
        }

        Realize(index).ActivateFromOwner(cause, key, modifiers);
        return true;
    }

    private void OnActivated(object? sender, ActivationEventArgs eventArgs)
    {
        var item = (ListItem) sender!;
        SetActiveIndex(item.Index);

        if (!IsActivatedItemCurrent(item))
        {
            return;
        }

        if (item.LastKey == Code.Enter)
        {
            // An incidental modifier still applies the current-item tracking above, but does not
            // commit an invocation the user did not intend.
            if (item.LastModifiers.IsActivationEligible())
            {
                ItemInvoked?.Invoke(this, new ItemInvokedEventArgs(item.Index, Items[item.Index], eventArgs.Cause));
            }

            return;
        }

        var modifiers = item.LastModifiers;
        var isSpaceToggle = SelectionMode == ListSelectionMode.Multiple &&
                            eventArgs.Cause == ActivationCause.Keyboard &&
                            (modifiers & (Modifiers.Control | Modifiers.Shift)) == 0;

        _ = ApplyInputSelection(item.Index, isSpaceToggle ? modifiers | Modifiers.Control : modifiers);

        if (!IsActivatedItemCurrent(item))
        {
            return;
        }

        // PointerManager's click-count chain tracks target, cell, and buttons only, never
        // modifiers. A multi-click is a commit only when lock-normalized modifiers are empty;
        // selection, application, and host command chords remain available to their owners.
        var isPlainPointerGesture = KeyboardModifierPolicy.MatchesCommand(modifiers, Modifiers.None);

        if (eventArgs.Cause == ActivationCause.Pointer &&
            (ItemInvocation == ListItemInvocation.SingleClick ||
                (item.LastClickCount >= 2 && isPlainPointerGesture)))
        {
            ItemInvoked?.Invoke(this, new ItemInvokedEventArgs(item.Index, Items[item.Index], eventArgs.Cause));
        }
    }

    [Pure]
    private bool IsActivatedItemCurrent(ListItem item) =>
        !IsDisposed &&
        !item.IsDisposed &&
        ReferenceEquals(ItemAt(item.Index), item);

    // Reacts only to the realized row's own content leaving availability, never to it arriving -
    // becoming available never reclaims selection or active state, matching the no-auto-reclaim
    // precedent a removal already sets for the rest of the collection.
    private void OnItemAvailabilityChanged(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;
        var item = (ListItem) sender!;

        if (item.IsAvailable)
        {
            return;
        }

        RepairIneligibleIndex(item.Index);
    }

    /// <summary>Removes the semantic item before its directly disposed template content leaves the wrapper.</summary>
    internal void OnItemContentDisposalRequested(ListItem item, ControlBase content)
    {
        if (!ReferenceEquals(item.Content, content) || !ReferenceEquals(ItemAt(item.Index), item))
        {
            return;
        }

        item.Content = null;
        RemoveItem(item.Index);
    }

    // Drops a row that just became ineligible from selection and, if it was also the active row,
    // hands active state to the nearest still-eligible neighbor - mirroring ReplaceItem's tail,
    // but never promoting selection to an adjacent index the way NavigationView's group-collapse
    // repair does; ListView selection is drop-only, matching its own removal precedent.
    private void RepairIneligibleIndex(int index)
    {
        HashSet<int> normalized = [.. _selection];
        _ = normalized.Remove(index);
        _ = ApplySelection(normalized, cancellable: false, requireAvailability: false);

        if (_selectionAnchor == index)
        {
            _selectionAnchor = _selection.Contains(ActiveIndex) ? ActiveIndex : -1;
        }

        if (ActiveIndex == index && !IsIndexAvailable(ActiveIndex))
        {
            SetActiveIndex(FindAvailableActiveIndex(ActiveIndex));
        }
    }

    private bool ApplyInputSelection(int index, Modifiers modifiers)
    {
        Debug.Assert(index >= 0, "ListView input selection index is non-negative.");

        if (SelectionMode == ListSelectionMode.None)
        {
            return false;
        }

        var (next, nextAnchor) = SelectionGesture<int>.Resolve(
            EqualityComparer<int>.Default,
            _selection,
            _selectionAnchor >= 0,
            _selectionAnchor,
            index,
            modifiers,
            SelectionMode == ListSelectionMode.Multiple,
            ShiftEligibleRange);

        var accepted = ApplySelection(next, cancellable: true) || _selection.SetEquals(next);

        if (accepted)
        {
            _selectionAnchor = nextAnchor;
        }

        return accepted;
    }

    private IEnumerable<int> ShiftEligibleRange(int anchor, int target)
    {
        for (var item = Math.Min(anchor, target); item <= Math.Max(anchor, target); item++)
        {
            if (IsIndexAvailable(item))
            {
                yield return item;
            }
        }
    }

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Bubble)
        {
            return;
        }

        if (eventArgs.IsInitialKeyDown &&
            (eventArgs.Stroke.Code == Code.Enter ||
             (eventArgs.Stroke.Code == Code.Character &&
              eventArgs.Stroke.Character == new Rune(' ') &&
              KeyboardModifierPolicy.IsCollectionSelectionEligible(eventArgs.Stroke.Modifiers))))
        {
            eventArgs.IsHandled = ActivateCurrent(
                ActivationCause.Keyboard,
                eventArgs.Stroke.Code,
                eventArgs.Stroke.Modifiers);
            return;
        }

        if (!eventArgs.IsKeyDown)
        {
            return;
        }

        if (FindItem(eventArgs.OriginalSource) is null && eventArgs.OriginalSource != this)
        {
            return;
        }

        eventArgs.IsHandled = MoveSelection(eventArgs.Stroke.Code);
    }

    /// <summary>Moves selection and current item through the shared keyboard-navigation transaction,
    /// then scrolls the committed row into view.</summary>
    /// <param name="code">The directional, endpoint, or paging navigation key.</param>
    /// <returns><see langword="true"/> when the key resolved an eligible target; otherwise,
    /// <see langword="false"/>.</returns>
    internal bool MoveSelection(Code code)
    {
        var target = ResolveMove(code);

        if (target is null)
        {
            return false;
        }

        if (SelectionMode != ListSelectionMode.None && !ApplyInputSelection(target.Index, Modifiers.None))
        {
            if (SelectionMode == ListSelectionMode.None)
            {
                _ = TryCommitCurrent(target);
            }

            return true;
        }

        _ = TryCommitCurrent(target);
        return true;
    }

    private ListItem? ResolveMove(Code code)
    {
        var current = ActiveIndex >= 0 && ActiveIndex < _items.Count && IsIndexAvailable(ActiveIndex)
            ? ActiveIndex
            : FindEligible(0, 1);

        return current < 0 ? null : ResolveNavigation(current, code);
    }

    private bool TryCommitCurrent(ListItem target)
    {
        if (!ReferenceEquals(ItemAt(target.Index), target))
        {
            return false;
        }

        CommitCurrent(target);
        return true;
    }

    private void CommitCurrent(ListItem target)
    {
        Debug.Assert(target is not null, "ListView navigation commits a realized item.");
        SetActiveIndex(target.Index);
        _ = _stack.BringIntoView(target);
    }

    /// <summary>Synchronizes active, anchor, and deferred visibility state after a programmatic
    /// selection target commits.</summary>
    /// <param name="index">The committed selected index.</param>
    private void CommitProgrammaticSelectionTarget(int index)
    {
        SetActiveIndex(index);
        _selectionAnchor = index;
        _pendingSelectionReveal = index;
        RevealPendingSelection();
    }

    /// <summary>Completes a programmatic selection's visibility request once a non-empty viewport
    /// exists; selection may be assigned before the first layout transaction.</summary>
    private void RevealPendingSelection()
    {
        if (_pendingSelectionReveal < 0)
        {
            return;
        }

        if (_pendingSelectionReveal >= Items.Count || !_selection.Contains(_pendingSelectionReveal))
        {
            _pendingSelectionReveal = -1;
            return;
        }

        if (Viewport.Height == 0)
        {
            return;
        }

        var index = _pendingSelectionReveal;
        _pendingSelectionReveal = -1;
        _ = BringIntoView(index);
    }

    private ListItem? ResolveNavigation(int currentIndex, Code code)
    {
        var target = code is Code.Up or Code.Left
            ? FindEligible(currentIndex - 1, -1)
            : code is Code.Down or Code.Right
            ? FindEligible(currentIndex + 1, 1)
            : code == Code.Home
            ? FindEligible(0, 1)
            : code == Code.End
            ? FindEligible(Items.Count - 1, -1)
            : code == Code.PageUp
            ? FindEligible(StepPage(currentIndex, -1), -1)
            : code == Code.PageDown ? FindEligible(StepPage(currentIndex, 1), 1) : -1;

        return target < 0 ? null : Realize(target);
    }

    // In eager mode every realized row's arranged Bounds.Height is already available, so a page
    // step accumulates realized row heights from the current index until the sum reaches the
    // committed viewport height (minus the retained overlap), rather than applying a cell-space
    // distance directly as an item-index delta. Once RowHeight is set every row is the same fixed
    // height, so the same distance becomes pure arithmetic requiring no realized row at all. The
    // returned index is raw and possibly out of range; FindEligible resolves it.
    [Pure]
    private int StepPage(int start, int direction)
    {
        var target = PagingStep.TargetExtent(Viewport.Height, PageOverlap);

        return RowHeight is int height
            ? start + (direction * Math.Max(1, (target + height - 1) / height))
            : PagingStep.Accumulate(start, direction, Items.Count, target, index => ItemAt(index)?.Bounds.Height ?? 0, clamp: false);
    }

    /// <summary>Scans for the nearest eligible logical index without requiring it to be realized.</summary>
    /// <param name="start">The starting index; clamped into range before scanning.</param>
    /// <param name="direction">Plus or minus one.</param>
    /// <returns>The found index, or -1 when no eligible index exists in that direction.</returns>
    [Pure]
    private int FindEligible(int start, int direction)
    {
        var clamped = Math.Clamp(start, 0, Math.Max(0, Items.Count - 1));
        return SingleSelectionIndex.FindLinear(clamped, direction, Items.Count, IsIndexAvailable);
    }

    private void SetActiveIndex(int value)
    {
        RemapPendingSelectionReveal(value);

        if (ActiveIndex == value)
        {
            ItemAt(value)?.SetCurrentState(true);
            return;
        }

        ItemAt(ActiveIndex)?.SetCurrentState(false);
        ActiveIndex = value;
        ItemAt(ActiveIndex)?.SetCurrentState(true);
        NotifyPropertyChanged(nameof(ActiveIndex), InvalidationImpact.Render);
    }

    /// <summary>Moves a deferred visibility request with its selected active item while collection
    /// mutations remap logical indexes before the first non-empty layout.</summary>
    /// <param name="activeIndex">The active index after the collection transaction.</param>
    private void RemapPendingSelectionReveal(int activeIndex)
    {
        if (_pendingSelectionReveal >= 0)
        {
            _pendingSelectionReveal = activeIndex >= 0 && _selection.Contains(activeIndex)
                ? activeIndex
                : -1;
        }
    }

    /// <summary>Gets the realized row for a logical index, or null when the index is unrealized -
    /// always the case in eager mode only when the index is out of range, but a routine and
    /// expected outcome in virtualized mode for any index outside the current window.</summary>
    [Pure]
    private ListItem? ItemAt(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            return null;
        }

        if (RowHeight is null)
        {
            return index >= ItemControlCount ? null : (ListItem) GetItemControl(index);
        }

        for (var position = 0; position < ItemControlCount; position++)
        {
            var candidate = (ListItem) GetItemControl(position);

            if (candidate.Index == index)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Answers whether a logical index is eligible for navigation or selection without
    /// requiring it to be realized.</summary>
    /// <remarks>
    /// A realized row answers with its live <see cref="ListItem.IsAvailable"/>. An unrealized row
    /// in virtualized mode has no live template output to ask, so this optimistically assumes
    /// eligible until a future realization proves otherwise - the same default
    /// <see cref="FindEligible"/> already tolerates when skipping past unavailable rows without
    /// crashing.
    /// </remarks>
    [Pure]
    private bool IsIndexAvailable(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            return false;
        }

        var item = ItemAt(index);
        return item?.IsAvailable ?? (RowHeight is not null && _availability[index] != false);
    }

    /// <summary>Ensures a logical index is realized, scrolling it into view first if necessary.</summary>
    private ListItem Realize(int index)
    {
        Debug.Assert(index >= 0 && index < _items.Count, "Realize requires a valid item index.");
        var existing = ItemAt(index);

        if (existing is not null)
        {
            return existing;
        }

        Debug.Assert(RowHeight is not null, "Every valid index is already realized in eager mode.");
        ScrollIndexIntoView(index, RowHeight.Value);
        Rewindow();
        var realized = ItemAt(index);
        Debug.Assert(realized is not null, "Rewindow realizes any index the viewport now reveals.");
        return realized;
    }

    [Pure]
    private static ListItem? FindItem(ControlBase? source)
    {
        for (var current = source; current is not null; current = current.Parent)
        {
            if (current is ListItem item)
            {
                return item;
            }
        }

        return null;
    }

    [Pure]
    private int IndexOfItem(object value)
    {
        for (var index = 0; index < Items.Count; index++)
        {
            if (Equals(Items[index], value))
            {
                return index;
            }
        }

        return -1;
    }

    private void ValidateIndex(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index is outside Items.");
        }
    }
}
