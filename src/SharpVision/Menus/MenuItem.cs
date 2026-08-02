// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

using System.Runtime.ExceptionServices;

using Popups;

/// <summary>Defines one focusable command, check, or radio entry in a <see cref="Menu"/>.</summary>
[PublicAPI]
public sealed class MenuItem: Pressable
{
    private const int _shortcutGap = 2;
    private readonly OwnedControlSlot _submenuSlot;
    private bool _isChecked;
    private bool _submenuOpenedFromPointerSelection;
    private int _checkedVersion;
    private Menu? _submenu;
    private Menu? _submenuCloseOwner;
    private Popup? _submenuPopup;
    private Rune? _uncheckedGlyph;
    private Rune? _checkedGlyph;
    private string? _shortcutTextValue;
    private string? _derivedShortcutText;

    /// <summary>Initializes an ordinary command item with no content.</summary>
    public MenuItem()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Height = Length.Cells(1);
        _submenuSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Popup,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "submenu",
                InvalidationImpact.None),
            capacity: 1);
        PointerExited += OnPointerExited;
    }

    /// <summary>Raised after an eligible item commits its optional check state and activation.</summary>
    public event EventHandler<MenuItemInvokedEventArgs>? Invoked;

    /// <summary>Gets or sets an optional submenu that opens as a popup when this item is activated.</summary>
    /// <remarks>
    /// When a submenu is assigned, activating this item opens a popup below a horizontal-menu item
    /// or right of a vertical-menu item instead of raising <see cref="Invoked"/>. The submenu closes
    /// on Escape or when one of its items is invoked. Items invoked in the submenu propagate through
    /// the owning <see cref="Menu.ItemInvoked"/> event. A menu may only be assigned as one item's
    /// submenu at a time; assigning a menu already hosted elsewhere is rejected, leaving the current
    /// submenu unchanged.
    /// </remarks>
    /// <exception cref="ArgumentException">The assigned menu already belongs to another tree.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public Menu? Submenu
    {
        get => _submenu;
        set
        {
            VerifyMutable();

            if (ReferenceEquals(_submenu, value))
            {
                return;
            }

            if (FindMenu() is { } menu)
            {
                menu.ReplaceSubmenu(this, value);
            }
            else
            {
                CommitSubmenu(value);
            }
        }
    }

    /// <summary>Commits one submenu replacement after the owning menu establishes its transition boundary.</summary>
    /// <param name="value">The replacement submenu, or null to remove the current value.</param>
    internal void CommitSubmenu(Menu? value)
    {
        // Construct and validate the replacement before touching the outgoing popup, so a
        // rejected assignment (the same menu already presented elsewhere) leaves everything
        // — the outgoing popup, the subscribed submenu, and this item's own state — untouched.
        var newPopup = value is null
            ? null
            : new Popup
            {
                Anchor = this,
                ConstrainToRoot = false,
                ModalBehavior = PopupModalBehavior.None,
                Placement = PopupPlacement.Below,
                Content = value,
            };

        if (_submenu is { } previous)
        {
            previous.ItemInvoked -= OnSubmenuItemInvoked;
        }

        if (_submenuPopup is not null)
        {
            _submenuPopup.IsOpen = false;
            _ = _submenuSlot.Remove(_submenuPopup);
            _submenuPopup.Content = null;
            _submenuPopup.Dispose();
            _submenuPopup = null;
        }

        _submenu = value;
        _submenuPopup = newPopup;

        if (newPopup is not null && value is not null)
        {
            newPopup.Closing += OnSubmenuClosing;
            newPopup.Closed += OnSubmenuClosed;
            _submenuSlot.Add(newPopup);
            value.ItemInvoked += OnSubmenuItemInvoked;
        }

        NotifyPropertyChanged(nameof(Submenu), InvalidationImpact.None);
    }

    /// <summary>Gets or sets the optional keyboard shortcut hint displayed right-aligned after the label.</summary>
    /// <remarks>
    /// When non-null, the text renders with dim attributes and accounts for two extra spacing cells.
    /// An explicit assignment always wins over <see cref="Shortcut"/>'s derived display text.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public string? ShortcutText
    {
        get => _shortcutTextValue ?? _derivedShortcutText;
        set => _ = SetProperty(ref _shortcutTextValue, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets the optional typed keyboard chord this item advertises.</summary>
    /// <remarks>
    /// Purely declarative: it derives <see cref="ShortcutText"/> when that is otherwise unset, but
    /// routes no input. A consumer that wants the gesture to actually invoke this item still owns
    /// that wiring, exactly as it did with a hand-typed <see cref="ShortcutText"/> before this member
    /// existed.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public KeyGesture? Shortcut
    {
        get;
        set
        {
            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                _derivedShortcutText = value?.ToString();

                if (_shortcutTextValue is null)
                {
                    NotifyPropertyChanged(nameof(ShortcutText), InvalidationImpact.Measure);
                }
            }
        }
    }

    /// <inheritdoc/>
    protected override bool OnAccessKey(Rune key)
    {
        _ = key;
        return FindMenu()?.InvokeAccessKey(this) ?? base.OnAccessKey(key);
    }

    /// <summary>Gets or sets the command, check, or radio behavior.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public MenuItemKind Kind
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The menu item kind is unknown.");
            }

            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            var clearChecked = value == MenuItemKind.Command && _isChecked;

            if (clearChecked)
            {
                _isChecked = false;
                _checkedVersion++;
                InvalidateVisualState();
            }

            ExceptionDispatchInfo? failure = null;

            if (value == MenuItemKind.Radio && _isChecked && FindMenu() is { } menu)
            {
                ExceptionAggregation.Capture(() => menu.SelectRadio(this), ref failure);
            }

            ExceptionAggregation.Capture(
                () => NotifyPropertyChanged(nameof(Kind), InvalidationImpact.Measure),
                ref failure);

            if (clearChecked)
            {
                ExceptionAggregation.Capture(
                    () => NotifyPropertyChanged(nameof(IsChecked), InvalidationImpact.None),
                    ref failure);
            }

            failure?.Throw();
        }
    }

    /// <summary>Gets or sets the local unchecked marker for check and radio items.</summary>
    public Rune UncheckedGlyph
    {
        get => _uncheckedGlyph ?? ThemeMenuGlyph(checkedValue: false).Value;
        set => SetOptionalGlyph(ref _uncheckedGlyph, value, nameof(UncheckedGlyph));
    }

    /// <summary>Gets or sets the local checked marker for check and radio items.</summary>
    public Rune CheckedGlyph
    {
        get => _checkedGlyph ?? ThemeMenuGlyph(checkedValue: true).Value;
        set => SetOptionalGlyph(ref _checkedGlyph, value, nameof(CheckedGlyph));
    }

    /// <summary>Clears both local menu markers to the code-owned defaults.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public void ResetGlyphs()
    {
        VerifyMutable();
        _ = ResetOptionalGlyph(ref _uncheckedGlyph, nameof(UncheckedGlyph));
        _ = ResetOptionalGlyph(ref _checkedGlyph, nameof(CheckedGlyph));
    }

    /// <summary>Gets or sets the optional non-empty radio-group name.</summary>
    /// <exception cref="ArgumentException">The value is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public string? GroupName
    {
        get;
        set
        {
            if (value is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value);
            }

            VerifyMutable();

            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            ExceptionDispatchInfo? failure = null;

            if (Kind == MenuItemKind.Radio && _isChecked && FindMenu() is { } menu)
            {
                ExceptionAggregation.Capture(() => menu.SelectRadio(this), ref failure);
            }

            ExceptionAggregation.Capture(
                () => NotifyPropertyChanged(nameof(GroupName), InvalidationImpact.None),
                ref failure);
            failure?.Throw();
        }
    }

    /// <summary>Gets or sets the checked state for check and radio items.</summary>
    /// <exception cref="InvalidOperationException">
    /// The item is not a check or radio item, or is mutated off-dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (Kind is not (MenuItemKind.Check or MenuItemKind.Radio))
            {
                throw new InvalidOperationException("Only check and radio menu items have a checked state.");
            }

            VerifyMutable();

            if (Kind == MenuItemKind.Radio && value && FindMenu() is { } menu)
            {
                menu.SelectRadio(this);
                return;
            }

            _ = SetVisualStateProperty(ref _isChecked, value);
        }
    }

    /// <summary>Activates this item through the programmatic path when it is available.</summary>
    /// <exception cref="InvalidOperationException">The attached item is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public void PerformInvoke()
    {
        VerifyMutable();

        if (EffectiveIsEnabled && EffectiveIsVisible)
        {
            Activate(ActivationCause.Programmatic);
        }
    }

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        if (_submenuPopup is not null)
        {
            if (FindMenu() is { } menu)
            {
                menu.ToggleSubmenu(this, cause);
            }
            else
            {
                ConfigureSubmenuPlacement();
                _submenuPopup.IsOpen = !_submenuPopup.IsOpen;
            }

            return;
        }

        switch (Kind)
        {
            case MenuItemKind.Check:
                _ = SetVisualStateProperty(ref _isChecked, !_isChecked, nameof(IsChecked));
                break;
            case MenuItemKind.Radio:
                if (FindMenu() is { } menu)
                {
                    menu.SelectRadio(this);
                }
                else
                {
                    _ = SetVisualStateProperty(ref _isChecked, true, nameof(IsChecked));
                }

                break;
            case MenuItemKind.Command:
                break;
            default:
                throw new UnreachableException();
        }

        var eventArgs = new MenuItemInvokedEventArgs(this, cause);
        var owner = FindMenu();
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(() => Invoked?.Invoke(this, eventArgs), ref failure);

        if (owner is not null)
        {
            ExceptionAggregation.Capture(() => owner.NotifyItemInvoked(eventArgs), ref failure);
        }

        failure?.Throw();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        if (_submenuPopup is not null)
        {
            _ = MeasureChild(_submenuPopup, new Constraint(constraint.Width, null));
        }

        var content = Content;
        var shortcutExtra = ShortcutText is { Length: > 0 }
            ? ShortcutExtent
            : 0;

        if (content is null)
        {
            return new Size(PrefixWidth.Add(shortcutExtra), 1);
        }

        var desired = MeasureChild(
            content,
            new Constraint(constraint.Width.Subtract(PrefixWidth), constraint.Height));

        return content.Visibility == Visibility.Collapsed
            ? new Size(PrefixWidth.Add(shortcutExtra), 1)
            : new Size(
                PrefixWidth.Add(desired.Width.Add(content.Margin.Horizontal).Add(shortcutExtra)),
                Math.Max(1, desired.Height.Add(content.Margin.Vertical)));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Content is { } content)
        {
            var consumed = Math.Min(PrefixWidth, bounds.Width);
            var trailing = Math.Min(ShortcutExtent, bounds.Width - consumed);
            ArrangeChild(
                content,
                new Rect(
                    bounds.X + consumed,
                    bounds.Y,
                    bounds.Width - consumed - trailing,
                    bounds.Height),
                ResolvedAxes.Both);
        }

        if (_submenuPopup is not null)
        {
            ArrangeChild(_submenuPopup, RootBounds(bounds), ResolvedAxes.Both);
        }
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var style = ResolvedStyle;

        if (this.HasOpaqueFill(GetAppearanceState()))
        {
            canvas.Clear(Bounds, style);
        }

        var themed = ThemeMenuGlyph(_isChecked);
        var glyph = (_isChecked ? CheckedGlyph : UncheckedGlyph).Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);
        var marker = Kind switch
        {
            MenuItemKind.Check => $"[{glyph}] ",
            MenuItemKind.Radio => $"{glyph} ",
            MenuItemKind.Command => string.Empty,
            _ => throw new UnreachableException()
        };
        _ = canvas.Draw(
            marker.AsSpan(),
            new Point(Bounds.X, Bounds.Y),
            style,
            background: BackgroundMode.Transparent);

        if (ShortcutText is { Length: > 0 })
        {
            var dimStyle = new TerminalStyle(
                style.Foreground,
                style.Background,
                style.Attributes | TerminalAttributes.Dim,
                style.Hyperlink,
                style.Underline,
                style.UnderlineColor);
            var shortcutX = Bounds.Right - ShortcutWidth;

            if (shortcutX > Bounds.X)
            {
                _ = canvas.Draw(
                    ShortcutText.AsSpan(),
                    new Point(shortcutX, Bounds.Y),
                    dimStyle,
                    background: BackgroundMode.Transparent);
            }
        }
    }

    /// <inheritdoc/>
    protected override bool IsCheckedState => _isChecked;

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);

        if (focused)
        {
            var menu = FindMenu();
            Debug.Assert(menu is not null, "A focused MenuItem belongs to a Menu.");
            menu.NotifyItemFocused(this);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        var sessionOwner = _submenuCloseOwner ??
            (IsSubmenuOpen ? FindMenu()?.FindSessionOwner() : null);
        ExceptionDispatchInfo? failure = null;

        if (sessionOwner is not null)
        {
            ExceptionAggregation.Capture(sessionOwner.CloseChain, ref failure);
        }

        ExceptionAggregation.Capture(() => base.OnUnavailable(reason), ref failure);

        if (reason == ReleaseReason.Disposed)
        {
            PointerExited -= OnPointerExited;
            var closeOwner = _submenuCloseOwner;
            _submenuCloseOwner = null;
            ExceptionAggregation.Capture(() => closeOwner?.EndSubmenuSurfaceClose(this), ref failure);

            if (Submenu is { } submenu)
            {
                submenu.ItemInvoked -= OnSubmenuItemInvoked;
            }

            // The popup is an owned child and the registry disposes it after this notification.
            // Disposing it here would reenter the owner's structural publication transaction.
            _submenuPopup = null;
            Invoked = null;
        }

        failure?.Throw();
    }

    private void OnSubmenuItemInvoked(object? sender, MenuItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        FindMenu()?.NotifyItemInvoked(eventArgs);
    }

    /// <summary>Stages a checked state for a coordinated menu radio transaction.</summary>
    /// <param name="value">The checked value to stage.</param>
    /// <returns>The new commit version, or zero when unchanged.</returns>
    internal int StageChecked(bool value)
    {
        VerifyMutable();

        if (_isChecked == value)
        {
            return 0;
        }

        _isChecked = value;
        _checkedVersion++;
        InvalidateVisualState();
        return _checkedVersion;
    }

    /// <summary>Gets whether one staged checked value remains current after callbacks.</summary>
    /// <param name="version">The positive staged commit version.</param>
    /// <param name="value">The expected checked value.</param>
    /// <returns>True when no reentrant transaction replaced the staged value.</returns>
    internal bool IsCheckedCommitCurrent(int version, bool value) =>
        version > 0 && _checkedVersion == version && _isChecked == value;

    /// <summary>Publishes one already-staged checked property change.</summary>
    /// <exception cref="InvalidOperationException">The attached item is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    internal void PublishChecked() => NotifyPropertyChanged(nameof(IsChecked), InvalidationImpact.None);

    /// <summary>Requests focus through this item's protected manager boundary.</summary>
    /// <returns>True when focus is acquired or already owned.</returns>
    /// <exception cref="InvalidOperationException">The attached item is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    internal bool RequestMenuFocus() => RequestFocus();

    /// <summary>Commits selected visual state from the containing menu.</summary>
    /// <param name="value">Whether this item is the menu's selected item.</param>
    internal void CommitSelection(bool value) => SetSelectedState(value);

    /// <summary>Activates this private face through its focus-owning menu.</summary>
    /// <param name="cause">The validated input cause.</param>
    internal void ActivateFromMenu(ActivationCause cause) => Activate(cause);

    /// <summary>Gets whether this item currently exposes its retained submenu popup.</summary>
    internal bool IsSubmenuOpen => _submenuPopup?.IsOpen == true;

    /// <summary>Gets whether this item retains a live submenu and its matching popup surface.</summary>
    internal bool HasRetainedSubmenuSurface =>
        _submenu is { IsDisposed: false } && _submenuPopup is { IsDisposed: false };

    /// <summary>Gets the measured label and marker width without the shortcut gutter or text.</summary>
    internal int DesiredLabelWidth => Math.Max(0, DesiredSize.Width - ShortcutExtent);

    /// <summary>Gets the Unicode cell width of this item's shortcut text.</summary>
    internal int ShortcutColumnWidth => ShortcutWidth;

    /// <summary>Gets the minimum cells reserved between the widest label and shortcut column.</summary>
    internal static int ShortcutGap => _shortcutGap;

    /// <summary>Opens this item's submenu when one is assigned.</summary>
    internal void OpenSubmenu()
    {
        if (_submenuPopup is null)
        {
            return;
        }

        if (FindMenu() is { } menu)
        {
            menu.OpenSubmenu(this);
        }
        else
        {
            OpenSubmenuSurface(openedFromPointerSelection: false);
        }
    }

    /// <summary>Opens this item's retained popup after its menu session has established modality.</summary>
    /// <param name="openedFromPointerSelection">
    /// Whether armed pointer movement, rather than explicit activation, selected this submenu.
    /// </param>
    internal void OpenSubmenuSurface(bool openedFromPointerSelection)
    {
        if (_submenuPopup is null)
        {
            return;
        }

        var wasOpen = _submenuPopup.IsOpen;
        ConfigureSubmenuPlacement();
        _submenuPopup.IsOpen = true;

        if (!wasOpen && _submenuPopup.IsOpen)
        {
            _submenuOpenedFromPointerSelection = openedFromPointerSelection;
        }
    }

    /// <summary>Consumes whether armed pointer selection just opened this submenu before its click activation.</summary>
    /// <returns>True exactly once for a submenu newly opened by pointer selection.</returns>
    internal bool ConsumePointerSelectionOpen()
    {
        var value = _submenuOpenedFromPointerSelection;
        _submenuOpenedFromPointerSelection = false;
        return value;
    }

    /// <summary>Clears a deferred pointer toggle after physical movement leaves this item's anchor.</summary>
    internal void ClearPointerSelectionOpen() => _submenuOpenedFromPointerSelection = false;

    /// <summary>Closes this item's submenu when it is open.</summary>
    internal void CloseSubmenu()
    {
        if (_submenuPopup?.IsOpen == true)
        {
            _submenuPopup.IsOpen = false;
        }
    }

    private int PrefixWidth => Kind == MenuItemKind.Check ? 4 : Kind == MenuItemKind.Radio ? 2 : 0;

    private int ShortcutWidth => ShortcutText is { Length: > 0 } shortcut
        ? Terminal.Unicode.Width.Measure(shortcut, CellPolicy.AmbiguousWidth).Cells
        : 0;

    private int ShortcutExtent
    {
        get
        {
            var width = ShortcutWidth;
            return width == 0 ? 0 : width.Add(_shortcutGap);
        }
    }

    private ControlGlyph ThemeMenuGlyph(bool checkedValue)
    {
        var selection = ControlGlyphs.Selection;
        return Kind == MenuItemKind.Radio
            ? checkedValue ? selection.MenuRadioChecked : selection.MenuRadioUnchecked
            : checkedValue
                ? selection.MenuCheckChecked
                : selection.MenuCheckUnchecked;
    }

    private void ConfigureSubmenuPlacement()
    {
        Debug.Assert(_submenuPopup is not null, "Submenu placement requires a retained popup.");
        _submenuPopup.Placement = FindMenu()?.Orientation == Orientation.Vertical
            ? PopupPlacement.Right
            : PopupPlacement.Below;
    }

    private void OnSubmenuClosing(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (FindMenu() is { } menu)
        {
            Debug.Assert(_submenuCloseOwner is null, "A submenu popup owns one close bracket at a time.");
            _submenuCloseOwner = menu.BeginSubmenuSurfaceClose();
            menu.RestoreFocusAfterSubmenuClose();
        }
    }

    private void OnSubmenuClosed(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        _submenuOpenedFromPointerSelection = false;
        var closeOwner = _submenuCloseOwner;
        _submenuCloseOwner = null;
        closeOwner?.EndSubmenuSurfaceClose(this);
    }

    private void OnPointerExited(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        _submenuOpenedFromPointerSelection = false;
    }

    private Menu? FindMenu() => FindAncestor<Menu>();
}
