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
public sealed class ComboBox: PressInteractionBase
{
    // A field has one content row; its content reserves a separator gap plus the
    // indicator cell after the label. The border is not part of content: measure
    // resolution adds the border inset on top of the content size returned here.
    private const int _fieldContentHeight = 1;
    private const int _indicatorReservedWidth = 2;
    private const int _indicatorWidth = 1;
    private const int _popupConnectingFrameHeight = 1;
    private const int _defaultDropDownHeight = 8;
    private readonly ListView _list;
    private readonly Popup _popup;
    private readonly OwnedControlSlot _popupSlot;
    private readonly PopupModalTracker _modalTracker;
    private readonly StyleSlot<ScrollBarStyle> _scrollBarStyle;
    private string _typeAhead = string.Empty;
    private int _selectedIndex = -1;
    private bool _synchronizingSelection;
    private bool _listSelectionChangedFired;

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
            ConnectsToAnchor = true,
            // ComboBox re-arranges its own popup child from its own ArrangeOverride every pass
            // (RootBounds(bounds) above), so base Popup's anchor-reflow tracking would be a
            // redundant second placement pass reacting to the same self-owned anchor.
            TracksAnchorReflow = false
        };
        _popup.Opened += OnPopupOpened;
        _popup.Closing += OnPopupClosing;
        _popup.Closed += OnPopupClosed;
        _modalTracker = new PopupModalTracker(_popup, () => Opened = false);
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

    /// <summary>Gets or sets whether the private drop-down owns a dismissing modal plane rooted at this field.</summary>
    /// <exception cref="ArgumentException">The attached ComboBox is not an eligible modal root.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    /// <exception cref="Exception">A focus, scope, pointer-cleanup, or user callback fails after committed cleanup.</exception>
    public bool Opened
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
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = MeasureChild(_popup, new Constraint(constraint.Width, DropDownHeight.Add(_popupConnectingFrameHeight)));
        var width = MeasureCells(SelectedText()).Add(_indicatorReservedWidth);
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
        var label = canvas.Clip(new Rect(content.X, content.Y, Math.Max(0, content.Width - _indicatorReservedWidth), _fieldContentHeight));
        _ = label.Draw(
            SelectedText().AsSpan(),
            new Point(content.X, content.Y),
            style,
            background: BackgroundMode.Transparent);
        var themed = ControlGlyphs.Disclosure.DropDown;
        var glyph = ResolveDropDownGlyph(themed.Fallback);
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

        if (Opened && eventArgs is KeyEventArgs { Stroke: { Action: KeyAction.Press } stroke })
        {
            if (stroke.Code == Code.Escape)
            {
                Opened = false;
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
                Opened = false;
                return;
            }
        }

        Interaction.Handle(eventArgs);

        if (!eventArgs.Handled && eventArgs is KeyEventArgs { Stroke: { Action: KeyAction.Press } key }
            && key.Code is Code.Delete or Code.Backspace)
        {
            eventArgs.Handled = ClearSelection();
        }
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

        if (reason == ReleaseReason.Disposed)
        {
            _list.ItemInvoked -= OnItemInvoked;
            _list.SelectionChanged -= OnSelectionChanged;
            _popup.Opened -= OnPopupOpened;
            _popup.Closing -= OnPopupClosing;
            _popup.Closed -= OnPopupClosed;
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
        Opened = !Opened;
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
        Opened = false;
    }

    private void OnSelectionChanged(object? sender, ListSelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _selectedIndex = _list.SelectedIndex;
        _listSelectionChangedFired = true;

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

    private void OnPopupOpened(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        NotifyPropertyChanged(nameof(Opened), InvalidationImpact.None);
    }

    private void OnPopupClosing(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        _typeAhead = string.Empty;

        if (ContainsFocused(_list))
        {
            _ = RequestFocus();
        }
    }

    private void OnPopupClosed(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        NotifyPropertyChanged(nameof(Opened), InvalidationImpact.None);
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
        _listSelectionChangedFired = false;

        try
        {
            _list.SelectedIndex = value;
        }
        catch
        {
            _selectedIndex = previous;
            throw;
        }

        if (_listSelectionChangedFired)
        {
            // The list's own SelectionChanged fired synchronously from inside the assignment
            // above, publishing this change through OnSelectionChanged.
            return;
        }

        if (_list.SelectedIndex != value && Opened)
        {
            // ListView.SelectedIndex's own availability check factors in ancestor
            // visibility, so a rejection is only a genuine veto (an unavailable item, or a
            // SelectionChanging handler cancelling it) while the drop-down's items are
            // actually visible. Roll back instead of reporting a selection the drop-down
            // never adopted.
            _selectedIndex = previous;
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

    /// <summary>Resolves the disclosure chevron from the active theme's input style.</summary>
    /// <remarks>
    /// The chevron lives on <see cref="InputStyle"/> rather than on this control, so a theme
    /// targeting a terminal without dependable arrow coverage replaces it for every drop-down
    /// input at once instead of per instance. This control resolves appearance through
    /// <c>AppearanceStates</c>, which drops non-appearance members, so the style set is read
    /// directly.
    /// </remarks>
    /// <param name="fallback">The code-owned narrow-policy fallback.</param>
    /// <returns>The glyph to draw.</returns>
    private Rune ResolveDropDownGlyph(Rune fallback) =>
        (Theme ?? ThemeCatalog.Dark)
        .GetStyleSet(InputStyle.Default)
        .Normal.DropDownGlyph.Resolve(fallback, CellPolicy.AmbiguousWidth);

}
