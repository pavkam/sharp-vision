// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

using System.Runtime.ExceptionServices;

using SharpVision.Terminal.Input;

using LayoutStack = Controls.Layout.Stack;

/// <summary>Arranges typed menu items and coordinates their keyboard selection and radio groups.</summary>
[PublicAPI]
public sealed class Menu: ItemsControl
{
    private int _selectedIndex = -1;

    /// <summary>The entry <see cref="_selectedIndex"/> pointed at as of the last committed
    /// selection change. A disposed entry's slot can be reclaimed by a different sibling without
    /// <see cref="_selectedIndex"/> itself moving, so index equality alone cannot detect that
    /// <see cref="SelectedItem"/>'s identity silently changed - this retained reference can.</summary>
    private ControlBase? _selectedEntry;

    private readonly RetainedPropertyOverrideService _propertyOverrides;
    private readonly ModalSession _modalSession;
    private readonly LayoutStack _stack;
    private bool _closeChainAfterInvocation;
    private bool _closeChainPending;
    private bool _discardPendingSubmenuTransitionOnClose;
    private bool _isClosingChain;
    private int _itemInvocationDepth;
    private ModalityManager? _pendingSubmenuModalityOwner;
    private Menu? _pendingSubmenuMenu;
    private MenuItem? _pendingSubmenuOpen;
    private bool _pendingSubmenuOpenFromPointerSelection;
    private ModalScope? _pendingSubmenuSession;
    private MenuItem? _spacePressedItem;
    private bool _submenuChainLostDuringClose;
    private int _submenuSurfaceCloseDepth;
    private int _submenuTransitionDepth;

    /// <summary>Initializes an empty horizontal menu with typed managed items and a 15-cell minimum width.</summary>
    public Menu()
    {
        MinWidth = Length.Cells(15);
        _stack = new LayoutStack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0
        };
        InitializeItemsHost(_stack);
        _propertyOverrides = new RetainedPropertyOverrideService(this, ItemControlsSlot);
        _modalSession = new ModalSession(OnModalDismissRequested, OnModalScopeExited);
        Items = new MenuEntryCollection(this);
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.None;
        FocusEntered += OnFocusEntered;
        FocusLeft += OnFocusLeft;
    }

    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        BarAppearance.Rebase((theme ?? ThemeCatalog.Dark).GetStyleSet(ControlStyle.Default));

    /// <inheritdoc/>
    internal override bool ProvidesContinuousBackground => true;

    /// <summary>Raised after an owned item invokes through keyboard, pointer, or programmatic input.</summary>
    public event EventHandler<MenuItemInvokedEventArgs>? ItemInvoked;

    /// <summary>Raised internally after one committed item invocation and its menu-chain cleanup complete.</summary>
    internal event EventHandler<MenuItemInvokedEventArgs>? ItemInvocationCompleted;

    /// <summary>Gets the typed managed menu items.</summary>
    public MenuEntryCollection Items { get; }

    /// <summary>Gets or sets whether an owning composite surface supplies this menu's modal session.</summary>
    internal bool UsesExternalModalSession { get; set; }

    /// <summary>Gets or sets horizontal or vertical menu layout.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu is disposed.</exception>
    public Orientation Orientation
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The menu orientation is unknown.");

            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                InvalidationImpact.Measure,
                () =>
                {
                    _stack.Orientation = Orientation;
                    UpdateItemSizing();
                });
        }
    } = Orientation.Horizontal;

    /// <summary>Gets or sets non-negative cells between participating items.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu is disposed.</exception>
    public int Spacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetPropertyAndSynchronize(
                ref field,
                value,
                InvalidationImpact.Measure,
                () => _stack.Spacing = Spacing);
        }
    }

    /// <summary>Gets or selects the active non-separator item index, or -1 for no selection.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current item range.</exception>
    /// <exception cref="ArgumentException">The target is a separator.</exception>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu is disposed.</exception>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < -1 || (value >= 0 && value >= ItemControlCount))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The selected index is outside the menu.");
            }

            if (value >= 0 && ItemAt(value) is MenuSeparator)
            {
                throw new ArgumentException("A separator cannot become selected.", nameof(value));
            }

            Select(value, focus: false);
        }
    }

    /// <summary>Gets the selected item, or sets one owned non-separator item as selected; null clears selection.</summary>
    /// <remarks>Setting an item not owned by this menu clears selection, matching <see cref="SelectedIndex"/>'s -1.</remarks>
    /// <exception cref="InvalidOperationException">The attached menu is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The menu is disposed.</exception>
    public MenuItem? SelectedItem
    {
        // A hard cast assumed the selected slot always holds a MenuItem. That invariant does not
        // hold by construction: a disposed sibling's slot can be reclaimed by a MenuSeparator
        // through OnItemControlsChanged, and RemoveEntry's own pre-repair shift can leave a
        // MenuSeparator in the selected slot before Select/CommitSelectionPresentation ever read
        // it - which used to leave this getter, Select, and CommitSelectionPresentation each one
        // read away from InvalidCastException instead of reporting the absence of a selectable
        // item.
        get => _selectedIndex < 0 ? null : ItemAt(_selectedIndex) as MenuItem;
        set => SelectedIndex = value is null ? -1 : IndexOfItem(value);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Beside the pre-existing shared shortcut-column negotiation, this also negotiates one shared
    /// start-affix column across every owned row: the widest local <see cref="InputBase.StartAffix"/>
    /// reservation among owned items, pushed down to every row through
    /// <see cref="MenuItem.SetSharedStartAffixColumn"/> so a row without its own start affix still
    /// leaves its caption aligned with a sibling that has one. A horizontal Menu resets every row's
    /// shared column back to zero, since only a vertical Menu's stacked captions need one shared
    /// leading edge.
    /// </remarks>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var desired = base.MeasureOverride(constraint);

        if (Orientation != Orientation.Vertical)
        {
            ClearSharedStartAffixColumn();
            return desired;
        }

        var labelWidth = 0;
        var shortcutWidth = 0;
        var sharedStartAffixCells = 0;

        for (var index = 0; index < ItemControlCount; index++)
        {
            if (ItemAt(index) is not MenuItem item || item.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            labelWidth = Math.Max(labelWidth, item.DesiredLabelWidth.Add(item.Margin.Horizontal));
            shortcutWidth = Math.Max(shortcutWidth, item.ShortcutColumnWidth);
            sharedStartAffixCells = Math.Max(sharedStartAffixCells, item.StartAffixCells);
        }

        for (var index = 0; index < ItemControlCount; index++)
        {
            if (ItemAt(index) is MenuItem item)
            {
                item.SetSharedStartAffixColumn(sharedStartAffixCells);
            }
        }

        if (shortcutWidth == 0 && sharedStartAffixCells == 0)
        {
            return desired;
        }

        // labelWidth already folds in each row's own local start-affix reservation (through
        // MenuItem.DesiredLabelWidth), but a row with no affix of its own did not reserve the
        // shared column another, wider-affixed sibling now forces it to leave blank - so the
        // negotiated maximum is added again here, on top of labelWidth, to keep that row's caption
        // from being clipped. A row that already owns both the widest label and the widest affix
        // ends up with a little extra unused width rather than any row losing text.
        var columnWidth = labelWidth.Add(sharedStartAffixCells);

        if (shortcutWidth > 0)
        {
            columnWidth = columnWidth.Add(MenuItem.ShortcutGap).Add(shortcutWidth);
        }

        return new Size(Math.Max(desired.Width, columnWidth), desired.Height);
    }

    private void ClearSharedStartAffixColumn()
    {
        for (var index = 0; index < ItemControlCount; index++)
        {
            if (ItemAt(index) is MenuItem item)
            {
                item.SetSharedStartAffixColumn(0);
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (eventArgs is PointerEventArgs pointer)
        {
            SelectPointerTarget(pointer);
        }

        if (eventArgs.IsHandled || eventArgs is not KeyEventArgs key)
        {
            return;
        }

        if (!key.IsKeyDown)
        {
            HandleSpace(key);
            return;
        }

        var sessionOwner = FindSessionOwner();
        if (key.IsInitialKeyDown &&
            key.Stroke.Code == Code.Escape &&
            key.Stroke.Modifiers.IsActivationEligible() &&
            ReferenceEquals(sessionOwner, this) &&
            sessionOwner.IsSessionArmed)
        {
            CloseChain();
            eventArgs.IsHandled = true;
            return;
        }

        var previous = Orientation == Orientation.Horizontal ? Code.Left : Code.Up;
        var next = Orientation == Orientation.Horizontal ? Code.Right : Code.Down;
        var scalarNavigationEligible = KeyboardModifierPolicy.IsScalarNavigationEligible(key.Stroke.Modifiers);
        var target = scalarNavigationEligible && key.Stroke.Code == previous
            ? SingleSelectionIndex.FindWrapped(_selectedIndex, -1, ItemControlCount, Available)
            : scalarNavigationEligible && key.Stroke.Code == next
                ? SingleSelectionIndex.FindWrapped(_selectedIndex, 1, ItemControlCount, Available)
                : key.Stroke.Code == Code.Tab && KeyboardModifierPolicy.IsTabTraversalEligible(key.Stroke.Modifiers)
                    ? SingleSelectionIndex.FindWrapped(_selectedIndex, (key.Stroke.Modifiers & Modifiers.Shift) == 0 ? 1 : -1, ItemControlCount, Available)
                    // Unlike the wrapping Left/Right/Up/Down/Tab cases above, Home and End are
                    // explicit boundary requests: FindLinear stops at either end of the collection
                    // instead of cycling back around it.
                    : scalarNavigationEligible && key.Stroke.Code == Code.Home
                        ? SingleSelectionIndex.FindLinear(0, 1, ItemControlCount, Available)
                        : scalarNavigationEligible && key.Stroke.Code == Code.End
                            ? SingleSelectionIndex.FindLinear(ItemControlCount - 1, -1, ItemControlCount, Available)
                            : -1;

        if (target >= 0)
        {
            SelectFromInput(
                target,
                focus: true,
                switchSubmenu: true,
                openedFromPointerSelection: false);
            eventArgs.IsHandled = true;
            return;
        }

        if (key.IsInitialKeyDown &&
            key.Stroke.Code == Code.Enter &&
            key.Stroke.Modifiers.IsActivationEligible() &&
            ActivateSelected())
        {
            eventArgs.IsHandled = true;
            return;
        }

        HandleSpace(key);
    }

    /// <summary>Selects one radio item and clears matching siblings.</summary>
    /// <param name="item">The non-null owned radio item.</param>
    internal void SelectRadio(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (IndexOfItemControl(item) < 0 || item.Kind != MenuItemKind.Radio)
        {
            throw new ArgumentException("The radio item must belong to this menu.", nameof(item));
        }

        var candidates = Items
            .OfType<MenuItem>()
            .Where(candidate => candidate.Kind == MenuItemKind.Radio &&
                                string.Equals(candidate.GroupName, item.GroupName, StringComparison.Ordinal))
            .ToArray();
        var groupName = item.GroupName;
        var versions = new int[candidates.Length];

        for (var index = 0; index < candidates.Length; index++)
        {
            versions[index] = candidates[index].StageChecked(ReferenceEquals(candidates[index], item));
        }

        ExceptionDispatchInfo? failure = null;

        for (var index = 0; index < candidates.Length; index++)
        {
            var expected = ReferenceEquals(candidates[index], item);

            if (IndexOfItemControl(candidates[index]) >= 0 &&
                candidates[index].Kind == MenuItemKind.Radio &&
                string.Equals(candidates[index].GroupName, groupName, StringComparison.Ordinal) &&
                candidates[index].IsCheckedCommitCurrent(versions[index], expected))
            {
                CaptureFailure(candidates[index].PublishChecked, ref failure);
            }
        }

        failure?.Throw();
    }

    /// <summary>Selects and invokes one mnemonic-matched item through the ordinary keyboard path.</summary>
    /// <param name="item">The available item owned by this menu.</param>
    /// <returns>True when the item belongs to this menu and was invoked.</returns>
    internal bool InvokeAccessKey(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var index = IndexOfItemControl(item);

        if (index < 0 || !item.EffectiveIsEnabled || !item.EffectiveIsVisible)
        {
            return false;
        }

        SelectFromInput(
            index,
            focus: true,
            switchSubmenu: false,
            openedFromPointerSelection: false);

        // A callback run by the publish above may have disabled, hidden, or reselected away from
        // the target. Falling through unhandled (rather than swallowing the keystroke while
        // skipping activation) mirrors AccessKeyManager's own contract: an access key that cannot
        // reach a real target is simply not this control's to handle.
        if (item is not { EffectiveIsEnabled: true, EffectiveIsVisible: true } ||
            _selectedIndex < 0 || !ReferenceEquals(ItemAt(_selectedIndex), item))
        {
            return false;
        }

        item.ActivateFromMenu(ActivationCause.Keyboard);
        return true;
    }

    /// <summary>Gets one checked typed child by index.</summary>
    /// <param name="index">The valid zero-based child index.</param>
    /// <returns>The exact owned item.</returns>
    internal ControlBase ItemAt(int index) => RequireEntry(GetItemControl(index));

    /// <summary>Gets the current semantic item count.</summary>
    internal int ItemCount => ItemControlCount;

    /// <summary>Gets the position of one owned entry, or -1 when it is not owned by this menu.</summary>
    /// <param name="item">The non-null candidate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    internal int IndexOfEntry(ControlBase item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return IndexOfItemControl(item);
    }

    /// <summary>Adds one typed item and tracks its invocation.</summary>
    /// <param name="item">The non-null detached item.</param>
    internal void Add(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        InsertEntry(ItemControlCount, item);
    }

    /// <summary>Adds one typed separator.</summary>
    /// <param name="separator">The non-null detached separator.</param>
    /// <exception cref="ArgumentNullException"><paramref name="separator"/> is null.</exception>
    internal void Add(MenuSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        InsertEntry(ItemControlCount, separator);
    }

    /// <summary>Inserts one typed item at a position and tracks its invocation.</summary>
    /// <param name="index">The insertion position from zero through the current item count.</param>
    /// <param name="item">The non-null detached item.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    internal void Insert(int index, MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        InsertEntry(index, item);
    }

    /// <summary>Inserts one typed separator at a position.</summary>
    /// <param name="index">The insertion position from zero through the current item count.</param>
    /// <param name="separator">The non-null detached separator.</param>
    /// <exception cref="ArgumentNullException"><paramref name="separator"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    internal void Insert(int index, MenuSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        InsertEntry(index, separator);
    }

    private void InsertEntry(int index, ControlBase item)
    {
        Debug.Assert(item is MenuItem or MenuSeparator, "Menu entries are constrained by typed collection overloads.");

        VerifyMutable();

        if ((uint) index > (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The insertion index is outside the menu.");
        }

        // Ownership is secured before any authored property is captured or
        // overwritten. InsertItemControl can throw for a duplicate, already
        // attached, or disposed candidate; a rejected insertion must leave the
        // caller's object exactly as it found it.
        InsertItemControl(index, item);
        if (item is MenuItem)
        {
            var lease = _propertyOverrides.Acquire(
                item,
                RetainedPropertyOverrides.IsFocusable,
                RetainedPropertyOverrides.IsTabStop,
                RetainedPropertyOverrides.Height);
            lease.SetLive(RetainedControlProperty.IsFocusable, false);
            lease.SetLive(RetainedControlProperty.IsTabStop, false);
        }
        else
        {
            _ = _propertyOverrides.Acquire(item, RetainedPropertyOverrides.Height);
        }

        ApplyItemSizing(item);

        // An already-selected entry never changes identity because of an
        // insertion; only its numeric position shifts.
        if (_selectedIndex >= index)
        {
            _selectedIndex++;
            NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Render);
            NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);
        }

        if (_selectedIndex < 0 && item is MenuItem)
        {
            Select(index, focus: false);
        }

        if (item is MenuItem { Kind: MenuItemKind.Radio, IsChecked: true } radio)
        {
            SelectRadio(radio);
        }
    }

    /// <summary>Removes one typed item and its subscription.</summary>
    /// <param name="item">The non-null item.</param>
    /// <returns>True when ownership was removed.</returns>
    internal bool Remove(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return RemoveEntry(item);
    }

    /// <summary>Removes one typed separator.</summary>
    /// <param name="separator">The non-null separator.</param>
    /// <returns>True when ownership was removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="separator"/> is null.</exception>
    internal bool Remove(MenuSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        return RemoveEntry(separator);
    }

    /// <summary>Removes the owned entry at a position and its subscription.</summary>
    /// <param name="index">The valid zero-based entry position.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current entries.</exception>
    internal void RemoveAt(int index)
    {
        VerifyMutable();

        if ((uint) index >= (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The removal index is outside the menu.");
        }

        _ = RemoveEntry(ItemAt(index));
    }

    private bool RemoveEntry(ControlBase item)
    {
        VerifyMutable();

        var index = IndexOfItemControl(item);

        if (index < 0)
        {
            return false;
        }

        var propertyLease = _propertyOverrides.Get(item);
        _ = RemoveItemControl(item);
        _propertyOverrides.Restore(propertyLease);

        // Mirrors InsertItem's symmetric case: a removal that does not touch the selected
        // entry must never change its identity. Only an actual removal of the selected entry
        // itself needs SingleSelectionIndex.FindNearest repair; an entry before it shifts the
        // index silently, and an entry after it - including a MenuSeparator, which can never be
        // selected - leaves the selection untouched.
        if (index < _selectedIndex)
        {
            _selectedIndex--;
            NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Render);
            NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);
        }
        else if (index == _selectedIndex)
        {
            Select(SingleSelectionIndex.FindNearest(Math.Min(index, ItemControlCount - 1), ItemControlCount, Available), focus: false);

            // Select's "_selectedIndex == index" guard is a no-op precisely when FindNearest's
            // inclusive forward scan lands an available MenuItem successor in the very slot the
            // removed entry vacated - the collision this repair exists for. Detect the identity
            // change Select's early return skipped the same way OnItemControlsChanged's disposal
            // branch does: compare the slot's current occupant against the retained
            // _selectedEntry rather than trusting index equality, then finish the work Select
            // would otherwise have done.
            if (_selectedIndex >= 0 && _selectedIndex < ItemControlCount)
            {
                var current = ItemAt(_selectedIndex);

                if (!ReferenceEquals(current, _selectedEntry))
                {
                    if (item is MenuItem outgoing)
                    {
                        outgoing.CommitSelection(false);
                    }

                    _selectedEntry = current;
                    NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);

                    // NotifyPropertyChanged raises SelectedItem synchronously, so a reentrant
                    // subscriber may have mutated this menu again from inside it - e.g. removing
                    // `current` itself, which would already have re-run this same repair against
                    // whatever slid into its place. Committing `current` below in that case would
                    // wrongly re-select an entry a nested call has already moved on from.
                    if (!ReferenceEquals(_selectedEntry, current))
                    {
                        return true;
                    }
                }

                if (current is MenuItem incoming)
                {
                    incoming.CommitSelection(ContainsFocus);
                }
            }
        }

        return true;
    }

    /// <summary>Replaces the owned entry at a position, preserving position and tracking the new entry.</summary>
    /// <param name="index">The valid zero-based entry position.</param>
    /// <param name="item">The non-null detached replacement item or separator.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current entries.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="item"/> is not a <see cref="MenuItem"/> or <see cref="MenuSeparator"/>.</exception>
    internal void ReplaceEntry(int index, ControlBase item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _ = RequireEntry(item);
        VerifyMutable();

        if ((uint) index >= (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The replacement index is outside the menu.");
        }

        var old = ItemAt(index);

        if (ReferenceEquals(old, item))
        {
            return;
        }

        var wasSelected = index == _selectedIndex;
        var oldPropertyLease = _propertyOverrides.Get(old);

        ReplaceItemControl(index, item);

        _propertyOverrides.Restore(oldPropertyLease);

        if (item is MenuItem)
        {
            var lease = _propertyOverrides.Acquire(
                item,
                RetainedPropertyOverrides.IsFocusable,
                RetainedPropertyOverrides.IsTabStop,
                RetainedPropertyOverrides.Height);
            lease.SetLive(RetainedControlProperty.IsFocusable, false);
            lease.SetLive(RetainedControlProperty.IsTabStop, false);
        }
        else
        {
            _ = _propertyOverrides.Acquire(item, RetainedPropertyOverrides.Height);
        }

        ApplyItemSizing(item);

        if (wasSelected)
        {
            var target = item is MenuItem ? index : SingleSelectionIndex.FindWrapped(index, 1, ItemControlCount, Available);
            _selectedIndex = -1;

            if (target < 0)
            {
                _selectedEntry = null;
                NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Render);
                NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);
            }
            else
            {
                Select(target, focus: false);
            }
        }

        if (item is MenuItem { Kind: MenuItemKind.Radio, IsChecked: true } radio)
        {
            SelectRadio(radio);
        }
    }

    /// <summary>Moves one owned entry to a different position, preserving its identity and subscription.</summary>
    /// <param name="oldIndex">The current zero-based entry position.</param>
    /// <param name="newIndex">The destination zero-based entry position.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="oldIndex"/> or <paramref name="newIndex"/> is outside the current entries.
    /// </exception>
    internal void MoveEntry(int oldIndex, int newIndex)
    {
        VerifyMutable();

        if ((uint) oldIndex >= (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(oldIndex), oldIndex, "The source index is outside the menu.");
        }

        if ((uint) newIndex >= (uint) ItemControlCount)
        {
            throw new ArgumentOutOfRangeException(nameof(newIndex), newIndex,
                "The destination index is outside the menu.");
        }

        if (oldIndex == newIndex)
        {
            return;
        }

        ExceptionDispatchInfo? failure = null;

        CaptureFailure(() => MoveItemControl(oldIndex, newIndex), ref failure);

        var previousSelectedIndex = _selectedIndex;

        if (_selectedIndex == oldIndex)
        {
            _selectedIndex = newIndex;
        }
        else if (oldIndex < _selectedIndex && _selectedIndex <= newIndex)
        {
            _selectedIndex--;
        }
        else if (newIndex <= _selectedIndex && _selectedIndex < oldIndex)
        {
            _selectedIndex++;
        }

        if (_selectedIndex != previousSelectedIndex)
        {
            NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Render);
            NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);
        }

        failure?.Throw();
    }

    /// <summary>Clears items and subscriptions.</summary>
    internal void ClearItems()
    {
        VerifyMutable();

        var items = new ControlBase[ItemControlCount];
        var propertyLeases = new RetainedPropertyOverrideLease[ItemControlCount];

        for (var index = 0; index < items.Length; index++)
        {
            items[index] = ItemAt(index);
            propertyLeases[index] = _propertyOverrides.Get(items[index]);
        }

        ClearItemControls();

        foreach (var propertyLease in propertyLeases)
        {
            _propertyOverrides.Restore(propertyLease);
        }

        Select(-1, focus: false);
    }

    // Reconciles state only for child-initiated disposal. Ordinary collection methods use their
    // position-aware repair after the commit; the delta distinguishes those paths without a
    // component-local mutation flag.
    /// <inheritdoc/>
    private protected override void OnItemControlsChanged(OwnedControlChange change)
    {
        base.OnItemControlsChanged(change);

        if (change.Kind != OwnedControlMutationKind.DirectDisposal)
        {
            return;
        }

        // This notification publishes from inside DisposeCore, which removes the item from
        // its owning slot before IsDisposed itself flips true — IsDisposing is what's already
        // set at this point.
        if (_spacePressedItem is { IsDisposing: true } or { IsDisposed: true })
        {
            _spacePressedItem = null;
        }

        if (_selectedIndex < 0)
        {
            return;
        }

        if (_selectedIndex >= ItemControlCount)
        {
            _selectedIndex = -1;
            _selectedEntry = null;
            NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Render);
            NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);
            return;
        }

        // A disposed entry before or at the selected index leaves _selectedIndex itself
        // unchanged (the count still fits it), but the slot it points at is now a different
        // sibling - or a MenuSeparator, which the MenuItem pattern below alone would silently
        // skip, leaving a stale cursor with nothing selected. Comparing identity, not just the
        // index, is what catches both.
        var current = ItemAt(_selectedIndex);

        if (!ReferenceEquals(current, _selectedEntry))
        {
            _selectedEntry = current;
            NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);
        }

        if (current is MenuItem item)
        {
            item.CommitSelection(ContainsFocus);
        }
    }

    /// <inheritdoc/>
    private protected override void OnItemsControlDisposing()
    {
        _propertyOverrides.Dispose();
        base.OnItemsControlDisposing();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        ExceptionDispatchInfo? failure = null;

        if (_spacePressedItem is { } item)
        {
            CaptureFailure(() => item.SetPressed(false), ref failure);
            _spacePressedItem = null;
        }

        var sessionOwner = FindSessionOwner();
        if (ReferenceEquals(sessionOwner, this) ||
            (sessionOwner.IsSessionArmed && sessionOwner._submenuSurfaceCloseDepth == 0))
        {
            CaptureFailure(sessionOwner.CloseChain, ref failure);
        }

        CaptureFailure(() => base.OnUnavailable(reason), ref failure);

        if (reason == ReleaseReason.Disposed)
        {
            ItemInvoked = null;
            ItemInvocationCompleted = null;
        }

        failure?.Throw();
    }

    /// <summary>Updates the selected index when a child item receives focus externally.</summary>
    /// <param name="item">The non-null owned item that received focus.</param>
    internal void NotifyItemFocused(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var index = IndexOfItemControl(item);

        if (index >= 0 && index != _selectedIndex)
        {
            Select(index, focus: false);
        }
    }

    /// <summary>Forwards one item invocation after the item's own subscribers complete.</summary>
    /// <param name="eventArgs">The non-null committed invocation payload.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null.</exception>
    internal void NotifyItemInvoked(MenuItemInvokedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var owner = FindSessionOwner();
        owner._itemInvocationDepth++;
        ExceptionDispatchInfo? failure = null;
        var invocationCompleted = false;

        try
        {
            CaptureFailure(
                () =>
                {
                    var index = IndexOfItemControl(eventArgs.Item);

                    if (index >= 0)
                    {
                        Select(index, focus: false);
                    }
                },
                ref failure);
            var handlers = ItemInvoked?.GetInvocationList();

            if (handlers is not null)
            {
                foreach (var handler in handlers)
                {
                    CaptureFailure(
                        () => ((EventHandler<MenuItemInvokedEventArgs>) handler).Invoke(this, eventArgs),
                        ref failure);
                }
            }

            owner._closeChainAfterInvocation = true;
        }
        finally
        {
            owner._itemInvocationDepth--;

            if (owner._itemInvocationDepth == 0 && owner._closeChainAfterInvocation)
            {
                owner._closeChainAfterInvocation = false;
                CaptureFailure(owner.CloseChain, ref failure);
                invocationCompleted = true;
            }
        }

        var completionHandlers = invocationCompleted
            ? ItemInvocationCompleted?.GetInvocationList()
            : null;

        if (completionHandlers is not null)
        {
            foreach (var handler in completionHandlers)
            {
                CaptureFailure(
                    () => ((EventHandler<MenuItemInvokedEventArgs>) handler).Invoke(this, eventArgs),
                    ref failure);
            }
        }

        failure?.Throw();
    }

    #region Modal menu session

    /// <summary>Toggles one owned item's retained submenu inside the topmost menu session.</summary>
    /// <param name="item">The non-null submenu-bearing item owned by this menu.</param>
    /// <param name="cause">The validated activation path requesting the toggle.</param>
    internal void ToggleSubmenu(MenuItem item, ActivationCause cause)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (IndexOfItemControl(item) < 0)
        {
            throw new ArgumentException("The submenu item must belong to this menu.", nameof(item));
        }

        var owner = FindSessionOwner();
        owner.ExecuteSubmenuTransition(() =>
        {
            if (item.IsSubmenuOpen)
            {
                if (cause == ActivationCause.Pointer && item.ConsumePointerSelectionOpen())
                {
                    return;
                }

                if (ReferenceEquals(this, owner))
                {
                    owner.CloseChain();
                }
                else
                {
                    CloseSubmenuBranch(item);
                }

                return;
            }

            owner.TransitionToSubmenuCore(this, item, openedFromPointerSelection: false);
        });
    }

    /// <summary>Opens one owned item's retained submenu inside the topmost menu session.</summary>
    /// <param name="item">The non-null submenu-bearing item owned by this menu.</param>
    internal void OpenSubmenu(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (IndexOfItemControl(item) < 0)
        {
            throw new ArgumentException("The submenu item must belong to this menu.", nameof(item));
        }

        var owner = FindSessionOwner();
        owner.ExecuteSubmenuTransition(
            () => owner.TransitionToSubmenuCore(this, item, openedFromPointerSelection: false));
    }

    /// <summary>Restores focus after a single submenu level closes outside complete-chain teardown.</summary>
    internal void RestoreFocusAfterSubmenuClose()
    {
        var owner = FindSessionOwner();

        if (!owner._isClosingChain)
        {
            _ = Focus();
        }
    }

    /// <summary>Begins one retained submenu-surface close and returns its exact session owner.</summary>
    /// <returns>The topmost menu whose close bracket must be completed.</returns>
    internal Menu BeginSubmenuSurfaceClose()
    {
        var owner = FindSessionOwner();
        owner._submenuSurfaceCloseDepth++;
        return owner;
    }

    /// <summary>Completes one retained submenu close against this exact original session owner.</summary>
    /// <param name="anchor">The item that owned the closing popup when the bracket began.</param>
    internal void EndSubmenuSurfaceClose(MenuItem anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        Debug.Assert(_submenuSurfaceCloseDepth > 0, "Every submenu close completion follows one close start.");

        if (_submenuSurfaceCloseDepth == 0)
        {
            return;
        }

        _submenuChainLostDuringClose |= !ModalityManager.IsWithin(anchor, this);
        _submenuSurfaceCloseDepth--;

        if (_submenuSurfaceCloseDepth == 0 && _submenuChainLostDuringClose)
        {
            _submenuChainLostDuringClose = false;

            if (IsSessionArmed)
            {
                CloseChain();
            }
        }
    }

    /// <summary>Replaces one owned item's submenu while preserving or ending the active menu session coherently.</summary>
    /// <param name="item">The non-null item owned by this menu.</param>
    /// <param name="value">The replacement submenu, or null to remove it.</param>
    internal void ReplaceSubmenu(MenuItem item, Menu? value)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (IndexOfItemControl(item) < 0)
        {
            throw new ArgumentException("The submenu item must belong to this menu.", nameof(item));
        }

        var owner = FindSessionOwner();
        var wasOpen = item.IsSubmenuOpen;
        owner.ExecuteSubmenuTransition(() =>
        {
            ExceptionDispatchInfo? failure = null;
            CaptureFailure(() => item.CommitSubmenu(value), ref failure);

            if (wasOpen && ReferenceEquals(item.Submenu, value))
            {
                if (value is null)
                {
                    owner._closeChainPending = true;
                }
                else
                {
                    CaptureFailure(
                        () => owner.TransitionToSubmenuCore(this, item, openedFromPointerSelection: false),
                        ref failure);
                }
            }
            else if (wasOpen)
            {
                owner._closeChainPending = true;
            }

            failure?.Throw();
        });
    }

    private bool IsSessionArmed =>
        _modalSession.IsActive || _modalSession.IsEntering || _submenuTransitionDepth > 0;

    /// <summary>Finds the exact top menu that owns this menu's current submenu session.</summary>
    /// <returns>This menu or its outermost owning menu.</returns>
    [Pure]
    internal Menu FindSessionOwner()
    {
        var owner = this;

        for (var current = Parent; current is not null; current = current.Parent)
        {
            if (current is Menu menu)
            {
                owner = menu;
            }
        }

        return owner;
    }

    private void TransitionToSubmenuCore(
        Menu menu,
        MenuItem item,
        bool openedFromPointerSelection)
    {
        if (_isClosingChain || _modalSession.IsEntering)
        {
            QueueSubmenuTransition(menu, item, openedFromPointerSelection);
            return;
        }

        try
        {
            CloseSiblingSubmenus(menu, item);

            if (!EnsureModalSession())
            {
                return;
            }

            if (_closeChainPending)
            {
                return;
            }

            item.OpenSubmenuSurface(openedFromPointerSelection);
        }
        catch
        {
            _closeChainPending = true;
            throw;
        }
    }

    private void QueueSubmenuTransition(
        Menu menu,
        MenuItem item,
        bool openedFromPointerSelection)
    {
        Debug.Assert(menu is not null, "A deferred submenu transition retains its containing menu.");
        Debug.Assert(item is not null, "A deferred submenu transition retains its selected item.");
        _pendingSubmenuMenu = menu;
        _pendingSubmenuOpen = item;
        _pendingSubmenuOpenFromPointerSelection = openedFromPointerSelection;
        _pendingSubmenuModalityOwner = ModalityOwner;
        _pendingSubmenuSession = ModalityOwner?.Active;
    }

    private void DiscardPendingSubmenuTransition()
    {
        _pendingSubmenuMenu = null;
        _pendingSubmenuOpen = null;
        _pendingSubmenuOpenFromPointerSelection = false;
        _pendingSubmenuModalityOwner = null;
        _pendingSubmenuSession = null;
    }

    private void ReplayPendingSubmenuTransition()
    {
        var menu = _pendingSubmenuMenu;
        var item = _pendingSubmenuOpen;
        var openedFromPointerSelection = _pendingSubmenuOpenFromPointerSelection;
        var modalityOwner = _pendingSubmenuModalityOwner;
        var session = _pendingSubmenuSession;
        DiscardPendingSubmenuTransition();

        if (menu is null || item is null)
        {
            return;
        }

        if (!CanReplaySubmenuTransition(menu, item, modalityOwner, session))
        {
            CloseChain(replayPendingSubmenuTransition: false);
            return;
        }

        ExecuteSubmenuTransition(
            () => TransitionToSubmenuCore(menu, item, openedFromPointerSelection));
    }

    [Pure]
    private bool CanReplaySubmenuTransition(
        Menu menu,
        MenuItem item,
        ModalityManager? modalityOwner,
        ModalScope? session)
    {
        return ReferenceEquals(menu.FindSessionOwner(), this) &&
            !menu.IsDisposed && menu.EffectiveIsEnabled && menu.EffectiveIsVisible &&
            menu.IndexOfItemControl(item) >= 0 &&
            !item.IsDisposed && item.EffectiveIsEnabled && item.EffectiveIsVisible &&
            item.HasRetainedSubmenuSurface &&
            ReferenceEquals(ModalityOwner, modalityOwner) &&
            ReferenceEquals(menu.ModalityOwner, modalityOwner) &&
            ReferenceEquals(item.ModalityOwner, modalityOwner) &&
            (modalityOwner is null ||
                (ReferenceEquals(modalityOwner.Active, session) && session?.IsActive != false));
    }

    private bool EnsureModalSession()
    {
        if (UsesExternalModalSession)
        {
            return true;
        }

        if (_modalSession.IsActive)
        {
            return true;
        }

        if (ModalityOwner is not { } modality)
        {
            return true;
        }

        if (_modalSession.IsEntering)
        {
            return false;
        }

        var scope = _modalSession.Enter(
            () => modality.Enter(this, OutsideInteraction.Dismiss),
            () => !IsDisposed &&
                EffectiveIsEnabled &&
                EffectiveIsVisible &&
                ReferenceEquals(ModalityOwner, modality));
        return scope.IsActive && ReferenceEquals(_modalSession.Current, scope);
    }

    private void OnModalDismissRequested(ModalScope scope)
    {
        if (scope.IsActive)
        {
            CloseChain();
        }
    }

    private void OnModalScopeExited(ModalScope scope)
    {
        _ = scope;

        if (!_isClosingChain)
        {
            CloseChain();
        }
    }

    /// <summary>Closes every retained submenu and releases this exact owner's modal session.</summary>
    /// <exception cref="Exception">A popup or scope callback fails after cleanup is attempted.</exception>
    internal void CloseChain() => CloseChain(replayPendingSubmenuTransition: true);

    private void CloseChain(bool replayPendingSubmenuTransition)
    {
        if (_submenuTransitionDepth > 0 || _modalSession.IsEntering)
        {
            _closeChainPending = true;
            _discardPendingSubmenuTransitionOnClose |= !replayPendingSubmenuTransition;
            return;
        }

        if (_isClosingChain)
        {
            _discardPendingSubmenuTransitionOnClose |= !replayPendingSubmenuTransition;
            return;
        }

        replayPendingSubmenuTransition &= !_discardPendingSubmenuTransitionOnClose;
        _discardPendingSubmenuTransitionOnClose = false;
        _isClosingChain = true;
        _closeChainPending = false;
        ExceptionDispatchInfo? failure = null;

        try
        {
            CloseOpenSubmenus(this, ref failure);
            CaptureFailure(_modalSession.Exit, ref failure);
        }
        finally
        {
            _isClosingChain = false;
        }

        if (replayPendingSubmenuTransition && failure is null)
        {
            CaptureFailure(ReplayPendingSubmenuTransition, ref failure);
        }
        else
        {
            DiscardPendingSubmenuTransition();
        }

        failure?.Throw();
    }

    private void ExecuteSubmenuTransition(Action action)
    {
        Debug.Assert(action is not null, "A submenu transition requires one operation.");
        _submenuTransitionDepth++;
        ExceptionDispatchInfo? failure = null;

        try
        {
            CaptureFailure(action, ref failure);
        }
        finally
        {
            _submenuTransitionDepth--;
        }

        var transitionFailed = failure is not null;

        if (transitionFailed)
        {
            _closeChainPending = true;
            DiscardPendingSubmenuTransition();
        }

        if (_submenuTransitionDepth == 0)
        {
            if (_closeChainPending)
            {
                CaptureFailure(
                    () => CloseChain(replayPendingSubmenuTransition: !transitionFailed),
                    ref failure);
            }
            else if (_pendingSubmenuOpen is not null)
            {
                CaptureFailure(ReplayPendingSubmenuTransition, ref failure);
            }
        }

        failure?.Throw();
    }

    private static void CloseSiblingSubmenus(Menu menu, MenuItem selected)
    {
        for (var index = 0; index < menu.ItemControlCount; index++)
        {
            if (menu.ItemAt(index) is MenuItem sibling && !ReferenceEquals(sibling, selected))
            {
                sibling.CloseSubmenu();
            }
        }
    }

    private static void CloseSubmenuBranch(MenuItem item)
    {
        ExceptionDispatchInfo? failure = null;

        if (item.Submenu is { } submenu)
        {
            CloseOpenSubmenus(submenu, ref failure);
        }

        CaptureFailure(item.CloseSubmenu, ref failure);
        failure?.Throw();
    }

    private static void CloseOpenSubmenus(Menu menu, ref ExceptionDispatchInfo? failure)
    {
        var traversal = new Stack<(Menu Menu, bool IsExpanded)>();
        var closeOrder = new List<MenuItem>();
        traversal.Push((menu, false));

        while (traversal.Count > 0)
        {
            var current = traversal.Pop();

            if (current.IsExpanded)
            {
                for (var index = 0; index < current.Menu.ItemControlCount; index++)
                {
                    if (current.Menu.ItemAt(index) is MenuItem item)
                    {
                        closeOrder.Add(item);
                    }
                }

                continue;
            }

            traversal.Push((current.Menu, true));

            for (var index = current.Menu.ItemControlCount - 1; index >= 0; index--)
            {
                if (current.Menu.ItemAt(index) is MenuItem { Submenu: { } submenu })
                {
                    traversal.Push((submenu, false));
                }
            }
        }

        foreach (var item in closeOrder)
        {
            CaptureFailure(item.CloseSubmenu, ref failure);
        }
    }

    #endregion

    private void Select(int index, bool focus)
    {
        VerifyMutable();

        if (_selectedIndex == index)
        {
            return;
        }

        if (_selectedIndex >= 0 && _selectedIndex < ItemControlCount && ItemAt(_selectedIndex) is MenuItem outgoing)
        {
            outgoing.CommitSelection(false);
        }

        _selectedIndex = index;
        _selectedEntry = index >= 0 ? ItemAt(index) : null;

        if (index >= 0)
        {
            var item = (MenuItem) ItemAt(index);
            item.CommitSelection(ContainsFocus);

            if (focus)
            {
                _ = Focus();
            }
        }

        NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Render);
        NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);
    }

    [Pure]
    private int IndexOfItem(MenuItem item)
    {
        for (var index = 0; index < ItemControlCount; index++)
        {
            if (ReferenceEquals(ItemAt(index), item))
            {
                return index;
            }
        }

        return -1;
    }

    private void OnFocusEntered(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        CommitSelectionPresentation(true);
    }

    private void OnFocusLeft(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        CommitSelectionPresentation(false);
    }

    private void CommitSelectionPresentation(bool value)
    {
        if (_selectedIndex >= 0 && _selectedIndex < ItemControlCount && ItemAt(_selectedIndex) is MenuItem outgoing)
        {
            outgoing.CommitSelection(value);
        }
    }

    [Pure]
    private bool Available(int index) =>
        ItemAt(index) is MenuItem { EffectiveIsEnabled: true, EffectiveIsVisible: true };

    private bool ActivateSelected()
    {
        if (_selectedIndex < 0 || ItemAt(_selectedIndex) is not MenuItem item ||
            !item.EffectiveIsEnabled || !item.EffectiveIsVisible)
        {
            return false;
        }

        item.ActivateFromMenu(ActivationCause.Keyboard);
        return true;
    }

    [Pure]
    private bool HasOpenSubmenu()
    {
        for (var index = 0; index < ItemControlCount; index++)
        {
            if (ItemAt(index) is MenuItem { IsSubmenuOpen: true })
            {
                return true;
            }
        }

        return false;
    }

    private void HandleSpace(KeyEventArgs eventArgs)
    {
        var stroke = eventArgs.Stroke;

        if (stroke.Code != Code.Character || stroke.Character != new Rune(' '))
        {
            return;
        }

        if (eventArgs.IsInitialKeyDown && _spacePressedItem is null)
        {
            // An incidental modifier must not silently arm the pressed frame - move the gate
            // ahead of IsHandled so a modified Space still bubbles for a shortcut to see.
            if (!stroke.Modifiers.IsActivationEligible())
            {
                return;
            }

            eventArgs.IsHandled = true;

            if (_selectedIndex >= 0 && ItemAt(_selectedIndex) is MenuItem
                {
                    EffectiveIsEnabled: true, EffectiveIsVisible: true
                } selected)
            {
                _spacePressedItem = selected;
                selected.SetPressed(true);
            }

            return;
        }

        if (eventArgs.IsKeyUp)
        {
            if (_spacePressedItem is not { } held)
            {
                // The paired press never armed an item here - either it was gated by an
                // incidental modifier, or it was never observed at all. A modifier-carrying
                // release must bubble to match its gated press instead of being silently
                // swallowed here; an eligible unmatched release keeps the consumed no-op
                // behavior it has always had.
                eventArgs.IsHandled = stroke.Modifiers.IsActivationEligible();
                return;
            }

            // The armed hold always consumes its paired release, whether or not it goes on to
            // activate. But an incidental modifier that appears only between press and release
            // must not silently commit the activation the user did not intend - gate the
            // activation on eligibility, mirroring the press-side gate, without un-consuming
            // the stroke.
            eventArgs.IsHandled = true;
            _spacePressedItem = null;
            held.SetPressed(false);

            if (stroke.Modifiers.IsActivationEligible() &&
                held is { EffectiveIsEnabled: true, EffectiveIsVisible: true } &&
                _selectedIndex >= 0 && ReferenceEquals(ItemAt(_selectedIndex), held))
            {
                held.ActivateFromMenu(ActivationCause.Keyboard);
            }

            return;
        }

        eventArgs.IsHandled = true;
    }

    private void SelectPointerTarget(PointerEventArgs eventArgs)
    {
        if (eventArgs.Pointer.Action == PointerAction.Wheel)
        {
            return;
        }

        if (eventArgs.Pointer.Action == PointerAction.Leave ||
            eventArgs.Pointer.Cells is not { } cells)
        {
            ClearPointerSelectionOpens();
            return;
        }

        for (var index = 0; index < ItemControlCount; index++)
        {
            if (ItemAt(index) is MenuItem item && item.Bounds.Contains(cells) &&
                item is { EffectiveIsEnabled: true, EffectiveIsVisible: true })
            {
                var pointerSelection = eventArgs.Pointer.Action == PointerAction.Move;
                SelectFromInput(
                    index,
                    focus: false,
                    switchSubmenu: pointerSelection,
                    openedFromPointerSelection: pointerSelection);
                return;
            }
        }

        ClearPointerSelectionOpens();
    }

    private void ClearPointerSelectionOpens()
    {
        for (var index = 0; index < ItemControlCount; index++)
        {
            if (ItemAt(index) is MenuItem item)
            {
                item.ClearPointerSelectionOpen();
            }
        }
    }

    private void SelectFromInput(
        int index,
        bool focus,
        bool switchSubmenu,
        bool openedFromPointerSelection)
    {
        var owner = FindSessionOwner();
        var selected = ItemAt(index) is MenuItem item ? item : null;
        var dispatcher = Dispatcher;
        var attachment = dispatcher is null ? null : CaptureAttachment();
        switchSubmenu &= HasOpenSubmenu() || owner.IsSessionArmed;
        Select(index, focus);

        if (!switchSubmenu ||
            selected is null ||
            !IsCurrentAttachmentOrDetached(attachment) ||
            !ReferenceEquals(_selectedEntry, selected) ||
            IndexOfItem(selected) < 0 ||
            !ReferenceEquals(FindSessionOwner(), owner))
        {
            return;
        }

        owner.ExecuteSubmenuTransition(() =>
        {
            if (IsCurrentAttachmentOrDetached(attachment) &&
                ReferenceEquals(_selectedEntry, selected) &&
                IndexOfItem(selected) >= 0 &&
                ReferenceEquals(FindSessionOwner(), owner))
            {
                owner.TransitionToSubmenuCore(this, selected, openedFromPointerSelection);
            }
        });
    }

    private bool IsCurrentAttachmentOrDetached(ControlAttachmentToken? attachment) =>
        attachment is { } token ? IsCurrent(token) : Dispatcher is null;

    private void UpdateItemSizing()
    {
        for (var index = 0; index < ItemControlCount; index++)
        {
            ApplyItemSizing(ItemAt(index));
        }
    }

    private void ApplyItemSizing(ControlBase item) =>
        _propertyOverrides.Get(item).SetLive(RetainedControlProperty.Height, Length.Cells(1));

    [Pure]
    private static ControlBase RequireEntry(ControlBase child)
    {
        return child is MenuItem or MenuSeparator
            ? child
            : throw new InvalidOperationException(
                "Menus may own only MenuItem and MenuSeparator controls through Items.");
    }

}
