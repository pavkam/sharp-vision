// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using SharpVision.Controls;
using SharpVision.Terminal.Input;
using SharpVision.Text;

using LayoutStack = Controls.Layout.Stack;

/// <summary>Defines a collapsible labeled group of navigation items.</summary>
[PublicAPI]
public sealed class NavigationViewGroup: ControlBase, IStyled<NavigationViewGroupStyle>
{
    private readonly LayoutStack _stack;
    private readonly OwnedControlSlot _childrenSlot;
    private readonly RetainedPropertyOverrideService _propertyOverrides;
    private readonly PressBehavior _press;
    private readonly StyleSlot<NavigationViewGroupStyle> _style;

    /// <summary>Initializes an expanded navigation group with no header.</summary>
    public NavigationViewGroup()
    {
        EnableChromeAuthoring();
        _style = InitializeStyle(NavigationViewGroupStyle.Definition);
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
        _propertyOverrides = new RetainedPropertyOverrideService(this, _stack.Children.OwnedSlot);
        Items = new NavigationViewItemCollection(this);
        IsFocusable = false;
        IsTabStop = false;
        _press = new PressBehavior(
            () => new Rect(
                ContentBounds.X,
                ContentBounds.Y,
                ContentBounds.Width,
                Math.Min(1, ContentBounds.Height)),
            () => !IsDisposed && EffectiveIsEnabled && EffectiveIsVisible,
            () => true,
            () => FindNavigationView()?.Focus() == true,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed,
            Activate,
            () => Capabilities.KeyReleaseEvents.Authoritative);
        RegisterLifecycleParticipant(_press);
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
            ArgumentException.ThrowIfContainsControls(value, nameof(value), "A navigation group header cannot contain terminal controls.");
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = string.Empty;

    /// <inheritdoc/>
    protected override string? AccessKeyText => Header;

    /// <summary>Gets or sets whether sub-items are visible.</summary>
    /// <exception cref="InvalidOperationException">The attached group is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The group is disposed.</exception>
    public bool IsExpanded
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
                    _stack.Visibility = IsExpanded ? Visibility.Visible : Visibility.Collapsed;
                    FindNavigationView()?.NotifyGroupVisibilityChanged(this);
                });
        }
    } = true;

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached group is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The group is disposed.</exception>
    public NavigationViewGroupStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public NavigationViewGroupStyle ActualStyle => _style.Actual;

    /// <summary>Gets the number of sub-items.</summary>
    internal int ItemCount => _stack.Children.Count;

    /// <summary>Gets the retained authored-presentation count used to prove metadata retires with ownership.</summary>
    internal int RequestedPresentationCount => _propertyOverrides.Count;

    /// <summary>Verifies the owner before a public collection validates candidate-specific state.</summary>
    internal void VerifyMutation() => VerifyMutable();

    /// <summary>Gets one sub-item by index.</summary>
    internal NavigationViewItem ItemAt(int index) => (NavigationViewItem) _stack.Children[index];

    /// <summary>Adds one sub-item to this group.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    internal void AddItemCore(NavigationViewItem item)
    {
        VerifyMutable();
        ArgumentNullException.ThrowIfNull(item);

        // Ownership is secured before any authored property is captured or
        // overwritten. A rejected insertion (duplicate, already attached,
        // disposed) must leave the caller's object exactly as it found it.
        _stack.Children.Add(item);
        var lease = _propertyOverrides.Acquire(
            item,
            RetainedPropertyOverrides.IsFocusable,
            RetainedPropertyOverrides.IsTabStop);
        ConfigureItem(item, lease);
    }

    /// <summary>Removes one sub-item from this group.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    internal bool RemoveItemCore(NavigationViewItem item) =>
        RemoveItemCore(item, restorePresentation: true);

    private bool RemoveItemCore(NavigationViewItem item, bool restorePresentation)
    {
        VerifyMutable();
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
        var lease = _propertyOverrides.Get(item);
        _ = _stack.Children.Remove(item);
        item.Invoked -= OnItemInvoked;

        if (repair is { } value)
        {
            owner!.CompleteRemoval(value);
        }

        if (restorePresentation)
        {
            _propertyOverrides.Restore(lease);
        }
        else
        {
            _propertyOverrides.Retire(lease);
        }

        return true;
    }

    /// <summary>Detaches one grouped item before direct disposal publication begins.</summary>
    /// <param name="item">The owned item whose caller requested disposal.</param>
    internal void RemoveItemForDisposal(NavigationViewItem item) =>
        _ = RemoveItemCore(item, restorePresentation: false);

    /// <summary>Clears all sub-items.</summary>
    internal void ClearItemsCore()
    {
        VerifyMutable();
        var owner = FindNavigationView();
        var repair = owner?.PrepareDescendantRemoval(this);
        var items = _stack.Children.OfType<NavigationViewItem>().ToArray();
        var leases = items.Select(_propertyOverrides.Get).ToArray();
        _stack.Children.Clear();

        foreach (var item in items)
        {
            item.Invoked -= OnItemInvoked;
        }

        if (repair is { } value)
        {
            owner!.CompleteRemoval(value);
        }

        foreach (var lease in leases)
        {
            _propertyOverrides.Restore(lease);
        }
    }

    private void ConfigureItem(NavigationViewItem item, RetainedPropertyOverrideLease lease)
    {
        lease.SetLive(RetainedControlProperty.IsFocusable, false);

        if (!IsCommitted(item, lease))
        {
            return;
        }

        lease.SetLive(RetainedControlProperty.IsTabStop, false);

        if (!IsCommitted(item, lease))
        {
            return;
        }

        item.Invoked += OnItemInvoked;
    }

    [Pure]
    private bool IsCommitted(NavigationViewItem item, RetainedPropertyOverrideLease lease) =>
        _stack.Children.Contains(item) && lease.IsCurrent;

    /// <summary>Retires descendant snapshots before owner-driven disposal skips direct child hooks.</summary>
    internal void RetirePresentationMetadataForOwnerDisposal() => _propertyOverrides.Dispose();

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var headerCells = (int) Math.Min(
            int.MaxValue,
            3L + Header.Measure(CellPolicy.AmbiguousWidth, UseMnemonic));
        var childConstraint = new Constraint(
            constraint.Width is { } width ? (int) Math.Max(0L, (long) width - ActualStyle.ItemIndent) : null,
            constraint.Height);
        var childrenDesired = MeasureChild(_stack, childConstraint);
        var childrenHeight = IsExpanded ? childrenDesired.Height : 0;
        var childrenWidth = (int) Math.Min(int.MaxValue, (long) childrenDesired.Width + ActualStyle.ItemIndent);
        return new Size(
            Math.Max(headerCells, childrenWidth),
            (int) Math.Min(int.MaxValue, 1L + childrenHeight));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var indent = Math.Min(ActualStyle.ItemIndent, bounds.Width);
        var slot = IsExpanded && bounds.Height > 1
            ? new Rect(bounds.X.Add(indent), bounds.Y.Add(1), bounds.Width - indent, bounds.Height - 1)
            : default;
        ArrangeChild(_stack, slot, ResolvedAxes.Both);
    }

    /// <inheritdoc/>
    internal override SelectableTextSnapshot CreateSelectableTextSnapshot()
    {
        var label = new Rect(
            ContentBounds.X.Add(3),
            ContentBounds.Y,
            Math.Max(0, ContentBounds.Width - 3),
            Math.Min(1, ContentBounds.Height));
        var header = SingleLineSelectableTextProjection.Create(
            this,
            Header,
            new Point(label.X, label.Y),
            label,
            UseMnemonic);
        var text = new StringBuilder(header.Text);
        var glyphs = new List<SelectableTextGlyph>(header.Glyphs);

        foreach (var item in _stack.Children.OfType<NavigationViewItem>())
        {
            if (!item.EffectiveIsVisible)
            {
                continue;
            }

            var snapshot = item.GetSelectableTextSnapshot();
            var offset = text.Length;
            _ = text.Append(snapshot.Text);

            foreach (var glyph in snapshot.Glyphs)
            {
                glyphs.Add(new SelectableTextGlyph(
                    new Selection(glyph.Range.Start.Add(offset), glyph.Range.End.Add(offset)),
                    new Rect(
                        item.Bounds.X.Add(glyph.Bounds.X) - Bounds.X,
                        item.Bounds.Y.Add(glyph.Bounds.Y) - Bounds.Y,
                        glyph.Bounds.Width,
                        glyph.Bounds.Height)));
            }
        }

        return new SelectableTextSnapshot(text.ToString(), glyphs, isAuthoritative: true);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (ContentBounds.Width == 0 || ContentBounds.Height == 0)
        {
            return;
        }

        var themed = IsExpanded
            ? ControlGlyphs.Navigation.GroupExpanded
            : ControlGlyphs.Navigation.GroupCollapsed;
        var glyph = (IsExpanded ? ActualStyle.ExpandedGlyph : ActualStyle.CollapsedGlyph).Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);
        var leading = canvas.Draw(
            $" {glyph} ".AsSpan(),
            new Point(ContentBounds.X, ContentBounds.Y),
            ResolvedStyle,
            background: BackgroundMode.Transparent);
        _ = Header.Draw(
            canvas,
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

        if (eventArgs.IsHandled)
        {
            return;
        }

        var keyboard = eventArgs is KeyEventArgs
        {
            IsInitialKeyDown: true,
            Stroke: { Code: Code.Enter, Modifiers: var enterModifiers }
        } && enterModifiers.IsActivationEligible();
        if (keyboard)
        {
            Activate(ActivationCause.Keyboard);
            eventArgs.IsHandled = true;
            return;
        }

        if (eventArgs is PointerEventArgs)
        {
            _press.Handle(eventArgs);
        }
    }

    private void Activate(ActivationCause cause)
    {
        _ = cause;
        FindNavigationView()?.NotifyGroupInvoked(this);
        IsExpanded = !IsExpanded;
    }

    /// <inheritdoc/>
    internal override void OnDirectDisposalRequested()
    {
        FindNavigationView()?.RemoveEntryForDisposal(this);
        _propertyOverrides.Dispose();
        base.OnDirectDisposalRequested();
    }

    [Pure]
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
