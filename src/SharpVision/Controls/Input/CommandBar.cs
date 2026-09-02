// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.ComponentModel;
using System.Runtime.ExceptionServices;

using Menus;

using Popups;

using SharpVision.Terminal.Input;

/// <summary>Retains a single-row command surface with deterministic source-order tail overflow.</summary>
/// <remarks>
/// The control is one focusable Tab stop. Semantic items remain owned by its permanent private
/// host when their private menu projections are visible, so command identity and application
/// subscriptions never move between control-tree planes.
/// </remarks>
[PublicAPI]
public sealed class CommandBar: ItemsControl, IStyled<CommandBarStyle>
{
    private readonly CommandBarHost _host;
    private readonly CommandBarOverflowButton _overflowButton;
    private readonly Menu _overflowMenu;
    private readonly Popup _overflowPopup;
    private readonly PopupDropDownCoordinator _overflowCoordinator;
    private readonly RetainedPropertyOverrideService _propertyOverrides;
    private readonly StyleSlot<CommandBarStyle> _style;
    private readonly HashSet<ControlBase> _primaryEntries = [];
    private readonly List<ControlBase> _overflowEntries = [];
    private readonly List<CommandBarOverflowProjection> _projections = [];
    private readonly Dictionary<CommandBarItem, CommandBarOverflowProjection> _projectionBySource = [];
    private int _selectedIndex = -1;
    private CommandBarItem? _selectedItem;
    private ControlBase? _spacePressedTarget;
    private int _primaryExtent;
    private int _lastLayoutWidth = -1;
    private ulong _entriesGeneration;
    private ulong _activationGeneration;
    private ulong _availabilityGeneration;
    private bool _overflowTargetSelected;
    private bool _hasLayoutSnapshot;

    #region Construction and public properties

    /// <summary>Initializes an empty, focusable command bar with one-cell inter-entry spacing.</summary>
    public CommandBar()
    {
        _host = new CommandBarHost(this);
        InitializeItemsHost(_host);
        _propertyOverrides = new RetainedPropertyOverrideService(this, ItemControlsSlot);
        _style = InitializeStyle(CommandBarStyle.Definition);
        Items = new CommandBarEntryCollection(this);

        _overflowButton = new CommandBarOverflowButton(this);
        var triggerSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: false,
                partKey: "overflow-trigger",
                InvalidationImpact.Measure),
            capacity: 1);
        triggerSlot.Add(_overflowButton);

        _overflowMenu = new Menu
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
            UsesExternalModalSession = true,
            IsTabStop = false
        };
        _overflowMenu.ItemInvocationCompleted += OnOverflowItemInvocationCompleted;
        _overflowPopup = new Popup
        {
            Anchor = _overflowButton,
            Content = _overflowMenu,
            FocusOnOpen = true,
            ModalBehavior = PopupModalBehavior.None,
            Placement = PopupPlacement.Below,
            SuppressCloseOtherPopups = true,
            TabNavigation = TabNavigation.None,
            TracksAnchorReflow = false
        };
        var popupSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Popup,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "overflow",
                InvalidationImpact.Measure),
            capacity: 1);
        popupSlot.Add(_overflowPopup);
        _overflowCoordinator = new PopupDropDownCoordinator(
            this,
            _overflowPopup,
            _overflowMenu,
            RequestFocus,
            () => NotifyPropertyChanged(nameof(IsOverflowOpen), InvalidationImpact.None),
            static () => { },
            static () => { });

        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.None;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        FocusEntered += OnFocusEntered;
        FocusLeft += OnFocusLeft;
    }

    /// <summary>Raised after an eligible item event and before its captured command executes.</summary>
    public event EventHandler<CommandBarItemInvokedEventArgs>? ItemInvoked;

    /// <summary>Gets the typed semantic entries retained in source order.</summary>
    public CommandBarEntryCollection Items { get; }

    /// <summary>Gets or sets the non-negative cells between participating primary entries.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int Spacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = 1;

    /// <summary>Gets or selects one owned visible and enabled command item by source index.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned index is outside the entries.</exception>
    /// <exception cref="ArgumentException">The assigned index identifies a separator.</exception>
    /// <exception cref="InvalidOperationException">
    /// The assigned item is unavailable, or the attached control is mutated off-dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            VerifyMutable();

            if (value < -1 || value >= EntryCount)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The selected index is outside the command bar.");
            }

            if (value >= 0 && EntryAt(value) is CommandBarSeparator)
            {
                throw new ArgumentException("A separator cannot become selected.", nameof(value));
            }

            if (value >= 0 && !IsAvailableItem((CommandBarItem) EntryAt(value)))
            {
                throw new InvalidOperationException("Only a visible enabled command item can become selected.");
            }

            Select(value >= 0 ? (CommandBarItem) EntryAt(value) : null);
        }
    }

    /// <summary>Gets or selects one owned visible and enabled command item; null or a foreign item clears selection.</summary>
    /// <exception cref="InvalidOperationException">
    /// An owned assigned item is unavailable, or the attached control is mutated off-dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public CommandBarItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            VerifyMutable();

            if (value is null)
            {
                Select(null);
                return;
            }

            var index = IndexOfItemControl(value);

            if (index >= 0 && !IsAvailableItem(value))
            {
                throw new InvalidOperationException("Only a visible enabled command item can become selected.");
            }

            Select(index >= 0 ? value : null);
        }
    }

    /// <summary>Gets whether the private overflow menu currently owns an active popup session.</summary>
    public bool IsOverflowOpen => _overflowCoordinator.IsOpen;

    /// <summary>Gets or sets the complete local presentation, or null for inherited theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public CommandBarStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public CommandBarStyle ActualStyle => _style.Actual;

    #endregion

    #region Semantic collection ownership

    /// <summary>Gets one checked retained entry by source position.</summary>
    /// <param name="index">The valid source index.</param>
    /// <returns>The exact retained semantic entry.</returns>
    internal ControlBase EntryAt(int index) => RequireEntry(GetItemControl(index));

    /// <summary>Gets the current semantic entry count.</summary>
    internal int EntryCount => ItemControlCount;

    /// <summary>Gets the source position of one retained entry, or -1.</summary>
    /// <param name="entry">The non-null candidate.</param>
    internal int IndexOfEntry(ControlBase entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return IndexOfItemControl(entry);
    }

    /// <summary>Inserts one validated semantic entry at a source position.</summary>
    /// <param name="index">The insertion position.</param>
    /// <param name="entry">The detached item or separator.</param>
    internal void InsertEntry(int index, ControlBase entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _ = RequireEntry(entry);
        VerifyMutable();

        if ((uint) index > (uint) EntryCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The insertion index is outside the command bar.");
        }

        InsertItemControl(index, entry);
        AcquireEntry(entry);

        if (_selectedItem is not null)
        {
            _selectedIndex = IndexOfItemControl(_selectedItem);
        }

        PublishEntriesChanged();
    }

    /// <summary>Removes one identical semantic entry without disposal.</summary>
    /// <param name="entry">The candidate entry.</param>
    /// <returns>True when ownership was removed.</returns>
    internal bool RemoveEntry(ControlBase entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        VerifyMutable();
        var index = IndexOfItemControl(entry);

        if (index < 0)
        {
            return false;
        }

        var selectedWasRemoved = ReferenceEquals(entry, _selectedItem);
        var propertyLease = _propertyOverrides.Get(entry);
        ReleaseEntry(entry);
        _ = RemoveItemControl(entry);
        _propertyOverrides.Restore(propertyLease);

        if (selectedWasRemoved)
        {
            SelectNearest(Math.Min(index, EntryCount - 1));
        }
        else if (_selectedItem is not null)
        {
            _selectedIndex = IndexOfItemControl(_selectedItem);
        }

        PublishEntriesChanged();
        return true;
    }

    /// <summary>Removes the semantic entry at one source position without disposal.</summary>
    /// <param name="index">The valid source position.</param>
    internal void RemoveEntryAt(int index)
    {
        VerifyMutable();

        if ((uint) index >= (uint) EntryCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The removal index is outside the command bar.");
        }

        _ = RemoveEntry(EntryAt(index));
    }

    /// <summary>Replaces one retained entry after complete candidate validation.</summary>
    /// <param name="index">The valid source position.</param>
    /// <param name="entry">The detached item or separator.</param>
    internal void ReplaceEntry(int index, ControlBase entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _ = RequireEntry(entry);
        VerifyMutable();

        if ((uint) index >= (uint) EntryCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The replacement index is outside the command bar.");
        }

        var previous = EntryAt(index);

        if (ReferenceEquals(previous, entry))
        {
            return;
        }

        var selectedWasReplaced = ReferenceEquals(previous, _selectedItem);
        var propertyLease = _propertyOverrides.Get(previous);
        ReleaseEntry(previous);
        ReplaceItemControl(index, entry);
        _propertyOverrides.Restore(propertyLease);
        AcquireEntry(entry);

        if (selectedWasReplaced)
        {
            Select(entry is CommandBarItem item && IsAvailableItem(item)
                ? item
                : FindNearestAvailable(index));
        }
        else if (_selectedItem is not null)
        {
            _selectedIndex = IndexOfItemControl(_selectedItem);
        }

        PublishEntriesChanged();
    }

    /// <summary>Moves one retained identity to a new source position.</summary>
    /// <param name="oldIndex">The current source position.</param>
    /// <param name="newIndex">The destination source position.</param>
    internal void MoveEntry(int oldIndex, int newIndex)
    {
        VerifyMutable();

        if ((uint) oldIndex >= (uint) EntryCount)
        {
            throw new ArgumentOutOfRangeException(nameof(oldIndex), oldIndex, "The source index is outside the command bar.");
        }

        if ((uint) newIndex >= (uint) EntryCount)
        {
            throw new ArgumentOutOfRangeException(nameof(newIndex), newIndex, "The destination index is outside the command bar.");
        }

        if (oldIndex == newIndex)
        {
            return;
        }

        MoveItemControl(oldIndex, newIndex);

        if (_selectedItem is not null)
        {
            _selectedIndex = IndexOfItemControl(_selectedItem);
        }

        PublishEntriesChanged();
    }

    /// <summary>Detaches every semantic entry as one ownership transaction.</summary>
    internal void ClearEntries()
    {
        VerifyMutable();
        var entries = Items.ToArray();
        var leases = entries.Select(_propertyOverrides.Get).ToArray();

        foreach (var entry in entries)
        {
            ReleaseEntry(entry);
        }

        ClearItemControls();

        foreach (var lease in leases)
        {
            _propertyOverrides.Restore(lease);
        }

        Select(null);
        PublishEntriesChanged();
    }

    private void AcquireEntry(ControlBase entry)
    {
        var descriptors = entry is CommandBarItem
            ? new[]
            {
                RetainedPropertyOverrides.IsFocusable,
                RetainedPropertyOverrides.IsTabStop,
                RetainedPropertyOverrides.Height
            }
            : [RetainedPropertyOverrides.Height];
        var lease = _propertyOverrides.Acquire(entry, descriptors);

        if (entry is CommandBarItem item)
        {
            lease.SetLive(RetainedControlProperty.IsFocusable, false);
            lease.SetLive(RetainedControlProperty.IsTabStop, false);
            item.PropertyChanged += OnEntryPropertyChanged;
        }

        lease.SetLive(RetainedControlProperty.Height, Length.Cells(1));
    }

    private void ReleaseEntry(ControlBase entry)
    {
        if (entry is CommandBarItem item)
        {
            item.PropertyChanged -= OnEntryPropertyChanged;

            if (!item.IsDisposing && !item.IsDisposed)
            {
                item.SetOverflowed(false);
                item.CommitSelection(false);
            }
        }
    }

    private void PublishEntriesChanged()
    {
        _entriesGeneration++;
        CancelSpacePress();
        _overflowButton.CancelPress();

        if (IsOverflowOpen)
        {
            _overflowCoordinator.SetOpen(false);
        }

        Invalidate(Invalidation.Measure);
    }

    #endregion

    #region Selection and activation

    /// <summary>Runs the canonical captured-command activation for one owned item.</summary>
    /// <param name="item">The semantic source item.</param>
    /// <param name="cause">The validated activation path.</param>
    internal void InvokeItem(CommandBarItem item, ActivationCause cause)
    {
        ArgumentNullException.ThrowIfNull(item);
        VerifyMutable();
        ArgumentOutOfRangeException.ThrowIfNotDefined(cause);

        if (!IsAvailableItem(item))
        {
            return;
        }

        Select(item);
        var (command, parameter) = item.CaptureCommand();

        if (command is not null && !command.CanExecute(parameter))
        {
            return;
        }

        var activation = ++_activationGeneration;
        var entries = _entriesGeneration;
        var availability = _availabilityGeneration;
        var itemAvailability = item.AvailabilityGeneration;
        ExceptionDispatchInfo? failure = null;
        CaptureFailure(() => item.RaiseInvoked(cause), ref failure);

        if (IsActivationCurrent(item, activation, entries, availability, itemAvailability))
        {
            var eventArgs = new CommandBarItemInvokedEventArgs(item, cause);
            CaptureFailure(() => ItemInvoked?.Invoke(this, eventArgs), ref failure);
        }

        if (IsActivationCurrent(item, activation, entries, availability, itemAvailability))
        {
            CaptureFailure(() => command?.Execute(parameter), ref failure);
        }

        failure?.Throw();
    }

    /// <summary>Routes one private menu face back through its semantic source item.</summary>
    /// <param name="item">The retained source.</param>
    /// <param name="cause">The menu activation path.</param>
    internal void InvokeProjection(CommandBarItem item, ActivationCause cause)
    {
        ArgumentNullException.ThrowIfNull(item);
        InvokeItem(item, cause);
    }

    /// <summary>Focuses, selects, and routes a matched caption through its current plane.</summary>
    /// <param name="item">The caption owner matched by access-key discovery.</param>
    /// <param name="key">The matched scalar.</param>
    /// <returns>True when the current owned item accepted the match.</returns>
    internal bool InvokeAccessKey(CommandBarItem item, Rune key)
    {
        ArgumentNullException.ThrowIfNull(item);
        _ = key;

        if (!IsAvailableItem(item))
        {
            return false;
        }

        _ = RequestFocus();
        Select(item);

        if (!item.IsOverflowed)
        {
            item.InvokeFromProjection(ActivationCause.Keyboard);
            return true;
        }

        return OpenOverflow(item);
    }

    /// <summary>Selects and focuses a semantic item when its primary pointer face is pressed.</summary>
    /// <param name="item">The semantic pointer target.</param>
    /// <param name="eventArgs">The routed pointer candidate.</param>
    internal void PrepareItemPointer(CommandBarItem item, RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (eventArgs is PointerEventArgs { Pointer.Action: PointerAction.Press } && IsAvailableItem(item))
        {
            _ = RequestFocus();
            Select(item);
        }
    }

    [Pure]
    private bool IsActivationCurrent(
        CommandBarItem item,
        ulong activation,
        ulong entries,
        ulong availability,
        ulong itemAvailability) =>
        !IsDisposed &&
        activation == _activationGeneration &&
        entries == _entriesGeneration &&
        availability == _availabilityGeneration &&
        itemAvailability == item.AvailabilityGeneration &&
        IsAvailableItem(item);

    private void Select(CommandBarItem? item)
    {
        VerifyMutable();

        if (ReferenceEquals(_selectedItem, item) && !_overflowTargetSelected)
        {
            return;
        }

        _selectedItem?.CommitSelection(false);
        _overflowTargetSelected = false;
        _overflowButton.CommitSelection(false);
        _selectedItem = item;
        _selectedIndex = item is null ? -1 : IndexOfItemControl(item);
        item?.CommitSelection(ContainsFocus && !item.IsOverflowed);
        NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Render);
        NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);
    }

    private void SelectOverflowTarget()
    {
        if (_overflowTargetSelected)
        {
            return;
        }

        _selectedItem?.CommitSelection(false);
        _selectedItem = null;
        _selectedIndex = -1;
        _overflowTargetSelected = true;
        _overflowButton.CommitSelection(ContainsFocus);
        NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Render);
        NotifyPropertyChanged(nameof(SelectedItem), InvalidationImpact.Render);
    }

    private void SelectNearest(int sourceIndex) => Select(FindNearestAvailable(sourceIndex));

    [Pure]
    private CommandBarItem? FindNearestAvailable(int sourceIndex)
    {
        var index = SingleSelectionIndex.FindNearest(sourceIndex, EntryCount, IsAvailableIndex);
        return index < 0 ? null : (CommandBarItem) EntryAt(index);
    }

    [Pure]
    private bool IsAvailableIndex(int index) =>
        EntryAt(index) is CommandBarItem item && IsAvailableItem(item);

    [Pure]
    private bool IsAvailableItem(CommandBarItem item) =>
        !item.IsDisposed &&
        IndexOfItemControl(item) >= 0 &&
        item.Visibility == Visibility.Visible &&
        item.EffectiveIsVisible &&
        item.EffectiveIsEnabled;

    #endregion

    #region Layout and overflow projection

    /// <summary>Reports whether one semantic host child belongs to the current primary snapshot.</summary>
    /// <param name="entry">The checked retained entry.</param>
    /// <returns>True when the host should arrange it.</returns>
    internal bool IsPrimaryEntry(ControlBase entry) => _primaryEntries.Contains(entry);

    /// <summary>Resolves one semantic overflow foreground against this bar's inherited theme.</summary>
    /// <param name="color">The validated style color.</param>
    /// <returns>The literal terminal color.</returns>
    internal Color ResolveOverflowColor(ControlColor color) => ResolveColor(color);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var padding = ActualStyle.Padding;
        var availableHeight = constraint.Height.Subtract(padding.Vertical);
        var desired = MeasureChild(_host, new Constraint(width: null, availableHeight));
        _ = MeasureChild(_overflowPopup, new Constraint(constraint.Width, height: null));
        return new Size(
            desired.Width.Add(_host.Margin.Horizontal).Add(padding.Horizontal),
            desired.Height.Add(_host.Margin.Vertical).Add(padding.Vertical));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var content = ActualStyle.Padding.Deflate(bounds);
        PrepareLayout(content.Width);

        var hostWidth = Math.Min(content.Width, _primaryExtent);
        ArrangeChild(_host, new Rect(content.X, content.Y, hostWidth, content.Height), ResolvedAxes.Both);

        var triggerGap = _primaryEntries.Count > 0 ? Spacing : 0;
        var triggerX = content.X.Add(_primaryExtent).Add(triggerGap);
        var showTrigger = _overflowEntries.OfType<CommandBarItem>().Any() &&
            triggerX >= content.X && triggerX < content.Right;
        ArrangeChild(
            _overflowButton,
            showTrigger ? new Rect(triggerX, content.Y, 1, Math.Min(1, content.Height)) : new Rect(content.X, content.Y, 0, 0),
            ResolvedAxes.Both);
        ArrangeChild(_overflowPopup, RootBounds(bounds), ResolvedAxes.Both);
    }

    private void PrepareLayout(int availableWidth)
    {
        var visibleCommands = Items
            .OfType<CommandBarItem>()
            .Where(static item => item.Visibility == Visibility.Visible)
            .ToArray();
        var fullPrimary = BuildPrimaryEntries(visibleCommands, visibleCommands.Length);
        var fullExtent = MeasureEntries(fullPrimary);
        var prefix = visibleCommands.Length;

        if (fullExtent > availableWidth)
        {
            prefix = 0;

            for (var candidate = visibleCommands.Length - 1; candidate >= 0; candidate--)
            {
                var entries = BuildPrimaryEntries(visibleCommands, candidate);
                var extent = MeasureEntries(entries);
                var triggerGap = entries.Count > 0 ? Spacing : 0;

                if (extent.Add(triggerGap).Add(1) <= availableWidth)
                {
                    prefix = candidate;
                    break;
                }
            }
        }

        var primary = BuildPrimaryEntries(visibleCommands, prefix);
        var overflow = BuildOverflowEntries(visibleCommands, prefix);
        var primaryExtent = MeasureEntries(primary);
        var changed = !_hasLayoutSnapshot ||
            _lastLayoutWidth != availableWidth ||
            _primaryExtent != primaryExtent ||
            !_primaryEntries.SetEquals(primary) ||
            !SequenceEqual(_overflowEntries, overflow);

        if (changed)
        {
            CancelSpacePress();
            _overflowButton.CancelPress();

            if (IsOverflowOpen)
            {
                _overflowCoordinator.SetOpen(false);
            }
        }

        _primaryEntries.Clear();
        _primaryEntries.UnionWith(primary);
        _overflowEntries.Clear();
        _overflowEntries.AddRange(overflow);
        _primaryExtent = primaryExtent;
        _lastLayoutWidth = availableWidth;
        _hasLayoutSnapshot = true;

        var overflowItems = overflow.OfType<CommandBarItem>().ToHashSet();

        foreach (var item in Items.OfType<CommandBarItem>())
        {
            item.SetOverflowed(overflowItems.Contains(item));
        }

        CommitSelectionPresentation(ContainsFocus);
        SynchronizeOverflowProjections();
    }

    private List<ControlBase> BuildPrimaryEntries(CommandBarItem[] visibleCommands, int prefix)
    {
        var primaryCommands = visibleCommands.Take(prefix).ToHashSet();
        var normalized = NormalizePlane(primaryCommands);
        var result = new List<ControlBase>();

        foreach (var entry in Items)
        {
            if (entry.Visibility == Visibility.Hidden || normalized.Contains(entry))
            {
                result.Add(entry);
            }
        }

        return result;
    }

    private List<ControlBase> BuildOverflowEntries(CommandBarItem[] visibleCommands, int prefix) =>
        [.. NormalizePlane([.. visibleCommands.Skip(prefix)])
            .Where(static entry => entry.Visibility == Visibility.Visible)];

    private HashSet<ControlBase> NormalizePlane(HashSet<CommandBarItem> commands)
    {
        var result = new HashSet<ControlBase>();
        CommandBarSeparator? pendingSeparator = null;
        var hasCommand = false;

        foreach (var entry in Items)
        {
            switch (entry)
            {
                case CommandBarItem item when item.Visibility == Visibility.Visible && commands.Contains(item):
                    if (hasCommand && pendingSeparator is not null)
                    {
                        _ = result.Add(pendingSeparator);
                    }

                    _ = result.Add(item);
                    hasCommand = true;
                    pendingSeparator = null;
                    break;
                case CommandBarSeparator separator when separator.Visibility == Visibility.Visible:
                    pendingSeparator ??= separator;
                    break;
                case CommandBarItem or CommandBarSeparator:
                    break;
                default:
                    throw new UnreachableException();
            }
        }

        return result;
    }

    [Pure]
    private int MeasureEntries(List<ControlBase> entries)
    {
        var extent = 0;

        foreach (var entry in entries)
        {
            extent = extent.Add(entry.DesiredSize.Width).Add(entry.Margin.Horizontal);
        }

        return extent.Add(LayoutMath.GapExtent(Spacing, entries.Count, int.MaxValue));
    }

    [Pure]
    private static bool SequenceEqual(List<ControlBase> current, List<ControlBase> next) =>
        current.Count == next.Count && current.SequenceEqual(next, ReferenceEqualityComparer.Instance);

    private void SynchronizeOverflowProjections()
    {
        if (_overflowEntries.Count == _overflowMenu.Items.Count)
        {
            var matches = true;

            for (var index = 0; index < _overflowEntries.Count; index++)
            {
                var source = _overflowEntries[index];
                var projected = _overflowMenu.Items[index];
                matches &= source is CommandBarItem item
                    ? _projectionBySource.TryGetValue(item, out var projection) && ReferenceEquals(projection.Item, projected)
                    : projected is MenuSeparator;
            }

            if (matches)
            {
                return;
            }
        }

        ReleaseOverflowProjections(disposeFaces: true);

        foreach (var entry in _overflowEntries)
        {
            if (entry is CommandBarItem item)
            {
                var projection = new CommandBarOverflowProjection(this, item);
                _projections.Add(projection);
                _projectionBySource.Add(item, projection);
                _overflowMenu.Items.Add(projection.Item);
            }
            else
            {
                _overflowMenu.Items.Add(new MenuSeparator());
            }
        }
    }

    private void ReleaseOverflowProjections(bool disposeFaces)
    {
        if (disposeFaces && !_overflowMenu.IsDisposed)
        {
            var faces = _overflowMenu.Items.ToArray();
            _overflowMenu.Items.Clear();

            foreach (var face in faces.OfType<MenuSeparator>())
            {
                face.Dispose();
            }
        }

        foreach (var projection in _projections)
        {
            if (disposeFaces)
            {
                projection.Dispose();
            }
            else
            {
                projection.Detach();
            }
        }

        _projections.Clear();
        _projectionBySource.Clear();
    }

    #endregion

    #region Input, popup, and lifecycle

    /// <summary>Focuses and selects the private trigger before its pointer behavior arms.</summary>
    /// <param name="eventArgs">The routed pointer candidate.</param>
    internal void FocusFromOverflowPointer(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (eventArgs is PointerEventArgs { Pointer.Action: PointerAction.Press })
        {
            _ = RequestFocus();
            SelectOverflowTarget();
        }
    }

    /// <summary>Toggles the current overflow popup from its retained trigger.</summary>
    /// <param name="cause">The validated activation path.</param>
    internal void ToggleOverflow(ActivationCause cause)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(cause);

        if (IsOverflowOpen)
        {
            _overflowCoordinator.SetOpen(false);
            return;
        }

        _ = OpenOverflow(null);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);

        if (eventArgs.IsHandled || eventArgs is not KeyEventArgs key)
        {
            return;
        }

        var stroke = key.Stroke;
        var movementEligible = KeyboardModifierPolicy.IsScalarNavigationEligible(stroke.Modifiers);

        if (key.IsInitialKeyDown && movementEligible && stroke.Code is Code.Left or Code.Right)
        {
            MoveSelection(stroke.Code == Code.Right ? 1 : -1);
            eventArgs.IsHandled = true;
            return;
        }

        if (key.IsInitialKeyDown && movementEligible && stroke.Code is Code.Home or Code.End)
        {
            SelectEndpoint(stroke.Code == Code.End);
            eventArgs.IsHandled = true;
            return;
        }

        if (key.IsInitialKeyDown && stroke.Code == Code.Enter && stroke.Modifiers.IsActivationEligible())
        {
            eventArgs.IsHandled = ActivateSelectedTarget();
            return;
        }

        HandleSpace(key);
    }

    private void MoveSelection(int direction)
    {
        CancelSpacePress();
        var targets = NavigationTargets();

        if (targets.Count == 0)
        {
            return;
        }

        var current = _overflowTargetSelected
            ? targets.IndexOf(_overflowButton)
            : _selectedItem is null ? -1 : targets.IndexOf(_selectedItem);
        var origin = current >= 0 ? current : direction < 0 ? targets.Count : -1;
        var next = (origin + direction + targets.Count) % targets.Count;
        SelectNavigationTarget(targets[next]);
    }

    private void SelectEndpoint(bool last)
    {
        CancelSpacePress();
        var targets = NavigationTargets();

        if (targets.Count > 0)
        {
            SelectNavigationTarget(targets[last ? targets.Count - 1 : 0]);
        }
    }

    private List<ControlBase> NavigationTargets()
    {
        var targets = Items
            .OfType<CommandBarItem>()
            .Where(item => _primaryEntries.Contains(item) && IsAvailableItem(item))
            .Cast<ControlBase>()
            .ToList();

        if (_overflowEntries.OfType<CommandBarItem>().Any())
        {
            targets.Add(_overflowButton);
        }

        return targets;
    }

    private void SelectNavigationTarget(ControlBase target)
    {
        if (target is CommandBarItem item)
        {
            Select(item);
        }
        else
        {
            SelectOverflowTarget();
        }
    }

    private bool ActivateSelectedTarget()
    {
        if (_overflowTargetSelected)
        {
            return OpenOverflow(null);
        }

        var item = _selectedItem;

        if (item is null)
        {
            return false;
        }

        if (!IsAvailableItem(item))
        {
            return false;
        }

        if (item.IsOverflowed)
        {
            return OpenOverflow(item);
        }

        item.InvokeFromProjection(ActivationCause.Keyboard);
        return true;
    }

    private void HandleSpace(KeyEventArgs key)
    {
        var stroke = key.Stroke;

        if (stroke.Code != Code.Character || stroke.Character != new Rune(' '))
        {
            return;
        }

        if (key.IsInitialKeyDown)
        {
            if (!stroke.Modifiers.IsActivationEligible())
            {
                return;
            }

            var target = SelectedTarget();

            if (target is null)
            {
                return;
            }

            key.IsHandled = true;
            _spacePressedTarget = target;
            SetTargetPressed(target, true);

            if (!Capabilities.KeyReleaseEvents.Authoritative)
            {
                SetTargetPressed(target, false);
                _spacePressedTarget = null;
                _ = ActivateSelectedTarget();
            }

            return;
        }

        if (key.IsKeyUp)
        {
            if (_spacePressedTarget is null)
            {
                key.IsHandled = stroke.Modifiers.IsActivationEligible();
                return;
            }

            key.IsHandled = true;
            var target = _spacePressedTarget;
            _spacePressedTarget = null;
            SetTargetPressed(target, false);

            if (ReferenceEquals(target, SelectedTarget()) && stroke.Modifiers.IsActivationEligible())
            {
                _ = ActivateSelectedTarget();
            }

            return;
        }

        key.IsHandled = _spacePressedTarget is not null;
    }

    [Pure]
    private ControlBase? SelectedTarget() => _overflowTargetSelected ? _overflowButton : _selectedItem;

    private static void SetTargetPressed(ControlBase target, bool value)
    {
        switch (target)
        {
            case CommandBarItem item:
                item.SetPressed(value);
                break;
            case CommandBarOverflowButton trigger:
                trigger.SetPressed(value);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private void CancelSpacePress()
    {
        if (_spacePressedTarget is { } target)
        {
            SetTargetPressed(target, false);
            _spacePressedTarget = null;
        }
    }

    private bool OpenOverflow(CommandBarItem? selected)
    {
        if (_projections.Count == 0 || _overflowPopup.IsDisposed)
        {
            return false;
        }

        CommandBarOverflowProjection? projection = null;

        if (selected is not null)
        {
            _ = _projectionBySource.TryGetValue(selected, out projection);
        }

        projection ??= _projections.FirstOrDefault(candidate => candidate.Item.EffectiveIsEnabled);

        if (projection is not null)
        {
            _overflowMenu.SelectedItem = projection.Item;
        }

        _overflowCoordinator.SetOpen(true);
        return true;
    }

    private void OnOverflowItemInvocationCompleted(object? sender, MenuItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (!IsDisposed && IsOverflowOpen)
        {
            _overflowCoordinator.AcceptAndClose();
        }
    }

    private void OnFocusEntered(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (_selectedItem is null && !_overflowTargetSelected)
        {
            var first = NavigationTargets().FirstOrDefault();

            if (first is not null)
            {
                SelectNavigationTarget(first);
            }
        }

        CommitSelectionPresentation(true);
    }

    private void OnFocusLeft(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        CancelSpacePress();
        CommitSelectionPresentation(false);
    }

    private void CommitSelectionPresentation(bool focused)
    {
        _selectedItem?.CommitSelection(focused && !_selectedItem.IsOverflowed);
        _overflowButton.CommitSelection(focused && _overflowTargetSelected);
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (sender is not CommandBarItem item)
        {
            return;
        }

        if (eventArgs.PropertyName is nameof(IsEnabled) or
            nameof(EffectiveIsEnabled) or
            nameof(Visibility) or
            nameof(EffectiveIsVisible))
        {
            if (ReferenceEquals(item, _selectedItem) && !IsAvailableItem(item))
            {
                SelectNearest(Math.Max(0, IndexOfItemControl(item)));
            }

            if (IsOverflowOpen && !item.EffectiveIsVisible)
            {
                _overflowCoordinator.SetOpen(false);
            }
        }
    }

    /// <inheritdoc/>
    private protected override void OnItemControlsChanged(OwnedControlChange change)
    {
        base.OnItemControlsChanged(change);

        if (change.Kind != OwnedControlMutationKind.DirectDisposal)
        {
            return;
        }

        foreach (var entry in change.Removed.Span)
        {
            ReleaseEntry(entry);
        }

        if (_selectedItem is { IsDisposing: true } or { IsDisposed: true } ||
            (_selectedItem is not null && IndexOfItemControl(_selectedItem) < 0))
        {
            SelectNearest(Math.Min(_selectedIndex, EntryCount - 1));
        }
        else if (_selectedItem is not null)
        {
            _selectedIndex = IndexOfItemControl(_selectedItem);
        }

        PublishEntriesChanged();
    }

    /// <inheritdoc/>
    private protected override void OnItemsControlDisposing()
    {
        _propertyOverrides.Dispose();
        base.OnItemsControlDisposing();
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        _overflowCoordinator.OnOwnerAttached();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        _availabilityGeneration++;
        ExceptionDispatchInfo? failure = null;
        CaptureFailure(CancelSpacePress, ref failure);
        CaptureFailure(_overflowButton.CancelPress, ref failure);
        CaptureFailure(() => _overflowCoordinator.OnOwnerUnavailable(reason), ref failure);
        CaptureFailure(() => base.OnUnavailable(reason), ref failure);

        if (reason == ReleaseReason.Disposed)
        {
            FocusEntered -= OnFocusEntered;
            FocusLeft -= OnFocusLeft;
            _overflowMenu.ItemInvocationCompleted -= OnOverflowItemInvocationCompleted;
            CaptureFailure(_overflowCoordinator.Detach, ref failure);
            CaptureFailure(() => ReleaseOverflowProjections(disposeFaces: false), ref failure);
            ItemInvoked = null;
        }

        failure?.Throw();
    }

    #endregion

    [Pure]
    private static ControlBase RequireEntry(ControlBase entry) =>
        entry is CommandBarItem or CommandBarSeparator
            ? entry
            : throw new InvalidOperationException(
                "Command bars may own only CommandBarItem and CommandBarSeparator controls through Items.");
}
