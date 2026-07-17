// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Input;

/// <summary>Defines a collapsible labeled group of navigation items.</summary>
public sealed class NavigationViewGroup: Control
{
    private readonly Stack _stack;
    private readonly OwnedControlSlot _childrenSlot;
    private Rune? _collapsedGlyph;
    private Rune? _expandedGlyph;

    /// <summary>Initializes an expanded navigation group with no header.</summary>
    public NavigationViewGroup()
    {
        _stack = new Stack();
        _childrenSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "group-items",
                ChangeImpact.Measure),
            capacity: 1);
        _childrenSlot.Add(_stack);
        Focusable = false;
        TabStop = false;
    }

    /// <summary>Gets or sets the non-null group label.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached group is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The group is disposed.</exception>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = string.Empty;

    /// <summary>Gets or sets whether sub-items are visible.</summary>
    /// <exception cref="InvalidOperationException">The attached group is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The group is disposed.</exception>
    public bool IsExpanded
    {
        get;
        set
        {
            if (SetProperty(ref field, value, ChangeImpact.Measure))
            {
                _stack.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                FindNavigationView()?.NotifyGroupVisibilityChanged(this);
            }
        }
    } = true;

    /// <summary>Gets or sets the local collapsed-group marker.</summary>
    public Rune CollapsedGlyph { get => _collapsedGlyph ?? ResolveThemeGlyphs().Navigation.GroupCollapsed.Value; set => SetGlyph(ref _collapsedGlyph, value, nameof(CollapsedGlyph)); }

    /// <summary>Gets or sets the local expanded-group marker.</summary>
    public Rune ExpandedGlyph { get => _expandedGlyph ?? ResolveThemeGlyphs().Navigation.GroupExpanded.Value; set => SetGlyph(ref _expandedGlyph, value, nameof(ExpandedGlyph)); }

    /// <summary>Clears both local disclosure glyphs so the active theme supplies them.</summary>
    /// <exception cref="InvalidOperationException">The attached group is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The group is disposed.</exception>
    public void ResetGlyphs()
    {
        VerifyMutable();

        if (_collapsedGlyph.HasValue)
        {
            _collapsedGlyph = null;
            NotifyPropertyChanged(nameof(CollapsedGlyph), ChangeImpact.Render);
        }

        if (_expandedGlyph.HasValue)
        {
            _expandedGlyph = null;
            NotifyPropertyChanged(nameof(ExpandedGlyph), ChangeImpact.Render);
        }
    }

    /// <summary>Gets the number of sub-items.</summary>
    internal int ItemCount => _stack.Children.Count;

    /// <summary>Gets one sub-item by index.</summary>
    internal NavigationViewItem ItemAt(int index) => (NavigationViewItem) _stack.Children[index];

    /// <summary>Adds one sub-item to this group.</summary>
    public void AddItem(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Padding = new Thickness(2, 0, 0, 0);
        item.Focusable = false;
        item.TabStop = false;
        _stack.Children.Add(item);
    }

    /// <summary>Removes one sub-item from this group.</summary>
    public bool RemoveItem(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _stack.Children.Remove(item);
    }

    /// <summary>Clears all sub-items.</summary>
    public void ClearItems() => _stack.Children.Clear();

    /// <inheritdoc/>

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var headerCells = (int) Math.Min(int.MaxValue, 3L + Terminal.Unicode.Width.Measure(Header).Cells);
        var childrenDesired = MeasureChild(_stack, constraint);
        var childrenHeight = IsExpanded ? childrenDesired.Height : 0;
        return new Size(
            Math.Max(headerCells, childrenDesired.Width),
            (int) Math.Min(int.MaxValue, 1L + childrenHeight));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var slot = IsExpanded && bounds.Height > 1
            ? new Rect(bounds.X, bounds.Y + 1, bounds.Width, bounds.Height - 1)
            : default;
        ArrangeChild(_stack, slot, ResolvedAxes.Both);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var themed = IsExpanded ? ResolveThemeGlyphs().Navigation.GroupExpanded : ResolveThemeGlyphs().Navigation.GroupCollapsed;
        var glyph = CellGlyph.Resolve(IsExpanded ? ExpandedGlyph : CollapsedGlyph, themed.Fallback, CellPolicy.AmbiguousWidth);
        _ = canvas.Draw(
            $" {glyph} {Header}".AsSpan(),
            new Point(Bounds.X, Bounds.Y),
            ResolvedStyle,
            background: BackgroundMode.Transparent);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (eventArgs.Handled)
        {
            return;
        }

        var keyboard = eventArgs is KeyEventArgs { Stroke: { Action: KeyAction.Press, Code: Code.Enter } };
        var pointer = eventArgs is PointerEventArgs
        {
            Pointer.Action: PointerAction.Release,
            Pointer.Buttons: var buttons,
            LocalCells.Y: 0,
        } && (buttons & Buttons.Primary) != 0;

        if (keyboard || pointer)
        {
            FindNavigationView()?.NotifyGroupInvoked(this);
            IsExpanded = !IsExpanded;
            eventArgs.Handled = true;
        }
    }

    private NavigationView? FindNavigationView()
    {
        for (var current = Parent; current is not null; current = current.Parent)
        {
            if (current is NavigationView view)
            {
                return view;
            }
        }

        return null;
    }

    private void SetGlyph(ref Rune? storage, Rune value, string propertyName)
    {
        _ = new ThemedGlyph(value, value);
        VerifyMutable();
        if (storage == value) { return; }
        storage = value;
        NotifyPropertyChanged(propertyName, ChangeImpact.Render);
    }
}
