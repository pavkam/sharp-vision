// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using SharpVision.Controls;
using SharpVision.Terminal.Input;

using LayoutStack = Controls.Layout.Stack;

/// <summary>Defines a collapsible labeled group of navigation items.</summary>
[PublicAPI]
public sealed class NavigationViewGroup: ControlBase, IStyled<NavigationViewGroupStyle>
{
    private readonly LayoutStack _stack;
    private readonly OwnedControlSlot _childrenSlot;
    private readonly Dictionary<NavigationViewItem, NavigationItemPresentation> _requestedPresentations = [];
    private readonly PressBehavior _press;
    private readonly StyleSlot<NavigationViewGroupStyle> _style;
    private bool _isWritingItemFocusPolicy;
    private long _presentationVersion;

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
        LostPointerCapture += OnGroupLostPointerCapture;
        PropertyChanged += OnGroupAvailabilityChanged;
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
    internal int RequestedPresentationCount => _requestedPresentations.Count;

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
        var presentation = new NavigationItemPresentation(
            item.IsFocusable,
            item.IsTabStop,
            ++_presentationVersion);
        _requestedPresentations.Add(item, presentation);
        ConfigureItem(item, presentation.Version);
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
        var presentation = TakePresentation(item);

        _ = _stack.Children.Remove(item);
        item.PropertyChanged -= OnItemFocusPolicyChanged;
        item.Invoked -= OnItemInvoked;

        if (repair is { } value)
        {
            owner!.CompleteRemoval(value);
        }

        if (restorePresentation && presentation is { } requested)
        {
            RestorePresentation(item, requested);
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
        var repair = owner?.PrepareRemoval(this);
        var items = _stack.Children.OfType<NavigationViewItem>().ToArray();
        var presentations = new List<(NavigationViewItem Item, NavigationItemPresentation Presentation)>();

        foreach (var item in items)
        {
            if (TakePresentation(item) is { } presentation)
            {
                presentations.Add((item, presentation));
            }
        }

        _stack.Children.Clear();

        foreach (var item in items)
        {
            item.PropertyChanged -= OnItemFocusPolicyChanged;
            item.Invoked -= OnItemInvoked;
        }

        if (repair is { } value)
        {
            owner!.CompleteRemoval(value);
        }

        foreach (var (item, presentation) in presentations)
        {
            RestorePresentation(item, presentation);
        }
    }

    private NavigationItemPresentation? TakePresentation(NavigationViewItem item)
        => _requestedPresentations.Remove(item, out var presentation) ? presentation : null;

    private void ConfigureItem(NavigationViewItem item, long presentationVersion)
    {
        item.IsFocusable = false;

        if (!IsCommitted(item, presentationVersion))
        {
            return;
        }

        item.IsTabStop = false;

        if (!IsCommitted(item, presentationVersion))
        {
            return;
        }

        item.PropertyChanged += OnItemFocusPolicyChanged;
        item.Invoked += OnItemInvoked;
    }

    private void OnItemFocusPolicyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (_isWritingItemFocusPolicy ||
            sender is not NavigationViewItem item ||
            !_requestedPresentations.TryGetValue(item, out var presentation))
        {
            return;
        }

        if (eventArgs.PropertyName == nameof(IsFocusable))
        {
            _requestedPresentations[item] = presentation.WithFocusable(item.IsFocusable);

            if (item.IsFocusable)
            {
                WriteItemFocusPolicy(item, isFocusable: true);
            }
        }
        else if (eventArgs.PropertyName == nameof(IsTabStop))
        {
            _requestedPresentations[item] = presentation.WithTabStop(item.IsTabStop);

            if (item.IsTabStop)
            {
                WriteItemFocusPolicy(item, isFocusable: false);
            }
        }
    }

    private void WriteItemFocusPolicy(NavigationViewItem item, bool isFocusable)
    {
        if (item.IsDisposed || item.IsDisposing)
        {
            return;
        }

        _isWritingItemFocusPolicy = true;

        try
        {
            if (isFocusable)
            {
                item.IsFocusable = false;
            }
            else
            {
                item.IsTabStop = false;
            }
        }
        finally
        {
            _isWritingItemFocusPolicy = false;
        }
    }

    [Pure]
    private bool IsCommitted(NavigationViewItem item, long presentationVersion) =>
        _stack.Children.Contains(item) &&
        _requestedPresentations.TryGetValue(item, out var presentation) &&
        presentation.Version == presentationVersion;

    private static void RestorePresentation(NavigationViewItem item, NavigationItemPresentation presentation)
    {
        if (item.Parent is not null || item.IsDisposed || item.IsDisposing)
        {
            return;
        }

        item.IsFocusable = presentation.IsFocusable;

        if (!item.IsDisposed && !item.IsDisposing)
        {
            item.IsTabStop = presentation.IsTabStop;
        }
    }

    /// <summary>Retires descendant snapshots before owner-driven disposal skips direct child hooks.</summary>
    internal void RetirePresentationMetadataForOwnerDisposal() => _requestedPresentations.Clear();

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
            ? new Rect(bounds.X + indent, bounds.Y + 1, bounds.Width - indent, bounds.Height - 1)
            : default;
        ArrangeChild(_stack, slot, ResolvedAxes.Both);
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

    private void OnGroupLostPointerCapture(object? sender, PointerCaptureLostEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        _press.CaptureLost();
    }

    private void OnGroupAvailabilityChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName is nameof(EffectiveIsVisible) or nameof(EffectiveIsEnabled))
        {
            _press.Unavailable();
        }
    }

    /// <inheritdoc/>
    internal override void OnDirectDisposalRequested()
    {
        _press.Unavailable();
        FindNavigationView()?.RemoveEntryForDisposal(this);
        _requestedPresentations.Clear();
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
