// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Collections.ObjectModel;
using System.Globalization;

using SharpVision.Styling;
using SharpVision.Terminal.Input;

using GenericList = List<object?>;
using Label = Text;

/// <summary>Defines a focusable fully realized item selection control with scrolling.</summary>
[SuppressMessage(
    "Naming",
    "CA1710:Identifiers should have correct suffix",
    Justification = "List is the approved concise terminal control name, not a collection implementation.")]
public sealed class List: Container, IStyleScope
{
    private readonly Children _chrome;
    private readonly ScrollView _scroll;
    private readonly Stack _stack;
    private readonly GenericList _items = [];
    private readonly ReadOnlyCollection<object?> _itemsView;
    private readonly HashSet<int> _selection = [];
    private readonly GenericList _selectedItems = [];
    private readonly ReadOnlyCollection<object?> _selectedView;
    private int _selectionVersion;
    private int _selectionAnchor = -1;

    /// <summary>Initializes an empty single-selection List with a text template.</summary>
    public List() : base(capacity: 0)
    {
        _itemsView = _items.AsReadOnly();
        _selectedView = _selectedItems.AsReadOnly();
        _stack = new Stack();
        _scroll = new ScrollView
        {
            Content = _stack,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
        };
        _chrome = new Children(this, capacity: 1)
        {
            _scroll
        };
        _ = AddHandler(Events.Key, OnKeyRouted);
        CanFocus = true;
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
    /// <exception cref="InvalidOperationException">The attached List is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The List is disposed.</exception>
    public IReadOnlyList<object?> Items
    {
        get => _itemsView;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();
            object?[] copied = Copy(value);
            ListItem[] realized = Build(copied, ItemTemplate);
            Replace(copied, realized, replaceItems: true);
        }
    }

    /// <summary>Gets or atomically sets the non-null detached-control factory.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">Candidate output is null, disposed, attached, or duplicated.</exception>
    /// <exception cref="InvalidOperationException">The attached List is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The List is disposed.</exception>
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

            ListItem[] realized = Build(_items, value);
            _ = Set(ref field, value, Invalidation.Measure);
            Replace(_items, realized, replaceItems: false);
        }
    } = DefaultTemplate;

    /// <summary>Gets or sets whether no, one, or many indexes may be selected.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached List is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The List is disposed.</exception>
    public SelectionMode SelectionMode
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The selection mode is unknown.");
            }

            VerifyMutable();

            if (field == value)
            {
                return;
            }

            HashSet<int> normalized = [.. _selection];

            if (value == SelectionMode.None)
            {
                normalized.Clear();
            }
            else if (value == SelectionMode.Single && normalized.Count > 1)
            {
                int retained = normalized.Min();
                normalized.Clear();
                _ = normalized.Add(retained);
            }

            _ = Set(ref field, value, Invalidation.Render);
            _ = ApplySelection(normalized, cancellable: false);
        }
    } = SelectionMode.Single;

    /// <summary>Gets the lowest selected index, or sets one exclusive index; -1 clears selection.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than -1 or outside Items.</exception>
    /// <exception cref="InvalidOperationException">A non-negative value is assigned in None mode.</exception>
    /// <exception cref="ObjectDisposedException">The List is disposed.</exception>
    public int SelectedIndex
    {
        get => _selection.Count == 0 ? -1 : _selection.Min();
        set
        {
            if (value < -1 || value >= Items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "SelectedIndex is outside Items.");
            }

            if (value >= 0 && SelectionMode == SelectionMode.None)
            {
                throw new InvalidOperationException("SelectionMode.None does not accept a selected index.");
            }

            VerifyMutable();
            HashSet<int> next = value < 0 ? [] : [value];
            _ = ApplySelection(next, cancellable: true);

            if (value >= 0 && _selection.Contains(value))
            {
                ActiveIndex = value;
                _selectionAnchor = value;
            }
        }
    }

    /// <summary>Gets the lowest selected item, or null when selection is empty or the item is null.</summary>
    public object? SelectedItem => SelectedIndex < 0 ? null : Items[SelectedIndex];

    /// <summary>Gets one stable owner-backed read-only view in ascending index order.</summary>
    public IReadOnlyList<object?> SelectedItems => _selectedView;

    /// <summary>Gets the active navigation index, or -1 when no item is active.</summary>
    public int ActiveIndex { get; private set; } = -1;

    /// <summary>Gets the composed vertical scroll offset.</summary>
    public new int VerticalOffset => _scroll.VerticalOffset;

    /// <summary>Gets or sets the axes available to the composed overflow host.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached List is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The List is disposed.</exception>
    public new ScrollBars ScrollBars
    {
        get => _scroll.ScrollBars;
        set
        {
            VerifyMutable();

            if (_scroll.ScrollBars == value)
            {
                return;
            }

            _scroll.ScrollBars = value;
            NotifyChanged(nameof(ScrollBars), Invalidation.None);
        }
    }

    /// <summary>Gets or sets the common scrollbar reservation policy for enabled axes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached List is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The List is disposed.</exception>
    public new ShowScrollBars ShowScrollBars
    {
        get => _scroll.ShowScrollBars;
        set
        {
            VerifyMutable();

            if (_scroll.ShowScrollBars == value)
            {
                return;
            }

            _scroll.ShowScrollBars = value;
            NotifyChanged(nameof(ShowScrollBars), Invalidation.None);
        }
    }

    /// <summary>Gets or sets the shared compact or full scrollbar chrome form.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached List is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The List is disposed.</exception>
    public ScrollBarChrome ScrollBarChrome
    {
        get => _scroll.ScrollBarChrome;
        set
        {
            VerifyMutable();

            if (_scroll.ScrollBarChrome == value)
            {
                return;
            }

            _scroll.ScrollBarChrome = value;
            NotifyChanged(nameof(ScrollBarChrome), Invalidation.None);
        }
    }

    /// <summary>Gets or sets the generated line or block glyph treatment for composed rails.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached List is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The List is disposed.</exception>
    public ScrollBarFill ScrollBarFill
    {
        get => _scroll.ScrollBarFill;
        set
        {
            VerifyMutable();

            if (_scroll.ScrollBarFill == value)
            {
                return;
            }

            _scroll.ScrollBarFill = value;
            NotifyChanged(nameof(ScrollBarFill), Invalidation.None);
        }
    }

    /// <summary>Changes one programmatic index without replacing other Multiple selections.</summary>
    /// <param name="index">The contained item index.</param>
    /// <param name="selected">Whether the index should be selected.</param>
    /// <returns>True when a changed selection committed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside Items.</exception>
    /// <exception cref="InvalidOperationException">Selection is requested in None mode.</exception>
    /// <exception cref="ObjectDisposedException">The List is disposed.</exception>
    public bool SetSelected(int index, bool selected)
    {
        ValidateIndex(index);

        if (selected && SelectionMode == SelectionMode.None)
        {
            throw new InvalidOperationException("SelectionMode.None does not accept selection.");
        }

        VerifyMutable();
        HashSet<int> next = [.. _selection];

        if (selected)
        {
            if (SelectionMode == SelectionMode.Single)
            {
                next.Clear();
            }

            _ = next.Add(index);
        }
        else
        {
            _ = next.Remove(index);
        }

        bool changed = ApplySelection(next, cancellable: true);

        if (changed && selected)
        {
            ActiveIndex = index;
            _selectionAnchor = index;
        }

        return changed;
    }

    /// <inheritdoc/>
    public override Control? HitTest(Point point)
    {
        Control? self = base.HitTest(point);

        return self is null ? null : _scroll.HitTestPopup(point) ?? _scroll.HitTest(point) ?? this;
    }

    /// <inheritdoc/>
    internal override int NavigationCount => _chrome.Count;

    /// <inheritdoc/>
    internal override Control NavigationAt(int index) => _chrome[index];

    /// <inheritdoc/>
    internal override void VisitChildren(Action<Control> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        foreach (Control child in _chrome)
        {
            visitor(child);
        }
    }

    /// <inheritdoc/>
    internal override void DisposeChildren()
    {
        while (_chrome.Count > 0)
        {
            Control child = _chrome[^1];
            _chrome.RemoveAt(_chrome.Count - 1);
            child.Dispose();
        }
    }

    /// <inheritdoc/>
    internal override void RenderChildren(TerminalCanvas canvas) => _scroll.Render(canvas);

    /// <inheritdoc/>
    internal override Control? HitTestPopup(Point point) => _scroll.HitTestPopup(point);

    /// <inheritdoc/>
    internal override void RenderPopupLayer(TerminalCanvas canvas) => _scroll.RenderPopupLayer(canvas);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _scroll.Measure(constraint);
        return _scroll.DesiredSize;
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0 || !ControlAppearance.HasOpaqueFill(this, GetVisualState()))
        {
            return;
        }

        canvas.Clear(Bounds, ResolvedStyle);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        _scroll.Arrange(bounds, widthResolved: true, heightResolved: true);

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

    private static Control DefaultTemplate(object? item) => new Label(Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty);

    private static object?[] Copy(IReadOnlyList<object?> values)
    {
        Debug.Assert(values is not null, "List copy requires a non-null source.");

        object?[] result = new object?[values.Count];

        for (int index = 0; index < result.Length; index++)
        {
            result[index] = values[index];
        }

        return result;
    }

    private static ListItem[] Build(IReadOnlyList<object?> items, ItemTemplate template)
    {
        Debug.Assert(items is not null, "List build requires a non-null item source.");
        Debug.Assert(template is not null, "List build requires a non-null template.");

        Control[] controls = new Control[items.Count];
        HashSet<Control> unique = new(ReferenceEqualityComparer.Instance);

        try
        {
            for (int index = 0; index < controls.Length; index++)
            {
                Control? control = template(items[index]);

                if (control is null || !unique.Add(control))
                {
                    throw new ArgumentException("ItemTemplate must return one unique non-null control.", nameof(template));
                }

                if (control.Parent is not null || control.Dispatcher is not null || control.IsDisposed)
                {
                    throw new ArgumentException("ItemTemplate controls must be detached and undisposed.", nameof(template));
                }

                control.ValidateAttachment();
                controls[index] = control;
            }

            ListItem[] result = new ListItem[controls.Length];

            for (int index = 0; index < result.Length; index++)
            {
                result[index] = new ListItem(index, controls[index]);
            }

            Debug.Assert(result.Length == items.Count, "Every item must realize to one ListItem.");

            return result;
        }
        catch
        {
            foreach (Control control in unique)
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
        Debug.Assert(realized is not null, "List replacement requires realized items.");
        Debug.Assert(realized.Length == items.Count, "Realized items must match the source count.");

        while (_stack.Children.Count > 0)
        {
            ListItem previous = (ListItem) _stack.Children[^1];
            previous.Activated -= OnActivated;
            _stack.Children.RemoveAt(_stack.Children.Count - 1);
            previous.Dispose();
        }

        if (replaceItems)
        {
            _items.Clear();

            for (int index = 0; index < items.Count; index++)
            {
                _items.Add(items[index]);
            }
        }

        foreach (ListItem item in realized)
        {
            item.Activated += OnActivated;
            _stack.Children.Add(item);
            item.CommitSelection(_selection.Contains(item.Index));
        }

        HashSet<int> normalized = [.. _selection.Where(index => index < Items.Count)];

        if (SelectionMode == SelectionMode.Single && normalized.Count > 1)
        {
            int retained = normalized.Min();
            normalized.Clear();
            _ = normalized.Add(retained);
        }

        _ = ApplySelection(normalized, cancellable: false);
        ActiveIndex = ActiveIndex >= Items.Count ? Items.Count - 1 : ActiveIndex;
        _selectionAnchor = _selectionAnchor >= Items.Count ? ActiveIndex : _selectionAnchor;
        RefreshSelectedItems();
        NotifyChanged(nameof(Items), Invalidation.Measure);
    }

    private bool ApplySelection(HashSet<int> next, bool cancellable)
    {
        int[] added = [.. next.Except(_selection).Order()];
        int[] removed = [.. _selection.Except(next).Order()];

        if (added.Length == 0 && removed.Length == 0)
        {
            return false;
        }

        int version = _selectionVersion;

        if (cancellable)
        {
            ListSelectionChangingEventArgs changing = new(added, removed);
            SelectionChanging?.Invoke(this, changing);

            if (changing.Cancel || version != _selectionVersion)
            {
                return false;
            }
        }

        _selection.Clear();
        _selection.UnionWith(next);
        _selectionVersion++;

        for (int index = 0; index < _stack.Children.Count; index++)
        {
            ((ListItem) _stack.Children[index]).CommitSelection(_selection.Contains(index));
        }

        RefreshSelectedItems();
        NotifyChanged(nameof(SelectedIndex), Invalidation.Render);
        NotifyChanged(nameof(SelectedItem), Invalidation.Render);
        NotifyChanged(nameof(SelectedItems), Invalidation.Render);
        SelectionChanged?.Invoke(this, new ListSelectionChangedEventArgs(added, removed));
        return true;
    }

    private void RefreshSelectedItems()
    {
        _selectedItems.Clear();

        foreach (int index in _selection.Order())
        {
            if (index < Items.Count)
            {
                _selectedItems.Add(Items[index]);
            }
        }
    }

    private void OnActivated(object? sender, ActivationEventArgs eventArgs)
    {
        ListItem item = (ListItem) sender!;
        ActiveIndex = item.Index;

        if (item.LastKey == Code.Enter)
        {
            ItemInvoked?.Invoke(this, new ItemInvokedEventArgs(item.Index, Items[item.Index], eventArgs.Cause));
            return;
        }

        ApplyInputSelection(item.Index, item.LastModifiers);

        if (eventArgs.Cause == ActivationCause.Pointer)
        {
            ItemInvoked?.Invoke(this, new ItemInvokedEventArgs(item.Index, Items[item.Index], eventArgs.Cause));
        }
    }

    private void ApplyInputSelection(int index, Modifiers modifiers)
    {
        Debug.Assert(index >= 0, "List input selection index is non-negative.");

        if (SelectionMode == SelectionMode.None)
        {
            return;
        }

        HashSet<int> next = [.. _selection];
        bool control = (modifiers & Modifiers.Control) != 0;
        bool shift = (modifiers & Modifiers.Shift) != 0;

        if (SelectionMode == SelectionMode.Multiple && shift && _selectionAnchor >= 0)
        {
            next.Clear();

            for (int item = Math.Min(index, _selectionAnchor); item <= Math.Max(index, _selectionAnchor); item++)
            {
                _ = next.Add(item);
            }
        }
        else if (SelectionMode == SelectionMode.Multiple && control)
        {
            if (!next.Remove(index))
            {
                _ = next.Add(index);
            }

            _selectionAnchor = index;
        }
        else
        {
            next.Clear();
            _ = next.Add(index);
            _selectionAnchor = index;
        }

        _ = ApplySelection(next, cancellable: true);
    }

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Bubble || eventArgs.Stroke.Action != KeyAction.Press)
        {
            return;
        }

        ListItem? current = FindItem(eventArgs.OriginalSource) ?? ItemAt(ActiveIndex);

        if (current is null)
        {
            return;
        }

        ListItem? target = ResolveNavigation(current, eventArgs.Stroke.Code);

        if (target is null)
        {
            return;
        }

        ActiveIndex = target.Index;
        _ = FocusOwner?.Focus(target);
        _ = _scroll.BringIntoView(target);
        eventArgs.Handled = true;
    }

    private ListItem? ResolveNavigation(ListItem current, Code code)
    {
        if (code is Code.Up or Code.Left)
        {
            return FindEligible(current.Index - 1, -1);
        }

        if (code is Code.Down or Code.Right)
        {
            return FindEligible(current.Index + 1, 1);
        }

        if (code == Code.Home)
        {
            return FindEligible(0, 1);
        }

        if (code == Code.End)
        {
            return FindEligible(Items.Count - 1, -1);
        }

        int page = Math.Max(1, _scroll.Viewport.Height);

        return code == Code.PageUp
            ? FindEligible(current.Index - page, -1)
            : code == Code.PageDown
            ? FindEligible(current.Index + page, 1)
            : null;
    }

    private ListItem? FindEligible(int start, int direction)
    {
        for (int index = Math.Clamp(start, 0, Math.Max(0, Items.Count - 1));
            index >= 0 && index < Items.Count;
            index += direction)
        {
            ListItem? item = ItemAt(index);

            if (item?.IsAvailable == true)
            {
                return item;
            }
        }

        return null;
    }

    private ListItem? ItemAt(int index) => index < 0 || index >= _stack.Children.Count ? null : (ListItem) _stack.Children[index];

    private static ListItem? FindItem(Control? source)
    {
        for (Control? current = source; current is not null; current = current.Parent)
        {
            if (current is ListItem item)
            {
                return item;
            }
        }

        return null;
    }

    private void ValidateIndex(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index is outside Items.");
        }
    }
}
