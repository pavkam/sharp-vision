// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Displays a retained semantic path with one current location and owner-routed interaction.</summary>
[PublicAPI]
public sealed class Breadcrumb: ItemsControl, IStyled<BreadcrumbStyle>
{
    private readonly BreadcrumbHost _host;
    private readonly RetainedPropertyOverrideService _propertyOverrides;
    private readonly StyleSlot<BreadcrumbStyle> _style;
    private BreadcrumbItem? _currentItem;
    private long _collectionGeneration;
    private long _currentGeneration;

    /// <summary>Initializes an empty, single-tab-stop breadcrumb path.</summary>
    public Breadcrumb()
    {
        EnableChromeAuthoring();
        _style = InitializeStyle(BreadcrumbStyle.Definition);
        _host = new BreadcrumbHost(this);
        InitializeItemsHost(_host);
        _propertyOverrides = new RetainedPropertyOverrideService(this, ItemControlsSlot);
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

            SetCurrent(value < 0 ? null : ItemAt(value));
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
            SetCurrent(index < 0 ? null : value);
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

        var previousLease = _propertyOverrides.Get(previous);
        ReplaceItemControl(index, item);
        Unsubscribe(previous);
        var lease = _propertyOverrides.Acquire(
            item,
            RetainedPropertyOverrides.IsFocusable,
            RetainedPropertyOverrides.IsTabStop);
        ConfigureItem(item, lease);
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

        var lease = _propertyOverrides.Get(item);
        RemoveItemControlAt(index);
        Unsubscribe(item);
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
        CollectionMutated(selectFinal: false);
    }

    /// <summary>Clears every item without disposing it.</summary>
    internal void ClearItems() => ClearItems(disposing: false);

    private void ClearItems(bool disposing)
    {
        VerifyMutable();
        var items = Items.ToArray();
        var leases = items.Select(_propertyOverrides.Get).ToArray();
        ClearItemControls();

        foreach (var item in items)
        {
            Unsubscribe(item);
        }

        _collectionGeneration++;
        SetCurrent(null);

        for (var index = 0; index < leases.Length; index++)
        {
            if (disposing)
            {
                _propertyOverrides.Retire(leases[index]);
            }
            else
            {
                _propertyOverrides.Restore(leases[index]);
            }
        }
    }

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

        SetCurrent(item);

        if (!IsAvailableOwned(item) || !ReferenceEquals(_currentItem, item))
        {
            return false;
        }

        var collectionGeneration = _collectionGeneration;
        var currentGeneration = _currentGeneration;
        item.InvokeAfterOwnerCommit(cause);

        if (collectionGeneration != _collectionGeneration ||
            currentGeneration != _currentGeneration ||
            !IsAvailableOwned(item) ||
            !ReferenceEquals(_currentItem, item))
        {
            return true;
        }

        InputBase.ExecuteCommandIfAny(command);
        return true;
    }

    /// <summary>Focuses this breadcrumb and activates a mnemonic-selected primary item.</summary>
    internal bool ActivateAccessKey(BreadcrumbItem item, Rune key)
    {
        _ = key;

        return IsAvailableOwned(item) &&
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
        _collectionGeneration++;

        if (selectFinal || _currentItem is null || !IsAvailableOwned(_currentItem))
        {
            SetCurrent(FinalAvailableItem());
            return;
        }

        NotifyPropertyChanged(nameof(CurrentIndex), InvalidationImpact.Measure);
    }

    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (sender is not BreadcrumbItem item ||
            eventArgs.PropertyName is not nameof(Visibility) and
            not nameof(IsEnabled) and
            not nameof(EffectiveIsVisible) and
            not nameof(EffectiveIsEnabled))
        {
            return;
        }

        _collectionGeneration++;

        if ((ReferenceEquals(_currentItem, item) && !IsAvailableOwned(item)) || _currentItem is null)
        {
            SetCurrent(FinalAvailableItem());
        }

        Invalidate(Invalidation.Measure);
    }

    private void SetCurrent(BreadcrumbItem? item)
    {
        VerifyMutable();

        if (item is not null && !IsAvailableOwned(item))
        {
            throw new InvalidOperationException("The current breadcrumb item must be visible and enabled.");
        }

        if (ReferenceEquals(_currentItem, item))
        {
            return;
        }

        var version = ++_currentGeneration;
        var previous = _currentItem;
        previous?.CommitSemanticCurrent(false);

        if (_currentGeneration != version)
        {
            return;
        }

        _currentItem = item;
        item?.CommitSemanticCurrent(true);

        if (_currentGeneration != version || !ReferenceEquals(_currentItem, item))
        {
            return;
        }

        NotifyPropertyChanged(nameof(CurrentIndex), InvalidationImpact.Measure);

        if (_currentGeneration != version || !ReferenceEquals(_currentItem, item))
        {
            return;
        }

        NotifyPropertyChanged(nameof(CurrentItem), InvalidationImpact.None);

        if (_currentGeneration != version || !ReferenceEquals(_currentItem, item))
        {
            return;
        }

        CurrentChanged?.Invoke(this, new BreadcrumbCurrentChangedEventArgs(previous, item));
    }

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

    private void OnKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
    }

    private void OnPointerRouted(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
    }

    /// <inheritdoc/>
    private protected override void OnItemsControlDisposing()
    {
        if (ItemControlCount > 0)
        {
            ClearItems(disposing: true);
        }

        _propertyOverrides.Dispose();
        CurrentChanged = null;
    }
}
