// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

using System.Runtime.ExceptionServices;

using Popups;

using SharpVision.Terminal.Input;

/// <summary>Defines one focusable command, check, or radio entry in a <see cref="Menu"/>.</summary>
[PublicAPI]
public sealed class MenuItem: InputBase, IStyled<MenuItemStyle>
{
    private const int _shortcutGap = 2;
    private readonly OwnedControlSlot _submenuSlot;
    private bool _isChecked;
    private bool _submenuOpenedFromPointerSelection;
    private int _checkedVersion;
    private Menu? _submenu;
    private Menu? _submenuCloseOwner;
    private Popup? _submenuPopup;
    private readonly StyleSlot<MenuItemStyle> _style;
    private string? _shortcutTextValue;
    private string? _derivedShortcutText;
    private Dispatcher? _shortcutCountDispatcher;

    /// <summary>Initializes an ordinary command item with no content.</summary>
    public MenuItem()
    {
        EnablePressActivation();
        EnableCaption();
        EnableCommand();
        _style = InitializeStyle(MenuItemStyle.Definition);
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

            if (FindAncestor<Menu>() is { } menu)
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
                ModalBehavior = PopupModalBehavior.None,
                Placement = PopupPlacement.Below,
                Content = value,
                SuppressCloseOtherPopups = true,
                // MenuItem re-arranges its own submenu popup from its own ArrangeOverride every
                // pass (RootBounds(bounds) elsewhere in this file), so base Popup's anchor-reflow
                // tracking would be a redundant second placement pass reacting to the same
                // self-owned anchor.
                TracksAnchorReflow = false
            };

        if (newPopup is not null && SubmenuChrome != default)
        {
            newPopup.Style = SubmenuChrome;
        }

        if (_submenu is { } previous)
        {
            previous.ItemInvoked -= OnSubmenuItemInvoked;
        }

        if (_submenuPopup is not null)
        {
            _submenuPopup.ContentDisposalRequested -= OnSubmenuContentDisposalRequested;
            _submenuPopup.CloseImmediatelyForPeerTransition();
            _ = _submenuSlot.Remove(_submenuPopup);
            _submenuPopup.Content = null;
            _submenuPopup.Dispose();
            _submenuPopup = null;
        }

        _submenu = value;
        _submenuPopup = newPopup;

        if (newPopup is not null && value is not null)
        {
            newPopup.ContentDisposalRequested += OnSubmenuContentDisposalRequested;
            newPopup.Closing += OnSubmenuClosing;
            newPopup.Closed += OnSubmenuClosed;
            _submenuSlot.Add(newPopup);
            value.ItemInvoked += OnSubmenuItemInvoked;
        }

        NotifyPropertyChanged(nameof(Submenu), InvalidationImpact.None);
    }

    private void OnSubmenuContentDisposalRequested(object? sender, OwnedContentDisposalEventArgs eventArgs)
    {
        if (ReferenceEquals(sender, _submenuPopup) && ReferenceEquals(eventArgs.Content, _submenu))
        {
            if (FindAncestor<Menu>() is { } menu)
            {
                menu.ReplaceSubmenu(this, null);
            }
            else
            {
                CommitSubmenu(null);
            }
        }
    }

    /// <summary>Gets or sets the submenu's owned popup border and shadow together.</summary>
    /// <remarks>
    /// A component left null keeps the popup on its own <see cref="PopupChrome"/> role
    /// appearance for that part, exactly as an unset <see cref="Popup.Style"/> would. Applies
    /// immediately to an already-open submenu's popup, and survives a <see cref="Submenu"/>
    /// reassignment, which recreates the popup.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public PopupChrome SubmenuChrome
    {
        get;
        set
        {
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            _ = _submenuPopup?.Style = value;
            NotifyPropertyChanged(nameof(SubmenuChrome), InvalidationImpact.None);
        }
    }

    /// <summary>Returns the submenu popup's border and shadow to <see cref="PopupChrome"/> ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public void ResetSubmenuChrome() => SubmenuChrome = default;

    /// <summary>Gets or sets the optional leading edge-pinned decoration, reserved inboard of the
    /// check or radio marker column and outside the caption's own alignment box.</summary>
    /// <remarks>
    /// A vertical <see cref="Menu"/> negotiates one shared start-affix column across every owned
    /// row, so every row's caption begins at the same cell whether or not that specific row sets
    /// this property - the same alignment guarantee the shortcut column already gives
    /// <see cref="ShortcutText"/>. A row without its own <see cref="StartAffix"/> simply leaves that
    /// shared column blank. This property's own glyph, when set, always draws flush against the
    /// marker column, never shifted by a wider sibling's column.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public Affix? StartAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <summary>Gets or sets the optional trailing edge-pinned decoration, reserved between the
    /// caption and the shortcut column and outside the caption's own alignment box.</summary>
    /// <remarks>
    /// Unlike <see cref="StartAffix"/>, this column is never negotiated across sibling rows - it
    /// stays purely per-item, matching how only the shortcut column, not a general trailing column,
    /// is shared today.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public Affix? EndAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
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

    /// <summary>Gets or sets the optional typed keyboard chord that both displays and activates this item.</summary>
    /// <remarks>
    /// Derives <see cref="ShortcutText"/> when that is otherwise unset. A matching keyboard
    /// transition activates the item application-wide, independent of focus, as long as the item
    /// is attached and enabled and is reachable from the active interaction plane.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public KeyGesture? Shortcut
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
                    _derivedShortcutText = Shortcut?.ToString();

                    // Contribution identity, not the before/after property values, owns the count.
                    // Parent and attachment callbacks may change Shortcut while dispatcher context
                    // has already moved, so reconciliation must be idempotent at every boundary.
                    SynchronizeShortcutContribution(Dispatcher);

                    if (_shortcutTextValue is null)
                    {
                        NotifyPropertyChanged(nameof(ShortcutText), InvalidationImpact.Measure);
                    }
                });
        }
    }

    /// <summary>Reports whether a completed keyboard transition should activate this item's shortcut.</summary>
    /// <param name="stroke">The decoded keyboard transition.</param>
    /// <returns>
    /// True when the item is attached, not disposed, enabled (visibility is deliberately not
    /// required, so shortcuts reach items inside a closed submenu), and <see cref="Shortcut"/> is
    /// set and matches <paramref name="stroke"/>.
    /// </returns>
    [Pure]
    internal bool MatchesShortcut(in Stroke stroke) =>
        !IsDisposed &&
        Dispatcher is not null &&
        EffectiveIsEnabled &&
        Shortcut is { } gesture &&
        gesture.Matches(stroke);

    /// <inheritdoc/>
    protected override bool OnAccessKey(Rune key)
    {
        _ = key;
        return FindAncestor<Menu>()?.InvokeAccessKey(this) ?? base.OnAccessKey(key);
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
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The menu item kind is unknown.");

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

            if (value == MenuItemKind.Radio && _isChecked && FindAncestor<Menu>() is { } menu)
            {
                CaptureFailure(() => menu.SelectRadio(this), ref failure);
            }

            CaptureFailure(
                () => NotifyPropertyChanged(nameof(Kind), InvalidationImpact.Measure),
                ref failure);

            if (clearChecked)
            {
                CaptureFailure(
                    () => NotifyPropertyChanged(nameof(IsChecked), InvalidationImpact.None),
                    ref failure);
            }

            failure?.Throw();
        }
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public MenuItemStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public MenuItemStyle ActualStyle => _style.Actual;

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

            if (Kind == MenuItemKind.Radio && _isChecked && FindAncestor<Menu>() is { } menu)
            {
                CaptureFailure(() => menu.SelectRadio(this), ref failure);
            }

            CaptureFailure(
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

            if (Kind == MenuItemKind.Radio && value && FindAncestor<Menu>() is { } menu)
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
    public void PerformInvoke() => _ = TryActivate(ActivationCause.Programmatic);

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        if (_submenuPopup is not null)
        {
            if (FindAncestor<Menu>() is { } menu)
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

        var command = CaptureCommand();

        switch (Kind)
        {
            case MenuItemKind.Check:
                _ = SetVisualStateProperty(ref _isChecked, !_isChecked, nameof(IsChecked));
                break;
            case MenuItemKind.Radio:
                if (FindAncestor<Menu>() is { } menu)
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
        var owner = FindAncestor<Menu>();
        ExceptionDispatchInfo? failure = null;
        CaptureFailure(() => Invoked?.Invoke(this, eventArgs), ref failure);

        if (owner is not null &&
            !IsDisposed &&
            !owner.IsDisposed &&
            ReferenceEquals(FindAncestor<Menu>(), owner))
        {
            CaptureFailure(() => owner.NotifyItemInvoked(eventArgs), ref failure);
        }

        CaptureFailure(() => ExecuteCommandIfAny(command), ref failure);

        failure?.Throw();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Folds this row's own local <see cref="StartAffix"/>/<see cref="EndAffix"/> reservation into
    /// the desired size, exactly like <see cref="Controls.Input.Button"/> does - so a standalone
    /// item, not owned by any <see cref="Menu"/>, still measures itself correctly. A vertical Menu's
    /// own shared-column negotiation (see <see cref="AffixColumnWidth"/>) only ever widens what this
    /// pass already reserves; it never needs to shrink it.
    /// </remarks>
    protected override Size MeasureOverride(Constraint constraint)
    {
        if (_submenuPopup is not null)
        {
            _ = MeasureChild(_submenuPopup, new Constraint(constraint.Width, null));
        }

        var content = TextControl;
        var affixes = MeasureAffixes(StartAffix, EndAffix, ActualStyle.AffixGap);
        var leading = PrefixWidth.Add(affixes.StartCells);
        var shortcutExtra = ShortcutText is { Length: > 0 }
            ? ShortcutExtent
            : 0;
        var trailing = affixes.EndCells.Add(shortcutExtra);

        if (content is null)
        {
            return new Size(leading.Add(trailing), 1);
        }

        var desired = MeasureChild(
            content,
            new Constraint(constraint.Width.Subtract(leading).Subtract(affixes.EndCells), constraint.Height));

        return content.Visibility == Visibility.Collapsed
            ? new Size(leading.Add(trailing), 1)
            : new Size(
                leading.Add(desired.Width.Add(content.Margin.Horizontal)).Add(trailing),
                Math.Max(1, desired.Height.Add(content.Margin.Vertical)));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The caption's leading inset is the larger of this row's own local start-affix reservation and
    /// the negotiated <see cref="AffixColumnWidth"/>, so every sibling row's caption begins at the
    /// same column - a row with a narrower or absent <see cref="StartAffix"/> simply leaves blank
    /// filler before its caption. <see cref="EndAffix"/> is never negotiated and only ever reserves
    /// its own local width, between the caption and the shortcut column.
    /// </remarks>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (TextControl is { } content)
        {
            var rowLeading = Math.Min(PrefixWidth, bounds.Width);
            var rowTrailing = Math.Min(ShortcutExtent, bounds.Width - rowLeading);
            var rowWidth = bounds.Width - rowLeading - rowTrailing;

            var startCells = Math.Min(Math.Max(StartAffixCells, AffixColumnWidth), rowWidth);
            var endCells = Math.Min(
                MeasureAffixes(null, EndAffix, ActualStyle.AffixGap).EndCells,
                rowWidth - startCells);

            ArrangeChild(
                content,
                new Rect(
                    bounds.X.Add(rowLeading).Add(startCells),
                    bounds.Y,
                    rowWidth - startCells - endCells,
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

        var content = ContentBounds;

        var themed = ThemeMenuGlyph(_isChecked);
        var glyph = themed.Value.Resolve(themed.Fallback, CellPolicy.AmbiguousWidth);
        var marker = Kind switch
        {
            MenuItemKind.Check => $"[{glyph}] ",
            MenuItemKind.Radio => $"{glyph} ",
            MenuItemKind.Command => string.Empty,
            _ => throw new UnreachableException()
        };
        _ = canvas.Draw(
            marker.AsSpan(),
            new Point(content.X, content.Y),
            style,
            background: BackgroundMode.Transparent);

        if (StartAffix is not null || EndAffix is not null)
        {
            // Measured fresh against live ContentBounds, not against a possibly-stale Measure pass -
            // matching RenderAffixes' own documented overflow rule. The row box excludes the
            // marker and shortcut columns the same way ArrangeOverride's rowLeading/rowTrailing
            // does, so a start affix always draws flush against the marker column regardless of
            // AffixColumnWidth - only the caption's own inset shifts for a wider sibling.
            var rowLeading = Math.Min(PrefixWidth, content.Width);
            var rowTrailing = Math.Min(ShortcutExtent, content.Width - rowLeading);
            var rowBox = new Rect(content.X + rowLeading, content.Y, content.Width - rowLeading - rowTrailing, 1);
            var affixes = MeasureAffixes(StartAffix, EndAffix, ActualStyle.AffixGap);
            RenderAffixes(canvas, rowBox, affixes, StartAffix, EndAffix, style);
        }

        if (ShortcutText is { Length: > 0 })
        {
            var dimStyle = new TerminalStyle(
                style.Foreground,
                style.Background,
                style.Attributes | TerminalAttributes.Dim,
                style.Hyperlink,
                style.Underline,
                style.UnderlineColor);
            var shortcutX = content.Right - ShortcutWidth;

            if (shortcutX > content.X)
            {
                _ = canvas.Draw(
                    ShortcutText.AsSpan(),
                    new Point(shortcutX, content.Y),
                    dimStyle,
                    background: BackgroundMode.Transparent);
            }
        }
    }

    /// <inheritdoc/>
    protected override bool IsCheckedState => _isChecked;

    /// <inheritdoc/>
    /// <remarks>
    /// Reconciles the exact dispatcher contribution after attachment callbacks and any reentrant
    /// shortcut assignment they performed.
    /// </remarks>
    protected override void OnAttached()
    {
        base.OnAttached();
        Debug.Assert(Dispatcher is not null, "An attached menu item owns a dispatcher.");
        SynchronizeShortcutContribution(Dispatcher);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Releases the exact dispatcher contribution cached during attachment, regardless of any
    /// shortcut assignment performed by earlier detachment callbacks.
    /// </remarks>
    protected override void OnDetached()
    {
        SynchronizeShortcutContribution(null);
        base.OnDetached();
    }

    private void SynchronizeShortcutContribution(Dispatcher? dispatcher)
    {
        var desired = Shortcut is null ? null : dispatcher;

        if (ReferenceEquals(_shortcutCountDispatcher, desired))
        {
            return;
        }

        _shortcutCountDispatcher?.DecrementLiveShortcutCount();
        _shortcutCountDispatcher = desired;
        _shortcutCountDispatcher?.IncrementLiveShortcutCount();
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);

        if (focused)
        {
            var menu = FindAncestor<Menu>();
            Debug.Assert(menu is not null, "A focused MenuItem belongs to a Menu.");
            menu.NotifyItemFocused(this);
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        var sessionOwner = _submenuCloseOwner ??
            (IsSubmenuOpen ? FindAncestor<Menu>()?.FindSessionOwner() : null);
        ExceptionDispatchInfo? failure = null;

        if (sessionOwner is not null)
        {
            CaptureFailure(sessionOwner.CloseChain, ref failure);
        }

        CaptureFailure(() => base.OnUnavailable(reason), ref failure);

        if (reason == ReleaseReason.Disposed)
        {
            PointerExited -= OnPointerExited;
            var closeOwner = _submenuCloseOwner;
            _submenuCloseOwner = null;
            CaptureFailure(() => closeOwner?.EndSubmenuSurfaceClose(this), ref failure);

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
        FindAncestor<Menu>()?.NotifyItemInvoked(eventArgs);
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
    [Pure]
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

    /// <summary>Gets the measured label, marker, and own-local-affix width without the shortcut
    /// gutter or text.</summary>
    internal int DesiredLabelWidth => Math.Max(0, DesiredSize.Width - ShortcutExtent);

    /// <summary>Gets the Unicode cell width of this item's shortcut text.</summary>
    internal int ShortcutColumnWidth => ShortcutWidth;

    /// <summary>Gets the minimum cells reserved between the widest label and shortcut column.</summary>
    internal static int ShortcutGap => _shortcutGap;

    /// <summary>Gets this row's own local start-affix reservation - the input a vertical
    /// <see cref="Menu"/> maximizes across every sibling row to negotiate
    /// <see cref="AffixColumnWidth"/>.</summary>
    internal int StartAffixCells => MeasureAffixes(StartAffix, null, ActualStyle.AffixGap).StartCells;

    /// <summary>Gets the shared leading affix column a vertical <see cref="Menu"/> negotiated
    /// across every owned row, so a caption begins at the same column whether or not this exact
    /// row sets <see cref="StartAffix"/>. Zero for an item not owned by a vertical Menu.</summary>
    internal int AffixColumnWidth { get; private set; }

    /// <summary>Applies the shared start-affix column a vertical <see cref="Menu"/> negotiated
    /// across every owned row.</summary>
    /// <param name="value">The non-negative negotiated column width in cells.</param>
    /// <remarks>
    /// Only ever invalidates <see cref="Invalidation.Arrange"/> - this repositions the caption
    /// inside a box <see cref="MeasureOverride"/> already reserved for this row's own worst case,
    /// it never changes this row's own desired size.
    /// </remarks>
    internal void SetSharedStartAffixColumn(int value)
    {
        if (AffixColumnWidth == value)
        {
            return;
        }

        AffixColumnWidth = value;
        Invalidate(Invalidation.Arrange);
    }

    /// <summary>Opens this item's submenu when one is assigned.</summary>
    internal void OpenSubmenu()
    {
        if (_submenuPopup is null)
        {
            return;
        }

        if (FindAncestor<Menu>() is { } menu)
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
            _submenuPopup.CloseImmediatelyForPeerTransition();
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

    // The marker comes from the resolved style; only the ASCII repair value stays code-owned, which
    // is the split theming-new-controls.md asks for. This resolver was already named for a theme
    // lookup it did not perform - it read four hardcoded ControlGlyphs.Selection entries that
    // nothing in the theme pipeline parsed.
    [Pure]
    private ControlGlyph ThemeMenuGlyph(bool checkedValue)
    {
        var selection = ControlGlyphs.Selection;
        var style = ActualStyle;
        return Kind == MenuItemKind.Radio
            ? checkedValue
                ? new ControlGlyph(style.RadioCheckedGlyph, selection.MenuRadioChecked.Fallback)
                : new ControlGlyph(style.RadioUncheckedGlyph, selection.MenuRadioUnchecked.Fallback)
            : checkedValue
                ? new ControlGlyph(style.CheckedGlyph, selection.MenuCheckChecked.Fallback)
                : new ControlGlyph(style.UncheckedGlyph, selection.MenuCheckUnchecked.Fallback);
    }

    private void ConfigureSubmenuPlacement()
    {
        Debug.Assert(_submenuPopup is not null, "Submenu placement requires a retained popup.");
        _submenuPopup.Placement = FindAncestor<Menu>()?.Orientation == Orientation.Vertical
            ? PopupPlacement.Right
            : PopupPlacement.Below;
    }

    private void OnSubmenuClosing(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (FindAncestor<Menu>() is { } menu)
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

}
