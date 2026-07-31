// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

using Collections;

using Popups;

using Scrolling;

using SharpVision.Terminal.Input;

/// <summary>Displays one selected value and composes a private popup list for choosing another.</summary>
[PublicAPI]
public sealed class ComboBox: Control
{
    // A field has one content row, two border columns, and one indicator cell.
    private const int _fieldContentHeight = 1;
    private const int _fieldBorderWidth = 2;
    private const int _indicatorWidth = 1;
    private const int _popupConnectingFrameHeight = 1;
    private const int _defaultDropDownHeight = 8;
    private readonly ListView _list;
    private readonly Popup _popup;
    private readonly OwnedControlSlot _popupSlot;
    private readonly PressBehavior _interaction;
    private readonly PopupModalTracker _modalTracker;
    private Rune? _dropDownGlyph;
    private string _typeAhead = string.Empty;
    private int _selectedIndex = -1;
    private bool _synchronizingSelection;

    #region Construction and properties

    /// <summary>Initializes an empty combo box with a light field border and a connected private popup.</summary>
    public ComboBox()
    {
        _list = new ListView
        {
            SelectionMode = ListSelectionMode.Single,
            TabStop = false
        };
        _list.ItemInvoked += OnItemInvoked;
        _list.SelectionChanged += OnSelectionChanged;
        _popup = new Popup
        {
            Anchor = this,
            Content = _list,
            FocusOnOpen = false,
            ModalBehavior = PopupModalBehavior.None,
            TabNavigation = TabNavigation.None,
            ConnectsToAnchor = true
        };
        _popup.Closing += OnPopupClosing;
        _popup.Closed += OnPopupClosed;
        _modalTracker = new PopupModalTracker(_popup, () => IsOpen = false);
        _popupSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Popup,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "drop-down",
                InvalidationImpact.Measure),
            capacity: 1);
        _popupSlot.Add(_popup);
        _interaction = new PressBehavior(
            () => Bounds,
            () => EffectiveIsEnabled && EffectiveIsVisible,
            () => FocusOwner is null || IsFocused,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed,
            Activate);
        Focusable = true;
        TabStop = true;
        TabNavigation = TabNavigation.None;
    }

    /// <inheritdoc/>
    protected override ThemeRole ThemeRole => ThemeRole.Input;

    /// <summary>Raised after a selected index commits through direct assignment or the drop-down list.</summary>
    public event EventHandler<ListSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised after the drop-down list opens.</summary>
    public event EventHandler? DropDownOpened;

    /// <summary>Raised after the drop-down list closes.</summary>
    public event EventHandler? DropDownClosed;

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
            _list.Items = value;

            if (_selectedIndex < 0 && value.Count > 0)
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

    /// <summary>Gets or sets the local one-cell drop-down indicator.</summary>
    /// <exception cref="ArgumentException">The value is a control or does not occupy exactly one cell.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public Rune DropDownGlyph
    {
        get => _dropDownGlyph ?? ControlGlyphs.Disclosure.DropDown.Value;
        set
        {
            _ = new ControlGlyph(value, value);
            VerifyMutable();

            if (_dropDownGlyph == value)
            {
                return;
            }

            _dropDownGlyph = value;
            NotifyPropertyChanged(nameof(DropDownGlyph), InvalidationImpact.Render);
        }
    }

    /// <summary>Clears the local drop-down indicator to the code-owned default.</summary>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public void ResetDropDownGlyph()
    {
        VerifyMutable();

        if (_dropDownGlyph.HasValue)
        {
            _dropDownGlyph = null;
            NotifyPropertyChanged(nameof(DropDownGlyph), InvalidationImpact.Render);
        }
    }

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
        get => _list.ScrollBarStyle;
        set
        {
            VerifyMutable();

            if (_list.ScrollBarStyle == value)
            {
                return;
            }

            var previous = ActualScrollBarStyle;
            _list.ScrollBarStyle = value;
            var current = ActualScrollBarStyle;
            NotifyPropertyChanged(nameof(ScrollBarStyle), InvalidationImpact.None);

            if (previous != current)
            {
                NotifyPropertyChanged(nameof(ActualScrollBarStyle), InvalidationImpact.None);
            }
        }
    }

    /// <summary>Gets the resolved drop-down scrollbar style.</summary>
    public ScrollBarStyle ActualScrollBarStyle => ScrollBar.ResolveStyle(ScrollBarStyle, Theme);

    /// <summary>Gets or sets whether the private drop-down owns a dismissing modal plane rooted at this field.</summary>
    /// <exception cref="ArgumentException">The attached ComboBox is not an eligible modal root.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    /// <exception cref="Exception">A focus, scope, pointer-cleanup, or user callback fails after committed cleanup.</exception>
    public bool IsOpen
    {
        get => _popup.IsOpen;
        set
        {
            VerifyMutable();

            if (_popup.IsOpen != value)
            {
                if (value)
                {
                    OpenDropDown();
                }
                else
                {
                    CloseDropDown();
                }
            }
        }
    }

    #endregion

    #region Input, layout, and rendering

    /// <inheritdoc/>
    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = MeasureChild(_popup, new Constraint(constraint.Width, LayoutMath.Add(DropDownHeight, _popupConnectingFrameHeight)));
        var width = LayoutMath.Add(Terminal.Unicode.Width.Measure(SelectedText()).Cells, _fieldBorderWidth);
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
        var label = canvas.Clip(new Rect(content.X, content.Y, Math.Max(0, content.Width - _fieldBorderWidth), _fieldContentHeight));
        _ = label.Draw(
            SelectedText().AsSpan(),
            new Point(content.X, content.Y),
            style,
            background: BackgroundMode.Transparent);
        var themed = ControlGlyphs.Disclosure.DropDown;
        var glyph = CellGlyphResolver.Resolve(DropDownGlyph, themed.Fallback, CellPolicy.AmbiguousWidth);
        canvas.DrawRune(
            glyph,
            new Point(Math.Max(content.X, content.Right - _indicatorWidth), content.Y),
            style,
            BackgroundMode.Transparent);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);

        if (eventArgs.Handled)
        {
            return;
        }

        if (IsOpen && eventArgs is KeyEventArgs { Stroke: { Action: KeyAction.Press } stroke })
        {
            if (stroke.Code == Code.Escape)
            {
                IsOpen = false;
                eventArgs.Handled = true;
                return;
            }

            if (stroke.Code == Code.Enter)
            {
                eventArgs.Handled = _list.ActivateCurrent(ActivationCause.Keyboard, Code.Enter, Modifiers.None);
                return;
            }

            if (stroke.Code is Code.Delete or Code.Backspace)
            {
                eventArgs.Handled = ClearSelection();
                return;
            }

            if (stroke.Code == Code.Character && stroke.Character is { } character)
            {
                eventArgs.Handled = SelectTypeAhead(character);
                return;
            }

            if (stroke.Code != Code.Tab && _list.MoveCurrent(stroke.Code))
            {
                eventArgs.Handled = true;
                return;
            }

            if (stroke.Code == Code.Tab)
            {
                IsOpen = false;
                return;
            }
        }

        _interaction.Handle(eventArgs);

        if (!eventArgs.Handled && eventArgs is KeyEventArgs { Stroke: { Action: KeyAction.Press } key }
            && key.Code is Code.Delete or Code.Backspace)
        {
            eventArgs.Handled = ClearSelection();
        }
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);
        _interaction.FocusChanged(focused);
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        _interaction.CaptureLost();
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();

        if (_popup.IsOpen)
        {
            _modalTracker.Enter(this);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        _interaction.Unavailable();

        if (reason == ReleaseReason.Disposed)
        {
            _list.ItemInvoked -= OnItemInvoked;
            _list.SelectionChanged -= OnSelectionChanged;
            _popup.Closing -= OnPopupClosing;
            _popup.Closed -= OnPopupClosed;
            SelectionChanged = null;
            DropDownOpened = null;
            DropDownClosed = null;
        }
    }

    #endregion

    #region Drop-down coordination

    private void Activate(ActivationCause cause)
    {
        _ = cause;
        IsOpen = !IsOpen;
    }

    private void OpenDropDown()
    {
        _popup.IsOpen = true;
        _modalTracker.Enter(this);
        DropDownOpened?.Invoke(this, EventArgs.Empty);
    }

    private void CloseDropDown()
    {
        _modalTracker.Exit();
        _popup.IsOpen = false;
        DropDownClosed?.Invoke(this, EventArgs.Empty);
    }

    private void OnItemInvoked(object? sender, ItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        _list.SelectedIndex = eventArgs.Index;
        IsOpen = false;
    }

    private void OnSelectionChanged(object? sender, ListSelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _selectedIndex = _list.SelectedIndex;

        if (_synchronizingSelection)
        {
            return;
        }

        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(
            () => NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure),
            ref failure);
        ExceptionAggregation.Capture(() => SelectionChanged?.Invoke(this, eventArgs), ref failure);
        failure?.Throw();
    }

    private void OnPopupClosing(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (ContainsFocused(_list))
        {
            _ = RequestFocus();
        }
    }

    private void OnPopupClosed(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        NotifyPropertyChanged(nameof(IsOpen), InvalidationImpact.None);
    }

    #endregion

    #region Geometry

    private string SelectedText()
    {
        var index = _selectedIndex;

        return index < 0 || index >= _list.Items.Count
            ? Placeholder
            : ItemText(_list.Items[index]);
    }

    private string ItemText(object? item) =>
        TextSelector?.Invoke(item) ?? Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty;

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

        try
        {
            _list.SelectedIndex = value;
        }
        catch
        {
            _selectedIndex = previous;
            throw;
        }

        if (_list.SelectedIndex == value)
        {
            // The list's own SelectionChanged already fired synchronously from inside the
            // assignment above, publishing this change through OnSelectionChanged.
            return;
        }

        if (IsOpen)
        {
            // ListView.SelectedIndex's own availability check factors in ancestor
            // visibility, so a rejection is only a genuine veto (an unavailable item, or a
            // SelectionChanging handler cancelling it) while the drop-down's items are
            // actually visible. Roll back instead of reporting a selection the drop-down
            // never adopted.
            _selectedIndex = previous;
            return;
        }

        // The drop-down is closed, so every item is effectively invisible and the list
        // always silently rejects the assignment regardless of real availability — the
        // list's own notification never fires in that case, so publish explicitly here.
        PublishSelectionChanged(value, previous);
    }

    private void PublishSelectionChanged(int selectedIndex, int previousIndex)
    {
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(
            () => NotifyPropertyChanged(nameof(SelectedIndex), InvalidationImpact.Measure),
            ref failure);

        int[] added = selectedIndex >= 0 ? [selectedIndex] : [];
        int[] removed = previousIndex >= 0 ? [previousIndex] : [];
        var selectionChanged = new ListSelectionChangedEventArgs(added, removed);
        ExceptionAggregation.Capture(
            () => SelectionChanged?.Invoke(this, selectionChanged),
            ref failure);
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
        if (char.IsControl((char) character.Value) || Items.Count == 0)
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
