// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.ComponentModel;
using System.Runtime.ExceptionServices;

using Collections;

using Popups;

using Scrolling;

using SharpVision.Terminal.Input;

/// <summary>Displays one selected value and composes a private popup list for choosing another.</summary>
[PublicAPI]
public sealed class ComboBox: InputBase
{
    // A field has one content row; its content reserves a separator gap plus the
    // indicator cell after the label. The border is not part of content: measure
    // resolution adds the border inset on top of the content size returned here.
    private const int _fieldContentHeight = 1;
    private const int _indicatorReservedWidth = 2;
    private const int _popupConnectingFrameHeight = 1;
    private const int _defaultDropDownHeight = 8;
    private readonly ListView _list;
    private readonly Popup _popup;
    private readonly StyleSlot<ScrollBarStyle> _scrollBarStyle;
    private string _typeAhead = string.Empty;
    private int _selectedIndex = -1;
    private long _selectionVersion;
    private bool _synchronizingSelection;
    private bool _listSelectionChangedFired;
    private int _openingSelectedIndex = -1;
    private int _openingCurrentIndex = -1;
    private long _openingItemsVersion;
    private long _openingSelectionVersion;
    private long _itemsVersion;
    private PopupItemActivationIdentity? _pointerActivation;

    #region Construction and properties

    /// <summary>Initializes an empty combo box with a light field border and a connected private popup.</summary>
    public ComboBox()
    {
        _list = new ListView
        {
            SelectionMode = ListSelectionMode.Single,
            IsTabStop = false
        };
        _list.ItemActivationStarting += OnItemActivationStarting;
        _list.ItemInvoked += OnItemInvoked;
        _list.SelectionChanged += OnSelectionChanged;
        _list.PropertyChanged += OnListPropertyChanged;
        _popup = EnablePopupNavigationSession(
            _list,
            focusOnOpen: false,
            beforeCloseFocusRestore: () => _typeAhead = string.Empty,
            beginSession: BeginNavigationSession,
            handleNavigationKey: HandleNavigationKey,
            cancelSession: CancelNavigationSession,
            acceptSession: AcceptNavigationSession);
        EnablePressActivation();
        _scrollBarStyle = InitializePartStyle(
            ScrollBarStyle.ForwardingDefinition,
            nameof(ScrollBarStyle));
        BindStyle(_scrollBarStyle, _list, nameof(ScrollBarStyle));
        TabNavigation = TabNavigation.None;
    }

    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetStyleSet(InputStyle.Default).ToAppearanceStates();

    /// <summary>Raised after a selected index commits through direct assignment or the drop-down list.</summary>
    public event EventHandler<ListSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised after the drop-down list opens.</summary>
    public event EventHandler? DropDownOpened;

    /// <summary>Raised after the drop-down list closes.</summary>
    public event EventHandler? DropDownClosed;

    /// <summary>Gets the private drop-down list, exposed for the incremental data-binding path.</summary>
    internal ListView GetDropDownList() => _list;

    /// <summary>Gets or sets a copied list of choices displayed by the drop-down.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">A list template cannot realize the supplied values.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public IReadOnlyList<object?> Items
    {
        get => _list.Items;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();

            // Captured before forwarding so the auto-select-0 fallback only applies when
            // nothing was selected coming in — not when this very reassignment is what just
            // dropped a real selection below the new Items.Count. The list's own
            // remap already retains an in-range selection, so _selectedIndex reflects the
            // correct post-remap value once the forward returns.
            var wasUnselected = _selectedIndex < 0;
            _list.Items = value;

            if (wasUnselected && value.Count > 0)
            {
                SetSelectedIndex(0);
            }
            else if (_selectedIndex >= value.Count)
            {
                SetSelectedIndex(-1);
            }

            NotifyPropertyChanged(nameof(Items), InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets the selected index, or -1 when no value is selected.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the item range.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetSelectedIndex(value);
    }

    /// <summary>Gets or sets the non-null detached-control factory that realizes each drop-down row.</summary>
    /// <remarks>Delegates directly to the private drop-down ListView's own ItemTemplate contract.</remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">Candidate output is null, disposed, attached, or duplicated.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public ItemTemplate ItemTemplate
    {
        get => _list.ItemTemplate;
        set => _list.ItemTemplate = value;
    }

    /// <summary>Gets or sets the optional projection from an item to its closed-field and
    /// type-ahead text, or null to fall back to <see cref="Convert.ToString(object?, IFormatProvider?)"/>
    /// under the invariant culture.</summary>
    /// <remarks>
    /// One textual projection drives both the closed field and type-ahead matching, so they cannot
    /// drift from each other or from a separately assigned <see cref="ItemTemplate"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public Func<object?, string>? TextSelector
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets whether Delete and Backspace may clear the selection.</summary>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public bool AllowNull
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    } = true;

    /// <summary>Gets or sets the text shown when no item is selected.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public string Placeholder
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = "Select…";

    /// <summary>Gets the selected item, or null when no value is selected.</summary>
    public object? SelectedItem
    {
        get => SelectedIndex < 0 ? null : Items[SelectedIndex];
        set
        {
            if (value is null)
            {
                SelectedIndex = -1;
                return;
            }

            for (var index = 0; index < Items.Count; index++)
            {
                if (Equals(Items[index], value))
                {
                    SelectedIndex = index;
                    return;
                }
            }

            SelectedIndex = -1;
        }
    }

    /// <summary>Gets or sets the maximum visible list height in terminal cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public int DropDownHeight
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = _defaultDropDownHeight;

    /// <summary>Gets or sets the axes available to the owned drop-down overflow host.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public ScrollBars ScrollBars
    {
        get => _list.ScrollBars;
        set
        {
            VerifyMutable();

            if (_list.ScrollBars == value)
            {
                return;
            }

            _list.ScrollBars = value;
            NotifyPropertyChanged(nameof(ScrollBars), InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets the drop-down scrollbar reservation policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get => _list.ShowScrollBars;
        set
        {
            VerifyMutable();

            if (_list.ShowScrollBars == value)
            {
                return;
            }

            _list.ShowScrollBars = value;
            NotifyPropertyChanged(nameof(ShowScrollBars), InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets the complete local style of owned drop-down rails.</summary>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public ScrollBarStyle? ScrollBarStyle
    {
        get => _scrollBarStyle.Local;
        set => _scrollBarStyle.Local = value;
    }

    /// <summary>Gets the resolved drop-down scrollbar style.</summary>
    public ScrollBarStyle ActualScrollBarStyle => _scrollBarStyle.Actual;

    /// <summary>Gets or sets the owned drop-down popup's border and shadow together.</summary>
    /// <remarks>
    /// A component left null keeps the popup on its own <see cref="PopupChrome"/> role
    /// appearance for that part.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public PopupChrome PopupChrome
    {
        get => _popup.Style;
        set
        {
            VerifyMutable();

            if (_popup.Style == value)
            {
                return;
            }

            _popup.Style = value;
            NotifyPropertyChanged(nameof(PopupChrome), InvalidationImpact.None);
        }
    }

    /// <summary>Returns the drop-down popup's border and shadow to <see cref="PopupChrome"/> ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public void ResetPopupChrome() => PopupChrome = default;

    /// <summary>Gets or sets the fixed row height of every owned drop-down item, or null to size each
    /// row to its content.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public int? RowHeight
    {
        get => _list.RowHeight;
        set
        {
            VerifyMutable();

            if (_list.RowHeight == value)
            {
                return;
            }

            _list.RowHeight = value;
            NotifyPropertyChanged(nameof(RowHeight), InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets the optional leading edge-pinned decoration, reserved inside the
    /// field box and strictly inboard of the drop-down indicator.</summary>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public Affix? StartAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <summary>Gets or sets the optional trailing edge-pinned decoration, reserved inside the
    /// field box and strictly inboard of the drop-down indicator.</summary>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public Affix? EndAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    #endregion

    #region Input, layout, and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = MeasureChild(_popup, new Constraint(constraint.Width, DropDownHeight.Add(_popupConnectingFrameHeight)));
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        var width = MeasureCells(SelectedItemText())
            .Add(_indicatorReservedWidth)
            .Add(affixes.StartCells)
            .Add(affixes.EndCells);
        return new Size(width, _fieldContentHeight);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        ArrangeChild(_popup, RootBounds(bounds), ResolvedAxes.Both);

        if (_selectedIndex >= 0 && _list.SelectedIndex != _selectedIndex)
        {
            _synchronizingSelection = true;

            try
            {
                _list.SelectedIndex = _selectedIndex;
            }
            finally
            {
                _synchronizingSelection = false;
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var content = ContentBounds;
        var style = ResolvedStyle;
        // The field box excludes the indicator's own reserved width (the ▼ cell plus its
        // separating gap) so affixes deflated from it can never draw over the indicator.
        var fieldBox = new Rect(content.X, content.Y, Math.Max(0, content.Width - _indicatorReservedWidth), _fieldContentHeight);
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        RenderAffixes(canvas, fieldBox, affixes, StartAffix, EndAffix, style);
        var labelBox = DeflateForAffixes(fieldBox, affixes);
        var label = canvas.Clip(labelBox);
        _ = label.Draw(
            SelectedItemText().AsSpan(),
            new Point(labelBox.X, labelBox.Y),
            style,
            background: BackgroundMode.Transparent);
        DrawDropDownIndicator(canvas, content, style);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);

        if (eventArgs.IsHandled)
        {
            return;
        }

        if (IsOpen && eventArgs is KeyEventArgs keyEventArgs)
        {
            var stroke = keyEventArgs.Stroke;

            if (keyEventArgs.IsKeyDown && stroke.Code is Code.Delete or Code.Backspace)
            {
                eventArgs.IsHandled = ClearSelection();
                return;
            }

            if (keyEventArgs.IsKeyDown &&
                stroke.Code == Code.Character &&
                stroke.Character is { } character &&
                KeyboardModifierPolicy.IsTextEntryEligible(stroke.Modifiers))
            {
                eventArgs.IsHandled = SelectTypeAhead(character);
                return;
            }

            if (keyEventArgs.IsInitialKeyDown &&
                stroke.Code == Code.Tab &&
                KeyboardModifierPolicy.IsTabTraversalEligible(stroke.Modifiers))
            {
                IsOpen = false;
                return;
            }
        }

        HandlePressActivation(eventArgs);

        if (!eventArgs.IsHandled && eventArgs is KeyEventArgs { IsKeyDown: true } keyEvent
            && keyEvent.Stroke.Code is Code.Delete or Code.Backspace)
        {
            eventArgs.IsHandled = ClearSelection();
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            _list.ItemActivationStarting -= OnItemActivationStarting;
            _list.ItemInvoked -= OnItemInvoked;
            _list.SelectionChanged -= OnSelectionChanged;
            _list.PropertyChanged -= OnListPropertyChanged;
            SelectionChanged = null;
            DropDownOpened = null;
            DropDownClosed = null;
        }
    }

    #endregion

    #region Drop-down coordination

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        _ = cause;
        IsOpen = !IsOpen;
    }

    /// <inheritdoc/>
    protected override void OnDropDownOpened()
    {
        SynchronizeListSelection(_selectedIndex);
        _list.SetProvisionalCurrentIndex(_list.ActiveIndex);
        DropDownOpened?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    protected override void OnDropDownClosed() => DropDownClosed?.Invoke(this, EventArgs.Empty);

    private void BeginNavigationSession()
    {
        _openingSelectedIndex = _selectedIndex;
        _openingCurrentIndex = _selectedIndex >= 0 ? _selectedIndex : _list.ActiveIndex;
        _openingItemsVersion = _itemsVersion;
        _openingSelectionVersion = _selectionVersion;
        _list.SetProvisionalCurrentIndex(_selectedIndex);
    }

    private bool HandleNavigationKey(KeyEventArgs eventArgs)
    {
        var stroke = eventArgs.Stroke;

        if (eventArgs.IsInitialKeyDown &&
            stroke.Code == Code.Escape &&
            stroke.Modifiers.IsActivationEligible())
        {
            eventArgs.IsHandled = true;
            IsOpen = false;
            return true;
        }

        if (eventArgs.IsInitialKeyDown && stroke.Code == Code.Enter)
        {
            // Enter belongs to the open session even when no item is available. Consuming it here
            // prevents the field's ordinary press activation from toggling the popup closed.
            eventArgs.IsHandled = true;
            var accepted = _list.ActivateCurrent(ActivationCause.Keyboard, Code.Enter, stroke.Modifiers);
            return accepted;
        }

        return _list.HandleCurrentNavigationKey(eventArgs);
    }

    private void CancelNavigationSession()
    {
        var openingSnapshotIsCurrent =
            _itemsVersion == _openingItemsVersion &&
            _selectionVersion == _openingSelectionVersion &&
            IsValidNavigationIndex(_openingSelectedIndex) &&
            IsValidNavigationIndex(_openingCurrentIndex);

        if (!openingSnapshotIsCurrent)
        {
            SynchronizeListSelection(_selectedIndex);
            _list.SetProvisionalCurrentIndex(_selectedIndex);
            return;
        }

        if (_selectedIndex != _openingSelectedIndex)
        {
            SetSelectedIndex(_openingSelectedIndex);
        }
        else
        {
            SynchronizeListSelection(_openingSelectedIndex);
        }

        _list.SetProvisionalCurrentIndex(_openingCurrentIndex);
    }

    private bool IsValidNavigationIndex(int index) =>
        index == -1 || (uint) index < (uint) Items.Count;

    private void AcceptNavigationSession()
    {
        var acceptedIndex = _list.ActiveIndex;

        if (acceptedIndex < 0)
        {
            return;
        }

        var popupVersion = PopupTransitionVersion;

        if (_selectedIndex == acceptedIndex)
        {
            SynchronizeListSelection(acceptedIndex);
        }
        else
        {
            SetSelectedIndex(acceptedIndex);
        }

        // Selection callbacks own a newer selection decision. Reopening establishes a new session
        // so the accepted session's close continuation cannot dismiss that newer state.
        if (!IsDisposed &&
            IsOpen &&
            PopupTransitionVersion == popupVersion &&
            _selectedIndex != acceptedIndex)
        {
            IsOpen = false;
            IsOpen = true;
        }
    }

    private void OnItemInvoked(object? sender, ItemInvokedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Cause == ActivationCause.Pointer)
        {
            var activation = _pointerActivation;
            _pointerActivation = null;
            var isCurrentInvocation =
                activation is { } identity &&
                !IsDisposed &&
                IsOpen &&
                eventArgs.ActivationGeneration == identity.ItemGeneration &&
                eventArgs.Index == identity.ItemIndex &&
                eventArgs.Index == _selectedIndex &&
                eventArgs.Index == _list.SelectedIndex &&
                PopupTransitionVersion == identity.PopupTransitionVersion &&
                PopupSessionGeneration == identity.PopupSessionGeneration;

            if (!isCurrentInvocation)
            {
                return;
            }
        }

        AcceptPopupAndClose();
    }

    private void OnItemActivationStarting(object? sender, ItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        _pointerActivation = eventArgs.Cause == ActivationCause.Pointer && !IsDisposed && IsOpen
            ? new PopupItemActivationIdentity(
                eventArgs.ActivationGeneration,
                eventArgs.Index,
                PopupTransitionVersion,
                PopupSessionGeneration)
            : null;
    }

    private void OnSelectionChanged(object? sender, ListSelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _listSelectionChangedFired = true;

        if (_synchronizingSelection)
        {
            return;
        }

        var selectedIndex = _list.SelectedIndex;

        if (_selectedIndex != selectedIndex)
        {
            _selectedIndex = selectedIndex;
            _selectionVersion++;
        }

        if ((selectedIndex >= 0 && !eventArgs.AddedIndexes.Span.Contains(selectedIndex)) ||
            (selectedIndex < 0 && !eventArgs.AddedIndexes.IsEmpty))
        {
            return;
        }

        var version = _selectionVersion;
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(
            () => NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure),
            ref failure);

        if (_selectionVersion == version && _selectedIndex == selectedIndex)
        {
            ExceptionAggregation.Capture(() => SelectionChanged?.Invoke(this, eventArgs), ref failure);
        }

        failure?.Throw();
    }

    private void OnListPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName == nameof(ListView.Items))
        {
            _itemsVersion++;
            return;
        }

        if (_synchronizingSelection ||
            eventArgs.PropertyName != nameof(ListView.SelectedIndex) ||
            _list.SelectedIndex == _selectedIndex)
        {
            return;
        }

        _selectedIndex = _list.SelectedIndex;
        _selectionVersion++;
        NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure);
    }

    #endregion

    #region Geometry

    private string SelectedItemText()
    {
        var index = _selectedIndex;

        return index < 0 || index >= _list.Items.Count
            ? Placeholder
            : ItemText(_list.Items[index]);
    }

    private string ItemText(object? item) =>
        TextSelector?.Invoke(item) ?? Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty;

    private void SynchronizeListSelection(int value)
    {
        _synchronizingSelection = true;

        try
        {
            _list.SelectedIndex = value;
        }
        finally
        {
            _synchronizingSelection = false;
        }
    }

    private void SetSelectedIndex(int value)
    {
        if (value < -1 || value >= Items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "SelectedIndex is outside Items.");
        }

        VerifyMutable();

        if (_selectedIndex == value)
        {
            return;
        }

        var previous = _selectedIndex;
        _selectedIndex = value;
        _selectionVersion++;
        _listSelectionChangedFired = false;

        try
        {
            _list.SelectedIndex = value;
        }
        catch
        {
            _selectedIndex = previous;
            _selectionVersion++;

            throw;
        }

        if (_listSelectionChangedFired)
        {
            // The list's own SelectionChanged fired synchronously from inside the assignment
            // above, publishing this change through OnSelectionChanged.
            return;
        }

        if (_list.SelectedIndex != value && IsOpen)
        {
            // ListView.SelectedIndex's own availability check factors in ancestor
            // visibility, so a rejection is only a genuine veto (an unavailable item, or a
            // SelectionChanging handler cancelling it) while the drop-down's items are
            // actually visible. Roll back instead of reporting a selection the drop-down
            // never adopted.
            _selectedIndex = previous;
            _selectionVersion++;
            return;
        }

        // Either the drop-down is closed, so every item is effectively invisible and the
        // list always silently rejects the assignment regardless of real availability — or
        // the assignment landed on a value the list was already reporting (for example both
        // sides already at -1), which is a genuine no-op inside the list and therefore never
        // fires SelectionChanged at all. Either way this combo box's own value still changed
        // and nothing else will publish it, so publish explicitly here.
        PublishSelectionChanged(value, previous);
    }

    private void PublishSelectionChanged(int selectedIndex, int previousIndex)
    {
        var version = _selectionVersion;
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(
            () => NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure),
            ref failure);

        if (_selectionVersion == version && _selectedIndex == selectedIndex)
        {
            int[] added = selectedIndex >= 0 ? [selectedIndex] : [];
            int[] removed = previousIndex >= 0 ? [previousIndex] : [];
            var selectionChanged = new ListSelectionChangedEventArgs(added, removed);
            ExceptionAggregation.Capture(
                () => SelectionChanged?.Invoke(this, selectionChanged),
                ref failure);
        }

        failure?.Throw();
    }

    private bool ClearSelection()
    {
        if (!AllowNull || SelectedIndex < 0)
        {
            return false;
        }

        SelectedIndex = -1;
        _typeAhead = string.Empty;
        return true;
    }

    private bool SelectTypeAhead(Rune character)
    {
        if (Rune.GetUnicodeCategory(character) == UnicodeCategory.Control || Items.Count == 0)
        {
            return false;
        }

        _typeAhead += character.ToString();
        var start = Math.Max(0, SelectedIndex + 1);
        var match = FindTypeAhead(_typeAhead, start);

        if (match < 0 && _typeAhead.Length > 1)
        {
            _typeAhead = character.ToString();
            match = FindTypeAhead(_typeAhead, start);
        }

        if (match < 0)
        {
            _typeAhead = string.Empty;
            return false;
        }

        SelectedIndex = match;
        return true;
    }

    private int FindTypeAhead(string prefix, int start)
    {
        for (var offset = 0; offset < Items.Count; offset++)
        {
            var index = (start + offset) % Items.Count;
            var text = ItemText(Items[index]);
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    #endregion

}
