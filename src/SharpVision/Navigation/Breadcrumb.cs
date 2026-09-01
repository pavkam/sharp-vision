// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using SharpVision.Terminal.Input;

using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Displays a retained semantic path with one current location and owner-routed interaction.</summary>
[PublicAPI]
public sealed class Breadcrumb: ItemsControl, IStyled<BreadcrumbStyle>
{
    private readonly BreadcrumbHost _host;
    private readonly CurrentItemNavigator _navigator;
    private readonly BreadcrumbOverflowButton _overflowButton;
    private readonly RetainedPropertyOverrideService _propertyOverrides;
    private readonly StyleSlot<BreadcrumbStyle> _style;
    private BreadcrumbItem? _currentItem;
    private BreadcrumbItem? _pressedItem;
    private long _currentGeneration;
    private long _layoutGeneration;
    private long _pressedLayoutGeneration;
    private long _projectedCollectionGeneration = -1;
    private bool _pressedOverflow;

    #region Construction and public state

    /// <summary>Initializes an empty, single-tab-stop breadcrumb path.</summary>
    public Breadcrumb()
    {
        EnableChromeAuthoring();
        _style = InitializeStyle(BreadcrumbStyle.Definition);
        _host = new BreadcrumbHost(this);
        InitializeItemsHost(_host);
        _propertyOverrides = new RetainedPropertyOverrideService(this, ItemControlsSlot);
        _overflowButton = new BreadcrumbOverflowButton(this);
        var overflowSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: false,
                partKey: "overflow",
                InvalidationImpact.Measure),
            capacity: 1);
        overflowSlot.Add(_overflowButton);
        _navigator = new CurrentItemNavigator(CollectNavigableItems);
        Items = new BreadcrumbItemCollection(this);
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.None;
        _ = AddHandler(Events.Key, OnKeyRouted);
        _ = AddHandler(Events.Pointer, OnPointerRouted);
    }

    /// <summary>Raised after the represented location changes while the breadcrumb is live.</summary>
    public event EventHandler<BreadcrumbCurrentChangedEventArgs>? CurrentChanged;

    /// <summary>Gets the typed retained path.</summary>
    public BreadcrumbItemCollection Items { get; }

    /// <summary>Gets or sets the current path index, or -1 for deliberate no-current state.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside -1 and the current item range.</exception>
    /// <exception cref="InvalidOperationException">The attached breadcrumb is mutated off-dispatcher or the target is unavailable.</exception>
    /// <exception cref="ObjectDisposedException">The breadcrumb is disposed.</exception>
    [ValueRange(-1, int.MaxValue)]
    public int CurrentIndex
    {
        get => _currentItem is null ? -1 : IndexOfItem(_currentItem);
        set
        {
            if (value < -1 || value >= ItemControlCount)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The current index is outside the breadcrumb path.");
            }

            _ = SetCurrent(value < 0 ? null : ItemAt(value));
        }
    }

    /// <summary>Gets or sets the represented owned item. Null or a foreign item clears current.</summary>
    /// <exception cref="InvalidOperationException">The attached breadcrumb is mutated off-dispatcher or an owned target is unavailable.</exception>
    /// <exception cref="ObjectDisposedException">The breadcrumb is disposed.</exception>
    public BreadcrumbItem? CurrentItem
    {
        get => _currentItem;
        set
        {
            var index = value is null ? -1 : IndexOfItem(value);
            _ = SetCurrent(index < 0 ? null : value);
        }
    }

    /// <summary>Gets or sets the complete local breadcrumb presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached breadcrumb is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The breadcrumb is disposed.</exception>
    public BreadcrumbStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned breadcrumb presentation.</summary>
    public BreadcrumbStyle ActualStyle => _style.Actual;

    #endregion

    #region Retained ownership

    /// <summary>Gets the immutable layout shared by presentation and input for the current pass.</summary>
    internal BreadcrumbLayout Layout { get; private set; } = BreadcrumbLayout.Empty;

    /// <summary>Gets the current semantic ownership generation.</summary>
    internal long CollectionGeneration { get; private set; }

    /// <summary>Gets the current overflow projection generation.</summary>
    internal long OverflowGeneration { get; private set; }

    /// <summary>Gets the current realized item count.</summary>
    internal int ItemCount => ItemControlCount;

    /// <summary>Gets one realized item.</summary>
    internal BreadcrumbItem ItemAt(int index) => (BreadcrumbItem) GetItemControl(index);

    /// <summary>Gets an item's identity position.</summary>
    internal int IndexOfItem(BreadcrumbItem item) => IndexOfItemControl(item);

    /// <summary>Adds one item.</summary>
    internal void AddItem(BreadcrumbItem item) => InsertItem(ItemControlCount, item);

    /// <summary>Inserts one item and establishes the final available location.</summary>
    internal void InsertItem(int index, BreadcrumbItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        VerifyMutable();

        if ((uint) index > (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The insertion index is outside the breadcrumb path.");
        }

        InsertItemControl(index, item);
        var lease = _propertyOverrides.Acquire(
            item,
            RetainedPropertyOverrides.IsFocusable,
            RetainedPropertyOverrides.IsTabStop);
        ConfigureItem(item, lease);
        RepairActive(selectFinal: true);
        CollectionMutated(selectFinal: true);
    }

    /// <summary>Replaces one retained item.</summary>
    internal void ReplaceItem(int index, BreadcrumbItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        VerifyMutable();

        if ((uint) index >= (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The replacement index is outside the breadcrumb path.");
        }

        var previous = ItemAt(index);

        if (ReferenceEquals(previous, item))
        {
            return;
        }

        if (ReferenceEquals(_pressedItem, previous))
        {
            CancelPointerPress();
        }

        var previousLease = _propertyOverrides.Get(previous);
        ReplaceItemControl(index, item);
        Unsubscribe(previous);
        var lease = _propertyOverrides.Acquire(
            item,
            RetainedPropertyOverrides.IsFocusable,
            RetainedPropertyOverrides.IsTabStop);
        ConfigureItem(item, lease);
        RepairActive(selectFinal: false);
        CollectionMutated(selectFinal: false);
        _propertyOverrides.Restore(previousLease);
    }

    /// <summary>Removes an owned item.</summary>
    internal bool RemoveItem(BreadcrumbItem item) => RemoveItemCore(item, restorePresentation: true);

    /// <summary>Detaches an item before direct disposal begins.</summary>
    internal void RemoveItemForDisposal(BreadcrumbItem item) =>
        _ = RemoveItemCore(item, restorePresentation: false);

    private bool RemoveItemCore(BreadcrumbItem item, bool restorePresentation)
    {
        ArgumentNullException.ThrowIfNull(item);
        VerifyMutable();
        var index = IndexOfItem(item);

        if (index < 0)
        {
            return false;
        }

        if (ReferenceEquals(_pressedItem, item))
        {
            CancelPointerPress();
        }

        var lease = _propertyOverrides.Get(item);
        RemoveItemControlAt(index);
        Unsubscribe(item);
        RepairActive(selectFinal: false);
        CollectionMutated(selectFinal: false);

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

    /// <summary>Removes an item by position.</summary>
    internal void RemoveItemAt(int index)
    {
        VerifyMutable();

        if ((uint) index >= (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The removal index is outside the breadcrumb path.");
        }

        _ = RemoveItem(ItemAt(index));
    }

    /// <summary>Moves an item without changing its semantic identity.</summary>
    internal void MoveItem(int oldIndex, int newIndex)
    {
        VerifyMutable();

        if ((uint) oldIndex >= (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(oldIndex), oldIndex, "The source index is outside the breadcrumb path.");
        }

        if ((uint) newIndex >= (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(newIndex), newIndex, "The destination index is outside the breadcrumb path.");
        }

        if (oldIndex == newIndex)
        {
            return;
        }

        MoveItemControl(oldIndex, newIndex);
        RepairActive(selectFinal: false);
        CollectionMutated(selectFinal: false);
    }

    /// <summary>Clears every item without disposing it.</summary>
    internal void ClearItems() => ClearItems(disposing: false);

    private void ClearItems(bool disposing)
    {
        VerifyMutable();

        if (!disposing)
        {
            CancelPointerPress();
        }

        var items = Items.ToArray();
        var leases = items.Select(_propertyOverrides.Get).ToArray();

        if (disposing)
        {
            _currentItem?.CommitSemanticCurrent(false);
            _currentItem = null;
            _currentGeneration++;
            OwnedControlRegistry.CommitCompoundForOwnerDisposal(
                () =>
                {
                    foreach (var item in items)
                    {
                        Unsubscribe(item);
                    }
                },
                (ItemControlsSlot, Array.Empty<ControlBase>()));
            CollectionGeneration++;
            _ = _navigator.SetCurrent(null);

            foreach (var item in items)
            {
                item.DisposeAfterUnavailable();
            }

            return;
        }

        ClearItemControls();

        foreach (var item in items)
        {
            Unsubscribe(item);
        }

        CollectionGeneration++;
        _ = _navigator.SetCurrent(null);
        _ = SetCurrent(null);

        foreach (var lease in leases)
        {
            _propertyOverrides.Restore(lease);
        }
    }

    #endregion

    #region Activation and source changes

    /// <summary>Runs one canonical activation transaction for an owned item.</summary>
    internal bool TryActivateItem(
        BreadcrumbItem item,
        ActivationCause cause,
        (System.Windows.Input.ICommand? Command, object? Parameter) command)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfNotDefined(cause);

        if (!IsAvailableOwned(item))
        {
            return false;
        }

        if (!SetCurrent(item))
        {
            return false;
        }

        if (!IsAvailableOwned(item) || !ReferenceEquals(_currentItem, item))
        {
            return false;
        }

        var collectionGeneration = CollectionGeneration;
        var currentGeneration = _currentGeneration;
        _ = _navigator.SetCurrent(item);
        item.InvokeAfterOwnerCommit(cause);

        if (collectionGeneration != CollectionGeneration ||
            currentGeneration != _currentGeneration ||
            !IsAvailableOwned(item) ||
            !ReferenceEquals(_currentItem, item))
        {
            return true;
        }

        InputBase.ExecuteCommandIfAny(command);
        return true;
    }

    /// <summary>Activates a source through an exact overflow projection generation.</summary>
    internal bool TryActivateProjection(
        BreadcrumbItem item,
        ActivationCause cause,
        long collectionGeneration,
        long overflowGeneration) =>
        collectionGeneration == CollectionGeneration &&
        overflowGeneration == OverflowGeneration &&
        TryActivateItem(item, cause, item.CaptureCommand());

    /// <summary>Focuses this breadcrumb and activates a mnemonic-selected primary item.</summary>
    internal bool ActivateAccessKey(BreadcrumbItem item, Rune key)
    {
        _ = key;

        var entry = Layout.EntryFor(item);
        return entry.IsPrimary &&
               entry.Bounds.Width > 0 &&
               IsAvailableOwned(item) &&
               Focus() &&
               IsAvailableOwned(item) &&
               TryActivateItem(item, ActivationCause.Keyboard, item.CaptureCommand());
    }

    private void ConfigureItem(BreadcrumbItem item, RetainedPropertyOverrideLease lease)
    {
        lease.SetLive(RetainedControlProperty.IsFocusable, false);

        if (!IsCommitted(item, lease))
        {
            return;
        }

        lease.SetLive(RetainedControlProperty.IsTabStop, false);

        if (IsCommitted(item, lease))
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }
    }

    [Pure]
    private bool IsCommitted(BreadcrumbItem item, RetainedPropertyOverrideLease lease) =>
        IndexOfItem(item) >= 0 && lease.IsCurrent;

    private void Unsubscribe(BreadcrumbItem item) => item.PropertyChanged -= OnItemPropertyChanged;

    private void CollectionMutated(bool selectFinal)
    {
        CollectionGeneration++;
        RepairActive(selectFinal: false);

        if (selectFinal || _currentItem is null || !IsAvailableOwned(_currentItem))
        {
            _ = SetCurrent(FinalAvailableItem());
            return;
        }

        NotifyPropertyChanged(nameof(CurrentIndex), InvalidationImpact.Measure);
    }

    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (sender is not BreadcrumbItem item ||
            eventArgs.PropertyName is not nameof(Visibility) and
            not nameof(IsEnabled) and
            not nameof(UseMnemonic) and
            not nameof(BreadcrumbItem.Text) and
            not nameof(EffectiveIsVisible) and
            not nameof(EffectiveIsEnabled))
        {
            return;
        }

        CollectionGeneration++;

        if (ReferenceEquals(_pressedItem, item) && !IsAvailableOwned(item))
        {
            CancelPointerPress();
        }

        if ((ReferenceEquals(_currentItem, item) && !IsAvailableOwned(item)) || _currentItem is null)
        {
            _ = SetCurrent(FinalAvailableItem());
        }

        Invalidate(Invalidation.Measure);
    }

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var natural = base.MeasureOverride(constraint);
        _ = MeasureChild(_overflowButton, new Constraint(width: null, 1));
        var candidate = BreadcrumbLayout.Create(
            this,
            constraint.Width,
            _overflowButton.DesiredSize.Width,
            _layoutGeneration + 1);

        var windowChanged = !Layout.HasSameWindow(candidate);

        if (windowChanged)
        {
            CancelPointerPress();
            Layout = candidate;
            // The host's outer slot can stay unchanged while the projected item window moves.
            // Force its containing arrange pass to apply the new per-item bounds instead of
            // accepting the previous same-slot arrangement.
            _host.InvalidateSelf(Invalidation.Arrange);
            _layoutGeneration = candidate.Generation;
        }

        if (windowChanged || _projectedCollectionGeneration != CollectionGeneration)
        {
            OverflowGeneration++;
            _overflowButton.SetSources(
                this,
                Layout.OverflowItems,
                CollectionGeneration,
                OverflowGeneration);
            _projectedCollectionGeneration = CollectionGeneration;
        }

        if (windowChanged)
        {
            RepairActive(selectFinal: false);
        }

        return new Size(
            constraint.Width.HasValue ? Math.Min(natural.Width, constraint.Width.Value) : natural.Width,
            Math.Max(natural.Height, _overflowButton.DesiredSize.Height));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        base.ArrangeOverride(bounds);
        var relative = Layout.TriggerBounds;
        var trigger = relative.Width == 0 || relative.Height == 0
            ? default
            : new Rect(ContentBounds.X.Add(relative.X), ContentBounds.Y, relative.Width, Math.Min(1, bounds.Height));
        ArrangeChild(_overflowButton, trigger, ResolvedAxes.Both);
    }

    /// <inheritdoc/>
    internal override void RenderOverlay(TerminalCanvas canvas)
    {
        base.RenderOverlay(canvas);

        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var separator = ResolveControlGlyph(ActualStyle.SeparatorGlyph);
        var style = NormalStyle.WithForeground(ResolveColor(ActualStyle.SeparatorColor, Theme));

        if (Layout.TriggerBounds.Width > 0 && Layout.PrimaryItems.Count > 0)
        {
            var adjacent = Layout.TriggerPrecedesPrimary ? Layout.PrimaryItems[0] : Layout.PrimaryItems[^1];

            if (adjacent.EffectiveIsVisible)
            {
                var x = Layout.TriggerPrecedesPrimary
                    ? Layout.TriggerBounds.Right
                    : Layout.EntryFor(adjacent).Bounds.Right;
                canvas.DrawRune(
                    separator,
                    new Point(ContentBounds.X.Add(x), ContentBounds.Y),
                    style,
                    BackgroundMode.Transparent);
            }
        }

        for (var index = 0; index + 1 < Layout.PrimaryItems.Count; index++)
        {
            var item = Layout.PrimaryItems[index];
            var next = Layout.PrimaryItems[index + 1];

            if (!item.EffectiveIsVisible || !next.EffectiveIsVisible)
            {
                continue;
            }

            var entry = Layout.EntryFor(item);
            canvas.DrawRune(
                separator,
                new Point(ContentBounds.X.Add(entry.Bounds.Right), ContentBounds.Y),
                style,
                BackgroundMode.Transparent);
        }
    }

    /// <inheritdoc/>
    internal override ControlBase? HitTest(Point point)
    {
        var target = base.HitTest(point);
        return ReferenceEquals(target, _host) ? this : target;
    }

    #endregion

    #region Current and keyboard navigation

    private bool SetCurrent(BreadcrumbItem? item)
    {
        VerifyMutable();

        if (item is not null && !IsAvailableOwned(item))
        {
            throw new InvalidOperationException("The current breadcrumb item must be visible and enabled.");
        }

        if (ReferenceEquals(_currentItem, item))
        {
            return true;
        }

        var collectionGeneration = CollectionGeneration;
        var version = ++_currentGeneration;
        var previous = _currentItem;
        previous?.CommitSemanticCurrent(false);

        if (!IsCurrentTransitionAuthority(version, collectionGeneration))
        {
            return false;
        }

        _currentItem = item;
        item?.CommitSemanticCurrent(true);

        if (!IsCurrentTransition(version, collectionGeneration, item))
        {
            return false;
        }

        NotifyPropertyChanged(nameof(CurrentIndex), InvalidationImpact.Measure);

        if (!IsCurrentTransition(version, collectionGeneration, item))
        {
            return false;
        }

        NotifyPropertyChanged(nameof(CurrentItem), InvalidationImpact.None);

        if (!IsCurrentTransition(version, collectionGeneration, item))
        {
            return false;
        }

        CurrentChanged?.Invoke(this, new BreadcrumbCurrentChangedEventArgs(previous, item));
        return IsCurrentTransition(version, collectionGeneration, item);
    }

    [Pure]
    private bool IsCurrentTransition(long version, long collectionGeneration, BreadcrumbItem? item) =>
        IsCurrentTransitionAuthority(version, collectionGeneration) &&
        ReferenceEquals(_currentItem, item);

    [Pure]
    private bool IsCurrentTransitionAuthority(long version, long collectionGeneration) =>
        !IsDisposed &&
        !IsDisposing &&
        _currentGeneration == version &&
        CollectionGeneration == collectionGeneration;

    [Pure]
    private BreadcrumbItem? FinalAvailableItem()
    {
        for (var index = ItemControlCount - 1; index >= 0; index--)
        {
            var item = ItemAt(index);

            if (IsAvailableOwned(item))
            {
                return item;
            }
        }

        return null;
    }

    [Pure]
    private bool IsAvailableOwned(BreadcrumbItem item) =>
        !item.IsDisposed &&
        !item.IsDisposing &&
        IndexOfItem(item) >= 0 &&
        item.Visibility == Visibility.Visible &&
        item.EffectiveIsVisible &&
        item.EffectiveIsEnabled;

    /// <summary>Gets whether an owned item is currently eligible for semantic presentation.</summary>
    internal bool IsAvailableItem(BreadcrumbItem item) => IsAvailableOwned(item);

    private List<ControlBase> CollectNavigableItems()
    {
        List<ControlBase> result = [];

        foreach (var item in Layout.PrimaryItems)
        {
            if (IsAvailableOwned(item) && Layout.EntryFor(item).Bounds.Width > 0)
            {
                result.Add(item);
            }
        }

        if (result.Count > 0 || Layout.Generation > 0)
        {
            return result;
        }

        for (var index = 0; index < ItemControlCount; index++)
        {
            var item = ItemAt(index);

            if (IsAvailableOwned(item))
            {
                result.Add(item);
            }
        }

        return result;
    }

    private void RepairActive(bool selectFinal)
    {
        if (selectFinal || _navigator.Current is not BreadcrumbItem current || !IsAvailableOwned(current))
        {
            _ = _navigator.SetCurrent(FinalAvailableItem());
        }
    }

    #endregion

    #region Routed input and cleanup

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Bubble || !eventArgs.IsInitialKeyDown)
        {
            return;
        }

        if (eventArgs.Stroke.Code == Code.Enter ||
            (eventArgs.Stroke.Code == Code.Character && eventArgs.Stroke.Character == new Rune(' ')))
        {
            if (!eventArgs.Stroke.Modifiers.IsActivationEligible())
            {
                return;
            }

            eventArgs.IsHandled = _navigator.Current is BreadcrumbItem item &&
                                  TryActivateItem(item, ActivationCause.Keyboard, item.CaptureCommand());
            return;
        }

        if (!KeyboardModifierPolicy.IsScalarNavigationEligible(eventArgs.Stroke.Modifiers))
        {
            return;
        }

        if (eventArgs.Stroke.Code is Code.Home or Code.End)
        {
            var items = CollectNavigableItems();

            if (items.Count > 0)
            {
                _ = _navigator.SetCurrent(eventArgs.Stroke.Code == Code.Home ? items[0] : items[^1]);
                eventArgs.IsHandled = true;
            }

            return;
        }

        var direction = eventArgs.Stroke.Code is Code.Left or Code.Up
            ? -1
            : eventArgs.Stroke.Code is Code.Right or Code.Down
                ? 1
                : 0;

        if (direction != 0)
        {
            _ = _navigator.Move(direction, wrap: false);
            eventArgs.IsHandled = true;
        }
    }

    private void OnPointerRouted(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Preview)
        {
            return;
        }

        var pointer = eventArgs.Pointer;

        if (pointer.Action == PointerAction.Leave)
        {
            if (_pressedItem is not null || _pressedOverflow)
            {
                eventArgs.IsHandled = true;
                CancelPointerPress();
            }

            return;
        }

        if (pointer.Cells is not { } cells)
        {
            return;
        }

        if (pointer.Action == PointerAction.Press && (pointer.Buttons & Buttons.Primary) != 0)
        {
            var item = HitPrimaryItem(cells);
            var overflow = _overflowButton.Bounds.Contains(cells) && _overflowButton.HasItems;
            var layoutGeneration = Layout.Generation;

            if ((item is null && !overflow) || !Focus())
            {
                return;
            }

            item = item is not null &&
                   layoutGeneration == Layout.Generation &&
                   IsAvailableOwned(item) &&
                   item.Bounds.Contains(cells)
                ? item
                : null;
            overflow = overflow &&
                       layoutGeneration == Layout.Generation &&
                       _overflowButton.Bounds.Contains(cells) &&
                       _overflowButton.HasItems;

            if ((item is null && !overflow) || !CapturePointer())
            {
                return;
            }

            _pressedItem = item;
            _pressedOverflow = overflow;
            _pressedLayoutGeneration = layoutGeneration;
            item?.SetPressed(true);
            _overflowButton.SetPressed(overflow);
            eventArgs.IsHandled = true;
            return;
        }

        if (_pressedItem is null && !_pressedOverflow)
        {
            return;
        }

        eventArgs.IsHandled = true;
        var sameGeneration = _pressedLayoutGeneration == Layout.Generation;
        var itemInside = sameGeneration && _pressedItem is { } pressed && pressed.Bounds.Contains(cells);
        var overflowInside = sameGeneration && _pressedOverflow && _overflowButton.Bounds.Contains(cells);
        _pressedItem?.SetPressed(itemInside);
        _overflowButton.SetPressed(overflowInside);

        if (!PointerButtonTransition.IsPrimaryRelease(pointer))
        {
            return;
        }

        var activationItem = itemInside ? _pressedItem : null;
        var activateOverflow = overflowInside;
        CancelPointerPress();

        if (activationItem is not null)
        {
            _ = TryActivateItem(
                activationItem,
                ActivationCause.Pointer,
                activationItem.CaptureCommand());
        }
        else if (activateOverflow)
        {
            _overflowButton.Open();
        }
    }

    [Pure]
    private BreadcrumbItem? HitPrimaryItem(Point cells)
    {
        foreach (var item in Layout.PrimaryItems)
        {
            if (IsAvailableOwned(item) && item.Bounds.Contains(cells))
            {
                return item;
            }
        }

        return null;
    }

    private void CancelPointerPress()
    {
        var pressedItem = _pressedItem;
        _pressedItem = null;
        _pressedOverflow = false;

        if (pressedItem is { IsDisposed: false, IsDisposing: false, TerminalDisposalStarted: false })
        {
            pressedItem.SetPressed(false);
        }

        if (!_overflowButton.IsDisposed &&
            !_overflowButton.IsDisposing &&
            !_overflowButton.TerminalDisposalStarted)
        {
            _overflowButton.SetPressed(false);
        }

        if (HasPointerCapture && !IsDisposed && !IsDisposing && !TerminalDisposalStarted)
        {
            ReleasePointerCapture();
        }
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);

        if (!focused)
        {
            CancelPointerPress();
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        CancelPointerPress();
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        CancelPointerPress();
    }

    /// <inheritdoc/>
    private protected override void OnItemsControlDisposing()
    {
        CancelPointerPress();

        if (ItemControlCount > 0)
        {
            ClearItems(disposing: true);
        }

        _ = _navigator.SetCurrent(null);
        _propertyOverrides.Dispose();
        CurrentChanged = null;
    }

    #endregion
}
