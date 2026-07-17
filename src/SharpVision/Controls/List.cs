// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Collections.ObjectModel;

using SharpVision.Terminal.Input;

using GenericList = List<object?>;
using Label = Text;

/// <summary>Defines a focusable fully realized item selection control with scrolling.</summary>
[SuppressMessage(
    "Naming",
    "CA1710:Identifiers should have correct suffix",
    Justification = "List is the approved concise terminal control name, not a collection implementation.")]
public sealed class List: ItemsControl
{
    private readonly Stack _stack;
    private readonly GenericList _items = [];
    private readonly ReadOnlyCollection<object?> _itemsView;
    private readonly HashSet<int> _selection = [];
    private readonly GenericList _selectedItems = [];
    private readonly ReadOnlyCollection<object?> _selectedView;
    private int _selectionVersion;
    private int _selectionAnchor = -1;

    /// <summary>Initializes an empty single-selection List with a text template and square semantic surface.</summary>
    public List()
    {
        _itemsView = _items.AsReadOnly();
        _selectedView = _selectedItems.AsReadOnly();
        _stack = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
        };
        InitializeItemsHost(_stack);
        _ = AddHandler(Events.Key, OnKeyRouted);
        BorderThickness = new Thickness(1);
        BorderGlyphs = Glyphs.Light;
        Background = ColorRole.Surface;
        Focusable = true;
        TabStop = true;
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
    /// <exception cref="InvalidOperationException">The attached List is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The List is disposed.</exception>
    public IReadOnlyList<object?> Items
    {
        get => _itemsView;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();
            var copied = Copy(value);
            var realized = Build(copied, ItemTemplate);
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

            var realized = Build(_items, value);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
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
                var retained = normalized.Min();
                normalized.Clear();
                _ = normalized.Add(retained);
            }

            _ = SetProperty(ref field, value, ChangeImpact.Render);
            // Mode transitions invalidate pending proposals even when normalization leaves selection unchanged.
            _selectionVersion++;
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
                SetActiveIndex(value);
                _selectionAnchor = value;
            }
        }
    }

    /// <summary>Gets the lowest selected item, or null when selection is empty or the item is null.</summary>
    public object? SelectedItem => SelectedIndex < 0 ? null : Items[SelectedIndex];

    /// <summary>Gets one stable owner-backed read-only view in ascending index order.</summary>
    public IReadOnlyList<object?> SelectedItems => _selectedView;

    /// <summary>Gets the active navigation and keyboard-selection index, or -1 when no item is active.</summary>
    public int ActiveIndex { get; private set; } = -1;

    /// <summary>Gets the current navigation index, or -1 when no item is current.</summary>
    public int CurrentIndex => ActiveIndex;

    /// <summary>Gets the composed vertical scroll offset.</summary>
    public int VerticalOffset => _stack.VerticalOffset;

    /// <summary>Gets or sets the axes available to the composed overflow host.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached List is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The List is disposed.</exception>
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
            NotifyPropertyChanged(nameof(ScrollBars), ChangeImpact.None);
        }
    }

    /// <summary>Gets or sets the common scrollbar reservation policy for enabled axes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached List is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The List is disposed.</exception>
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
            NotifyPropertyChanged(nameof(ShowScrollBars), ChangeImpact.None);
        }
    }

    /// <summary>Gets or sets the shared compact or full scrollbar chrome form.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached List is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The List is disposed.</exception>
    public ScrollBarChrome ScrollBarChrome
    {
        get => _stack.ScrollBarChrome;
        set
        {
            VerifyMutable();

            if (_stack.ScrollBarChrome == value)
            {
                return;
            }

            _stack.ScrollBarChrome = value;
            NotifyPropertyChanged(nameof(ScrollBarChrome), ChangeImpact.None);
        }
    }

    /// <summary>Gets or sets the generated line or block glyph treatment for composed rails.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached List is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The List is disposed.</exception>
    public ScrollBarFill ScrollBarFill
    {
        get => _stack.ScrollBarFill;
        set
        {
            VerifyMutable();

            if (_stack.ScrollBarFill == value)
            {
                return;
            }

            _stack.ScrollBarFill = value;
            NotifyPropertyChanged(nameof(ScrollBarFill), ChangeImpact.None);
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

        var changed = ApplySelection(next, cancellable: true);

        if (changed && selected)
        {
            SetActiveIndex(index);
            _selectionAnchor = index;
        }

        return changed;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) => MeasureChild(_stack, constraint);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0 || !ControlAppearance.HasOpaqueFill(this, GetAppearanceState()))
        {
            return;
        }

        canvas.Clear(Bounds, ResolvedStyle);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        ArrangeChild(_stack, bounds, ResolvedAxes.Both);

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

        var result = new object?[values.Count];

        for (var index = 0; index < result.Length; index++)
        {
            result[index] = values[index];
        }

        return result;
    }

    private static ListItem[] Build(IReadOnlyList<object?> items, ItemTemplate template)
    {
        Debug.Assert(items is not null, "List build requires a non-null item source.");
        Debug.Assert(template is not null, "List build requires a non-null template.");

        var controls = new Control[items.Count];
        var unique = new HashSet<Control>(ReferenceEqualityComparer.Instance);

        try
        {
            for (var index = 0; index < controls.Length; index++)
            {
                var control = template(items[index]);

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
        Debug.Assert(realized is not null, "List replacement requires realized items.");
        Debug.Assert(realized.Length == items.Count, "Realized items must match the source count.");

        // Re-realization invalidates pending proposals that may still reference an old wrapper.
        _selectionVersion++;
        List<ListItem> previous = [];

        for (var index = 0; index < ItemControlCount; index++)
        {
            var item = (ListItem) GetItemControl(index);
            item.Activated -= OnActivated;
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

        foreach (var item in realized)
        {
            item.Activated += OnActivated;
            item.CommitSelection(_selection.Contains(item.Index));
        }

        HashSet<int> normalized = [.. _selection.Where(index => index < Items.Count)];

        if (SelectionMode == SelectionMode.Single && normalized.Count > 1)
        {
            var retained = normalized.Min();
            normalized.Clear();
            _ = normalized.Add(retained);
        }

        _ = ApplySelection(normalized, cancellable: false);
        SetActiveIndex(ActiveIndex >= Items.Count ? Items.Count - 1 : ActiveIndex);
        _selectionAnchor = _selectionAnchor >= Items.Count ? ActiveIndex : _selectionAnchor;
        RefreshSelectedItems();
        NotifyPropertyChanged(nameof(Items), ChangeImpact.Measure);
    }

    private bool ApplySelection(HashSet<int> next, bool cancellable)
    {
        int[] added = [.. next.Except(_selection).Order()];
        int[] removed = [.. _selection.Except(next).Order()];

        if (added.Length == 0 && removed.Length == 0)
        {
            return false;
        }

        var version = _selectionVersion;

        if (cancellable)
        {
            var changing = new ListSelectionChangingEventArgs(added, removed);
            SelectionChanging?.Invoke(this, changing);

            if (changing.Cancel ||
                version != _selectionVersion ||
                (SelectionMode == SelectionMode.None && next.Count > 0))
            {
                return false;
            }
        }

        _selection.Clear();
        _selection.UnionWith(next);
        _selectionVersion++;

        for (var index = 0; index < ItemControlCount; index++)
        {
            ((ListItem) GetItemControl(index)).CommitSelection(_selection.Contains(index));
        }

        RefreshSelectedItems();
        NotifyPropertyChanged(nameof(SelectedIndex), ChangeImpact.Render);
        NotifyPropertyChanged(nameof(SelectedItem), ChangeImpact.Render);
        NotifyPropertyChanged(nameof(SelectedItems), ChangeImpact.Render);
        SelectionChanged?.Invoke(this, new ListSelectionChangedEventArgs(added, removed));
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
        var index = IndexOfItemControl(item);
        Debug.Assert(index >= 0, "A focused ListItem is a realized child of this List.");

        SetActiveIndex(index);
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
        var current = ItemAt(ActiveIndex) ?? FindEligible(0, 1);

        if (current is not { IsAvailable: true })
        {
            return false;
        }

        current.ActivateFromOwner(cause, key, modifiers);
        return true;
    }

    private void OnActivated(object? sender, ActivationEventArgs eventArgs)
    {
        var item = (ListItem) sender!;
        SetActiveIndex(item.Index);

        if (item.LastKey == Code.Enter)
        {
            ItemInvoked?.Invoke(this, new ItemInvokedEventArgs(item.Index, Items[item.Index], eventArgs.Cause));
            return;
        }

        var modifiers = item.LastModifiers;
        var isSpaceToggle = SelectionMode == SelectionMode.Multiple &&
            eventArgs.Cause == ActivationCause.Keyboard &&
            (modifiers & (Modifiers.Control | Modifiers.Shift)) == 0;

        _ = ApplyInputSelection(item.Index, isSpaceToggle ? modifiers | Modifiers.Control : modifiers);

        if (eventArgs.Cause == ActivationCause.Pointer)
        {
            ItemInvoked?.Invoke(this, new ItemInvokedEventArgs(item.Index, Items[item.Index], eventArgs.Cause));
        }
    }

    private bool ApplyInputSelection(int index, Modifiers modifiers)
    {
        Debug.Assert(index >= 0, "List input selection index is non-negative.");

        if (SelectionMode == SelectionMode.None)
        {
            return false;
        }

        HashSet<int> next = [.. _selection];
        var control = (modifiers & Modifiers.Control) != 0;
        var shift = (modifiers & Modifiers.Shift) != 0;
        var nextAnchor = _selectionAnchor;

        if (SelectionMode == SelectionMode.Multiple && shift && _selectionAnchor >= 0)
        {
            next.Clear();

            for (var item = Math.Min(index, _selectionAnchor); item <= Math.Max(index, _selectionAnchor); item++)
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

            nextAnchor = index;
        }
        else
        {
            next.Clear();
            _ = next.Add(index);
            nextAnchor = index;
        }

        var accepted = ApplySelection(next, cancellable: true) || _selection.SetEquals(next);

        if (accepted)
        {
            _selectionAnchor = nextAnchor;
        }

        return accepted;
    }

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Bubble || eventArgs.Stroke.Action != KeyAction.Press)
        {
            return;
        }

        if (eventArgs.Stroke.Code == Code.Enter ||
            (eventArgs.Stroke.Code == Code.Character && eventArgs.Stroke.Character == new Rune(' ')))
        {
            eventArgs.Handled = ActivateCurrent(
                ActivationCause.Keyboard,
                eventArgs.Stroke.Code,
                eventArgs.Stroke.Modifiers);
            return;
        }

        if (FindItem(eventArgs.OriginalSource) is null && eventArgs.OriginalSource != this)
        {
            return;
        }

        eventArgs.Handled = MoveSelection(eventArgs.Stroke.Code);
    }

    private bool MoveSelection(Code code)
    {
        var target = ResolveMove(code);

        if (target is null)
        {
            return false;
        }

        if (SelectionMode != SelectionMode.None && !ApplyInputSelection(target.Index, Modifiers.None))
        {
            if (SelectionMode == SelectionMode.None)
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
        var current = ItemAt(ActiveIndex) ?? FindEligible(0, 1);
        return current is null ? null : ResolveNavigation(current, code);
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
        Debug.Assert(target is not null, "List navigation commits a realized item.");
        SetActiveIndex(target.Index);
        _ = _stack.BringIntoView(target);
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

        var page = Math.Max(1, _stack.Viewport.Height);

        return code == Code.PageUp
            ? FindEligible(current.Index - page, -1)
            : code == Code.PageDown
            ? FindEligible(current.Index + page, 1)
            : null;
    }

    private ListItem? FindEligible(int start, int direction)
    {
        for (var index = Math.Clamp(start, 0, Math.Max(0, Items.Count - 1));
            index >= 0 && index < Items.Count;
            index += direction)
        {
            var item = ItemAt(index);

            if (item?.IsAvailable == true)
            {
                return item;
            }
        }

        return null;
    }

    private void SetActiveIndex(int value)
    {
        if (ActiveIndex == value)
        {
            return;
        }

        ItemAt(ActiveIndex)?.SetCurrentState(false);
        ActiveIndex = value;
        ItemAt(ActiveIndex)?.SetCurrentState(true);
        NotifyPropertyChanged(nameof(ActiveIndex), ChangeImpact.Render);
    }

    private ListItem? ItemAt(int index) => index < 0 || index >= ItemControlCount ? null : (ListItem) GetItemControl(index);

    private static ListItem? FindItem(Control? source)
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

    private void ValidateIndex(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index is outside Items.");
        }
    }
}
