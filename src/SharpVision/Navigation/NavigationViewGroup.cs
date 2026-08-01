// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using SharpVision.Controls;
using SharpVision.Terminal.Input;

using LayoutStack = Controls.Layout.Stack;

/// <summary>Defines a collapsible labeled group of navigation items.</summary>
[PublicAPI]
public sealed class NavigationViewGroup: Control
{
    private readonly LayoutStack _stack;
    private readonly OwnedControlSlot _childrenSlot;
    private readonly Dictionary<NavigationViewItem, NavigationItemPresentation> _requestedPresentations = [];
    private Rune? _collapsedGlyph;
    private Rune? _expandedGlyph;

    /// <summary>Initializes an expanded navigation group with no header.</summary>
    public NavigationViewGroup()
    {
        _stack = new LayoutStack();
        _childrenSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "group-items",
                InvalidationImpact.Measure),
            capacity: 1);
        _childrenSlot.Add(_stack);
        Items = new NavigationViewItemCollection(this);
        Focusable = false;
        TabStop = false;
    }

    /// <summary>Gets this group's constrained sub-item collection.</summary>
    public NavigationViewItemCollection Items { get; }

    /// <summary>Gets or sets the non-null group label.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value contains a terminal control character.</exception>
    /// <exception cref="InvalidOperationException">The attached group is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The group is disposed.</exception>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            TextValidation.ThrowIfContainsControls(
                value,
                nameof(value),
                "A navigation group header cannot contain terminal controls.");
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = string.Empty;

    /// <inheritdoc/>
    protected override string? AccessKeyText => Header;

    /// <summary>Gets or sets the number of cells used to indent sub-items below the header.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached group is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The group is disposed.</exception>
    public int ItemIndent
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = 2;

    /// <summary>Gets or sets whether sub-items are visible.</summary>
    /// <exception cref="InvalidOperationException">The attached group is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The group is disposed.</exception>
    public bool IsExpanded
    {
        get;
        set
        {
            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                _stack.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                FindNavigationView()?.NotifyGroupVisibilityChanged(this);
            }
        }
    } = true;

    /// <summary>Gets or sets the local collapsed-group marker.</summary>
    public Rune CollapsedGlyph
    {
        get => _collapsedGlyph ?? ControlGlyphs.Navigation.GroupCollapsed.Value;
        set => SetOptionalGlyph(ref _collapsedGlyph, value, nameof(CollapsedGlyph));
    }

    /// <summary>Gets or sets the local expanded-group marker.</summary>
    public Rune ExpandedGlyph
    {
        get => _expandedGlyph ?? ControlGlyphs.Navigation.GroupExpanded.Value;
        set => SetOptionalGlyph(ref _expandedGlyph, value, nameof(ExpandedGlyph));
    }

    /// <summary>Clears both local disclosure glyphs to the code-owned defaults.</summary>
    /// <exception cref="InvalidOperationException">The attached group is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The group is disposed.</exception>
    public void ResetGlyphs()
    {
        VerifyMutable();
        _ = ResetOptionalGlyph(ref _collapsedGlyph, nameof(CollapsedGlyph));
        _ = ResetOptionalGlyph(ref _expandedGlyph, nameof(ExpandedGlyph));
    }

    /// <summary>Gets the number of sub-items.</summary>
    internal int ItemCount => _stack.Children.Count;

    /// <summary>Gets one sub-item by index.</summary>
    internal NavigationViewItem ItemAt(int index) => (NavigationViewItem) _stack.Children[index];

    /// <summary>Adds one sub-item to this group.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    internal void AddItemCore(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        // Ownership is secured before any authored property is captured or
        // overwritten. A rejected insertion (duplicate, already attached,
        // disposed) must leave the caller's object exactly as it found it.
        _stack.Children.Add(item);
        _requestedPresentations.Add(
            item,
            new NavigationItemPresentation(item.Focusable, item.TabStop));

        item.Focusable = false;
        item.TabStop = false;
        item.Invoked += OnItemInvoked;
    }

    /// <summary>Removes one sub-item from this group.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    internal bool RemoveItemCore(NavigationViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!_stack.Children.Contains(item))
        {
            return false;
        }

        // Captured before detachment: FindNavigationView walks the still-attached
        // ancestor chain, and PrepareRemoval needs that chain intact to tell
        // whether the removed item is (or owns) the view's current selection.
        var owner = FindNavigationView();
        var repair = owner?.PrepareRemoval(item);

        item.Invoked -= OnItemInvoked;
        RestorePresentation(item);
        _ = _stack.Children.Remove(item);

        if (repair is { } value)
        {
            owner!.CompleteRemoval(value);
        }

        return true;
    }

    /// <summary>Clears all sub-items.</summary>
    internal void ClearItemsCore()
    {
        var owner = FindNavigationView();
        var repair = owner?.PrepareRemoval(this);

        foreach (var child in _stack.Children)
        {
            if (child is NavigationViewItem item)
            {
                item.Invoked -= OnItemInvoked;
                RestorePresentation(item);
            }
        }

        _stack.Children.Clear();

        if (repair is { } value)
        {
            owner!.CompleteRemoval(value);
        }
    }

    // Runs before the item is detached so callers observe restored values
    // immediately, not this group's private indentation and focus policy.
    private void RestorePresentation(NavigationViewItem item)
    {
        if (!_requestedPresentations.Remove(item, out var presentation))
        {
            return;
        }

        item.Focusable = presentation.Focusable;
        item.TabStop = presentation.TabStop;
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var headerCells = (int) Math.Min(
            int.MaxValue,
            3L + Input.AccessKeyText.Measure(Header, CellPolicy.AmbiguousWidth, UseMnemonic));
        var childConstraint = new Constraint(
            constraint.Width is { } width ? (int) Math.Max(0L, (long) width - ItemIndent) : null,
            constraint.Height);
        var childrenDesired = MeasureChild(_stack, childConstraint);
        var childrenHeight = IsExpanded ? childrenDesired.Height : 0;
        var childrenWidth = (int) Math.Min(int.MaxValue, (long) childrenDesired.Width + ItemIndent);
        return new Size(
            Math.Max(headerCells, childrenWidth),
            (int) Math.Min(int.MaxValue, 1L + childrenHeight));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var indent = Math.Min(ItemIndent, bounds.Width);
        var slot = IsExpanded && bounds.Height > 1
            ? new Rect(bounds.X + indent, bounds.Y + 1, bounds.Width - indent, bounds.Height - 1)
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

        var themed = IsExpanded
            ? ControlGlyphs.Navigation.GroupExpanded
            : ControlGlyphs.Navigation.GroupCollapsed;
        var glyph = CellGlyphResolver.Resolve(IsExpanded ? ExpandedGlyph : CollapsedGlyph, themed.Fallback,
            CellPolicy.AmbiguousWidth);
        var leading = canvas.Draw(
            $" {glyph} ".AsSpan(),
            new Point(Bounds.X, Bounds.Y),
            ResolvedStyle,
            background: BackgroundMode.Transparent);
        _ = Input.AccessKeyText.Draw(
            canvas,
            Header,
            leading.Final,
            ResolvedStyle,
            BackgroundMode.Transparent,
            CellPolicy.AmbiguousWidth,
            UseMnemonic,
            EffectiveIsEnabled ? Theme?.Hotkey ?? Color.Default : null);
    }

    /// <inheritdoc/>
    protected override bool OnAccessKey(Rune key)
    {
        _ = key;
        return FindNavigationView()?.InvokeAccessKey(this) == true;
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
            LocalCells.Y: 0
        } && (buttons & Buttons.Primary) != 0;

        if (keyboard || pointer)
        {
            FindNavigationView()?.NotifyGroupInvoked(this);
            IsExpanded = !IsExpanded;
            eventArgs.Handled = true;
        }
    }

    internal NavigationView? FindNavigationView() => FindAncestor<NavigationView>();

    private void OnItemInvoked(object? sender, ActivationEventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is NavigationViewItem item)
        {
            FindNavigationView()?.NotifyItemInvoked(item);
        }
    }

}
