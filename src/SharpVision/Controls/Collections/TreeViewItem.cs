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
    private bool? _isChecked = false;
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

    internal Modifiers LastModifiers { get; private set; }

    internal void SetCheckState(bool? value, ActivationCause cause, bool propagate)
    {
        var affected = CollectAffectedCheckStateItems(propagate);
        var previousStates = affected.ToDictionary(item => item, item => item.GetEffectiveCheckState());

        SetCheckStateCore(value, propagate);

        foreach (var item in affected)
        {
            var current = item.GetEffectiveCheckState();
            if (previousStates[item] == current)
            {
                continue;
            }

            item.RaiseCheckStateChanged(previousStates[item], current, cause);
            item.FindTreeView()?.NotifyCheckStateChanged(item);
        }
    }

    private void SetCheckStateCore(bool? value, bool propagate)
    {
        _isChecked = value;

        if (propagate)
        {
            foreach (var child in Children)
            {
                if (child.IsCheckable)
                {
                    child.SetCheckStateCore(value, propagate: true);
                }
            }
        }
    }

    private List<TreeViewItem> CollectAffectedCheckStateItems(bool propagate)
    {
        var affected = new List<TreeViewItem>();
        AddAffectedCheckStateItem(this, affected);

        if (propagate)
        {
            foreach (var child in Children)
            {
                if (child.IsCheckable)
                {
                    child.AddCheckStateSubtree(affected);
                }
            }
        }

        for (var parent = ParentCollection?.ParentItem; parent is not null; parent = parent.ParentCollection?.ParentItem)
        {
            AddAffectedCheckStateItem(parent, affected);
        }

        return affected;
    }

    private void AddCheckStateSubtree(List<TreeViewItem> affected)
    {
        AddAffectedCheckStateItem(this, affected);
        foreach (var child in Children)
        {
            if (child.IsCheckable)
            {
                child.AddCheckStateSubtree(affected);
            }
        }
    }

    private static void AddAffectedCheckStateItem(TreeViewItem item, List<TreeViewItem> affected)
    {
        if (!affected.Contains(item))
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
        if (!IsCheckable || !HasChildren)
        {
            return _isChecked;
        }

        bool? common = null;
        var hasCheckableChild = false;
        var hasState = false;

        foreach (var child in Children)
        {
            if (!child.IsCheckable)
            {
                continue;
            }

            var childState = child.GetEffectiveCheckState();
            hasCheckableChild = true;

            if (childState is null || (hasState && common != childState))
            {
                return null;
            }

            if (!hasState)
            {
                common = childState;
                hasState = true;
            }
        }

        return hasCheckableChild ? common : _isChecked;
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
