// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using SharpVision.Controls.Input;
using SharpVision.Terminal.Input;

/// <summary>Defines one selectable, optionally expandable entry in a <see cref="TreeView"/>.</summary>
[PublicAPI]
public sealed class TreeViewItem: Control
{
    // These dimensions are terminal-cell invariants for the compact tree row chrome.
    private const int _rowHeightCells = 1;
    private const int _disclosureWidthCells = 1;
    private const int _checkGlyphWidthCells = 2;
    private const int _headerLeadingSpaceCells = 1;
    // A row reserves one terminal cell between its chrome and header text.
    private const int _headerGapCells = 1;
    private const int _defaultIndentCells = 2;

    private bool _isSelected;
#pragma warning disable IDE0032 // Propagation assigns this field across instances.
    private bool? _isChecked = false;
#pragma warning restore IDE0032

    /// <summary>Gets this item's own stored check state, ignoring any descendant aggregate.</summary>
    internal bool? OwnCheckState => _isChecked;
    private CheckBoxGlyphs? _checkGlyphs;

    /// <summary>Initializes a tree view item with a fixed one-cell height.</summary>
    public TreeViewItem()
    {
        Height = Length.Cells(_rowHeightCells);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Focusable = false;
        TabStop = false;
        Children = new TreeViewItemCollection(this);
        IsEnabledChanged += OnIsEnabledChanged;
    }

    /// <summary>Initializes a tree view item with the specified header text.</summary>
    /// <param name="header">The non-null display text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="header"/> is null.</exception>
    public TreeViewItem(string header)
        : this()
    {
        ArgumentNullException.ThrowIfNull(header);
        Header = header;
    }

    /// <summary>Raised after keyboard or pointer activation requests invocation.</summary>
    public event EventHandler<ActivationEventArgs>? Invoked;

    /// <summary>Raised after the <see cref="IsExpanded"/> state changes.</summary>
    public event EventHandler? ExpandedChanged;

    /// <summary>Raised after this item or one of its descendants changes check state.</summary>
    public event EventHandler<CheckChangedEventArgs>? CheckStateChanged;

    /// <summary>Gets or sets the non-null display text.</summary>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = string.Empty;

    /// <summary>Gets or sets whether child items are visible.</summary>
    public bool IsExpanded
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            NotifyPropertyChanged(nameof(IsExpanded), InvalidationImpact.None);
            ExpandedChanged?.Invoke(this, EventArgs.Empty);
            FindTreeView()?.NotifyStructureChanged();
        }
    } = true;

    /// <summary>Gets the child item collection.</summary>
    public TreeViewItemCollection Children { get; }

    /// <summary>Gets or sets whether this item displays and responds to a check mark.</summary>
    public bool IsCheckable
    {
        get;
        set
        {
            var previousStates = new List<(TreeViewItem Item, bool? State)>();
            for (var item = this; item is not null; item = item.ParentCollection?.ParentItem)
            {
                previousStates.Add((item, item.GetEffectiveCheckState()));
            }

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                foreach (var (item, previous) in previousStates)
                {
                    var current = item.GetEffectiveCheckState();
                    if (previous != current)
                    {
                        item.RaiseCheckStateChanged(previous, current, ActivationCause.Programmatic);
                    }
                }

                FindTreeView()?.NotifyCheckStateChanged(this);
            }
        }
    }

    /// <summary>Gets or sets the checked, unchecked, or indeterminate state.</summary>
    /// <exception cref="InvalidOperationException">The item is not checkable.</exception>
    public bool? IsChecked
    {
        get => GetEffectiveCheckState();
        set
        {
            if (!IsCheckable)
            {
                throw new InvalidOperationException("Only checkable tree items have a check state.");
            }

            SetCheckState(value, ActivationCause.Programmatic, propagate: true);
        }
    }

    /// <summary>Gets or sets the three one-cell glyphs used for check states.</summary>
    public CheckBoxGlyphs CheckGlyphs
    {
        get => _checkGlyphs ?? CheckBoxGlyphs.Default;
        set
        {
            if (SetOptionalValue(ref _checkGlyphs, value, CheckBoxGlyphs.Default))
            {
                Invalidate(InvalidationImpact.Render);
            }
        }
    }

    /// <summary>Gets whether this item is the tree view's selected item.</summary>
    public bool IsSelected => _isSelected;

    /// <summary>Gets whether this item has any children.</summary>
    public bool HasChildren => Children.Count > 0;

    /// <summary>Gets the nesting depth, set internally by the owning tree view.</summary>
    public int Depth { get; internal set; }

    /// <summary>Commits the visual selected state from the containing tree view.</summary>
    internal void CommitSelection(bool value) =>
        _ = SetVisualStateProperty(ref _isSelected, value, nameof(IsSelected));

    /// <summary>Activates this item on behalf of its focus-owning tree view.</summary>
    internal void ActivateFromOwner(ActivationCause cause) =>
        Invoked?.Invoke(this, new ActivationEventArgs(cause));

    /// <inheritdoc/>
    protected override bool IsSelectedState => _isSelected;

    /// <inheritdoc/>
    protected override bool IsCheckedState => IsChecked == true;

    /// <inheritdoc/>
    protected override bool IsIndeterminateState => IsChecked is null && IsCheckable;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        var indent = FindTreeView()?.Indent ?? _defaultIndentCells;
        var checkWidth = IsCheckable ? _checkGlyphWidthCells : 0;
        return new Size(
            (int) Math.Min(
                int.MaxValue,
                ((long) Depth * indent) + _headerGapCells + checkWidth + Terminal.Unicode.Width.Measure(Header).Cells),
            _rowHeightCells);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;

        if (ControlAppearance.HasOpaqueFill(this, GetAppearanceState()))
        {
            canvas.Clear(Bounds, style);
        }

        var bounds = ContentBounds;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        var indent = FindTreeView()?.Indent ?? _defaultIndentCells;
        var indentCells = Depth * indent;
        var clipped = canvas.Clip(bounds);

        var x = bounds.X;

        if (indentCells > 0)
        {
            var leading = clipped.Draw(
                new string(' ', indentCells).AsSpan(),
                new Point(x, bounds.Y),
                style,
                background: BackgroundMode.Transparent);
            x = leading.Final.X;
        }

        if (HasChildren)
        {
            var themed = IsExpanded
                ? ControlGlyphs.Disclosure.Expanded
                : ControlGlyphs.Disclosure.Collapsed;
            canvas.DrawRune(
                CellGlyphResolver.Resolve(
                    IsExpanded
                        ? ControlGlyphs.Disclosure.Expanded.Value
                        : ControlGlyphs.Disclosure.Collapsed.Value,
                    themed.Fallback,
                    CellPolicy.AmbiguousWidth),
                new Point(x, bounds.Y),
                style,
                BackgroundMode.Transparent);
            x += _disclosureWidthCells;
        }
        else
        {
            var leading = clipped.Draw(
                " ".AsSpan(),
                new Point(x, bounds.Y),
                style,
                background: BackgroundMode.Transparent);
            x = leading.Final.X;
        }

        if (IsCheckable)
        {
            var glyphs = CheckGlyphs;
            var checkGlyph = IsChecked switch
            {
                true => glyphs.Checked,
                false => glyphs.Unchecked,
                null => glyphs.Indeterminate
            };
            canvas.DrawRune(
                CellGlyphResolver.Resolve(checkGlyph, CheckBoxGlyphs.Default.Unchecked, CellPolicy.AmbiguousWidth),
                new Point(x, bounds.Y),
                style,
                BackgroundMode.Transparent);
            x += _checkGlyphWidthCells;
        }

        if (bounds.Width > x - bounds.X + _headerLeadingSpaceCells)
        {
            var headerClipped = canvas.Clip(new Rect(x, bounds.Y, bounds.Width - (x - bounds.X), _rowHeightCells));
            _ = headerClipped.Draw(
                $" {Header}".AsSpan(),
                new Point(x, bounds.Y),
                style,
                background: BackgroundMode.Transparent);
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

        if (eventArgs is PointerEventArgs
            {
                Pointer: var pointer,
                Pointer.Action: PointerAction.Press,
                Pointer.Buttons: var buttons,
                Pointer.Cells: { } cells
            } && (buttons & Buttons.Primary) != 0 && Bounds.Contains(cells))
        {
            var indent = FindTreeView()?.Indent ?? _defaultIndentCells;
            var glyphX = ContentBounds.X + (Depth * indent);

            if (HasChildren && cells.X == glyphX)
            {
                IsExpanded = !IsExpanded;
                eventArgs.Handled = true;
                return;
            }

            var checkX = glyphX + _disclosureWidthCells;

            if (IsCheckable && cells.X == checkX)
            {
                SetCheckState(IsChecked != true, ActivationCause.Pointer, propagate: true);
                eventArgs.Handled = true;
                return;
            }

            LastModifiers = pointer.Modifiers;
            FindTreeView()?.NotifyItemInvoked(this, ActivationCause.Pointer, pointer.Modifiers);
            eventArgs.Handled = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            Invoked = null;
            ExpandedChanged = null;
            CheckStateChanged = null;
        }
    }

    internal TreeView? FindTreeView() => ParentCollection?.Owner ?? FindAncestor<TreeView>();

    internal TreeViewItemCollection? ParentCollection { get; set; }

    private void OnIsEnabledChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        FindTreeView()?.NotifyItemEnabledChanged(this);
    }

    [ThreadStatic]
    private static List<CheckStateFrame>? _checkStateWalk;

    internal Modifiers LastModifiers { get; private set; }

    internal void SetCheckState(bool? value, ActivationCause cause, bool propagate)
    {
        var affected = CollectAffectedCheckStateItems(propagate);

        // One memoized pass per snapshot. Evaluating each affected item independently re-walked its
        // whole subtree, so a chain-shaped tree cost O(n^2) aggregations for a single toggle.
        var previousStates = EvaluateStates(affected);

        SetCheckStateCore(value, propagate);
        var currentStates = EvaluateStates(affected);

        foreach (var item in affected)
        {
            var previous = previousStates[item];
            var current = currentStates[item];

            if (previous == current)
            {
                continue;
            }

            item.RaiseCheckStateChanged(previous, current, cause);
            item.FindTreeView()?.NotifyCheckStateChanged(item);
        }
    }

    private static Dictionary<TreeViewItem, bool?> EvaluateStates(List<TreeViewItem> affected)
    {
        var memo = new Dictionary<TreeViewItem, bool?>(affected.Count);

        foreach (var item in affected)
        {
            _ = EvaluateEffective(item, memo);
        }

        return memo;
    }

    private static bool? EvaluateEffective(TreeViewItem item, Dictionary<TreeViewItem, bool?> memo)
    {
        if (memo.TryGetValue(item, out var cached))
        {
            return cached;
        }

        if (!item.IsCheckable || !item.HasChildren)
        {
            memo[item] = item._isChecked;

            return item._isChecked;
        }

        // Iterative post-order sharing one memo across every affected item, so the whole snapshot
        // costs one pass over the touched nodes instead of one pass per node.
        List<CheckStateFrame> walk = [new CheckStateFrame(item)];
        bool? resolved = null;

        while (walk.Count > 0)
        {
            var index = walk.Count - 1;
            var frame = walk[index];

            if (frame.TryTakeNextCheckableChild(out var child))
            {
                walk[index] = frame;

                if (memo.TryGetValue(child, out var known))
                {
                    frame = walk[index];
                    frame.Accumulate(known);
                    walk[index] = frame;
                }
                else if (child.HasChildren)
                {
                    walk.Add(new CheckStateFrame(child));
                }
                else
                {
                    memo[child] = child._isChecked;
                    frame = walk[index];
                    frame.Accumulate(child._isChecked);
                    walk[index] = frame;
                }

                continue;
            }

            resolved = frame.Resolve();
            memo[frame.Item] = resolved;
            walk.RemoveAt(index);

            if (walk.Count > 0)
            {
                var parent = walk[^1];
                parent.Accumulate(resolved);
                walk[^1] = parent;
            }
        }

        return resolved;
    }

    private void SetCheckStateCore(bool? value, bool propagate)
    {
        _isChecked = value;

        if (!propagate)
        {
            return;
        }

        // Iterative: propagation descends a caller-controlled hierarchy.
        List<TreeViewItem> pending = [this];

        while (pending.Count > 0)
        {
            var current = pending[^1];
            pending.RemoveAt(pending.Count - 1);

            foreach (var child in current.Children)
            {
                if (child.IsCheckable)
                {
                    child._isChecked = value;
                    pending.Add(child);
                }
            }
        }
    }

    private List<TreeViewItem> CollectAffectedCheckStateItems(bool propagate)
    {
        var affected = new List<TreeViewItem>();
        var seen = new HashSet<TreeViewItem>();
        AddAffectedCheckStateItem(this, affected, seen);

        if (propagate)
        {
            // Iterative: the checkable subtree is caller-controlled and can be arbitrarily deep.
            List<TreeViewItem> pending = [this];

            while (pending.Count > 0)
            {
                var current = pending[^1];
                pending.RemoveAt(pending.Count - 1);

                foreach (var child in current.Children)
                {
                    if (child.IsCheckable)
                    {
                        AddAffectedCheckStateItem(child, affected, seen);
                        pending.Add(child);
                    }
                }
            }
        }

        for (var parent = ParentCollection?.ParentItem; parent is not null; parent = parent.ParentCollection?.ParentItem)
        {
            AddAffectedCheckStateItem(parent, affected, seen);
        }

        return affected;
    }

    private static void AddAffectedCheckStateItem(
        TreeViewItem item,
        List<TreeViewItem> affected,
        HashSet<TreeViewItem> seen)
    {
        // A set instead of List.Contains: the affected set is proportional to the subtree, so the
        // linear scan made propagation quadratic in the number of checkable descendants.
        if (seen.Add(item))
        {
            affected.Add(item);
        }
    }

    private void RaiseCheckStateChanged(bool? previous, bool? current, ActivationCause cause)
    {
        InvalidateVisualState();
        NotifyPropertyChanged(nameof(IsChecked), InvalidationImpact.Render);
        CheckStateChanged?.Invoke(this, new CheckChangedEventArgs(previous, current, cause));
    }

    internal bool? GetEffectiveCheckState()
    {
        // Leaves and non-checkable items answer without touching the walk buffer at all, which is
        // the overwhelmingly common case for the IsChecked getter.
        if (!IsCheckable || !HasChildren)
        {
            return _isChecked;
        }

        // Iterative post-order. A checkable chain is exactly the deep case, so recursion here is
        // the same unrecoverable stack hazard as the other traversals. The buffer is thread-static
        // because controls are dispatcher-affine, so aggregation stays allocation-free once warm.
        var walk = _checkStateWalk ??= [];
        var depth = walk.Count;
        walk.Add(new CheckStateFrame(this));

        while (walk.Count > depth)
        {
            var index = walk.Count - 1;
            var frame = walk[index];

            if (frame.TryTakeNextCheckableChild(out var child))
            {
                walk[index] = frame;

                if (child.IsCheckable && child.HasChildren)
                {
                    walk.Add(new CheckStateFrame(child));
                }
                else
                {
                    frame = walk[index];
                    frame.Accumulate(child._isChecked);
                    walk[index] = frame;
                }

                continue;
            }

            var resolved = frame.Resolve();
            walk.RemoveAt(index);

            if (walk.Count > depth)
            {
                var parent = walk[^1];
                parent.Accumulate(resolved);
                walk[^1] = parent;
            }
            else
            {
                return resolved;
            }
        }

        return _isChecked;
    }

    private static bool SetOptionalValue(ref CheckBoxGlyphs? field, CheckBoxGlyphs value, CheckBoxGlyphs defaultValue)
    {
        if (value.Equals(defaultValue))
        {
            if (field is null)
            {
                return false;
            }

            field = null;
            return true;
        }

        if (field is { } existing && existing.Equals(value))
        {
            return false;
        }

        field = value;
        return true;
    }
}
