// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

/// <summary>Defines one focusable command, check, or radio entry in a <see cref="Menu"/>.</summary>
public sealed class MenuItem: Pressable
{
    private readonly OwnedControlSlot _submenuSlot;
    private bool _isChecked;
    private int _checkedVersion;
    private Popup? _submenuPopup;

    /// <summary>Initializes an ordinary command item with no content.</summary>
    public MenuItem()
    {
        _submenuSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Popup,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "submenu",
                ChangeImpact.None),
            capacity: 1);
    }

    /// <summary>Raised after an eligible item commits its optional check state and activation.</summary>
    public event EventHandler<MenuItemInvokedEventArgs>? Invoked;

    /// <summary>Gets or sets an optional submenu that opens as a popup when this item is activated.</summary>
    /// <remarks>
    /// When a submenu is assigned, activating this item opens a popup anchored below instead of
    /// raising <see cref="Invoked"/>. The submenu closes on Escape or when one of its items is invoked.
    /// Items invoked in the submenu propagate through the owning <see cref="Menu.ItemInvoked"/> event.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public Menu? Submenu
    {
        get;
        set
        {
            VerifyMutable();

            if (ReferenceEquals(field, value))
            {
                return;
            }

            if (_submenuPopup is not null)
            {
                _submenuPopup.IsOpen = false;
                _ = _submenuSlot.Remove(_submenuPopup);
                _submenuPopup.Dispose();
                _submenuPopup = null;
            }

            if (Submenu is { } previous)
            {
                previous.ItemInvoked -= OnSubmenuItemInvoked;
            }

            field = value;

            if (value is not null)
            {
                _submenuPopup = new Popup
                {
                    Anchor = this,
                    Placement = PopupPlacement.Below,
                    Content = value,
                };
                _submenuSlot.Add(_submenuPopup);
                value.ItemInvoked += OnSubmenuItemInvoked;
            }

            NotifyPropertyChanged(nameof(Submenu), ChangeImpact.None);
        }
    }

    /// <summary>Gets or sets the optional keyboard shortcut hint displayed right-aligned after the label.</summary>
    /// <remarks>When non-null, the text renders with dim attributes and accounts for two extra spacing cells.</remarks>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public string? ShortcutText
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Measure);
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

            var failure = (ExceptionDispatchInfo?) null;

            if (value == MenuItemKind.Radio && _isChecked && FindMenu() is { } menu)
            {
                CaptureFailure(() => menu.SelectRadio(this), ref failure);
            }

            CaptureFailure(
                () => NotifyPropertyChanged(nameof(Kind), ChangeImpact.Measure),
                ref failure);

            if (clearChecked)
            {
                CaptureFailure(
                    () => NotifyPropertyChanged(nameof(IsChecked), ChangeImpact.None),
                    ref failure);
            }

            failure?.Throw();
        }
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
            var failure = (ExceptionDispatchInfo?) null;

            if (Kind == MenuItemKind.Radio && _isChecked && FindMenu() is { } menu)
            {
                CaptureFailure(() => menu.SelectRadio(this), ref failure);
            }

            CaptureFailure(
                () => NotifyPropertyChanged(nameof(GroupName), ChangeImpact.None),
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

            _ = SetVisualStateProperty(ref _isChecked, value, nameof(IsChecked));
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
            _submenuPopup.IsOpen = !_submenuPopup.IsOpen;
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
        var failure = (ExceptionDispatchInfo?) null;
        CaptureFailure(() => Invoked?.Invoke(this, eventArgs), ref failure);

        if (owner is not null)
        {
            CaptureFailure(() => owner.NotifyItemInvoked(eventArgs), ref failure);
        }

        failure?.Throw();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var content = Content;
        var shortcutExtra = ShortcutText is { Length: > 0 }
            ? ShortcutText.Length + 2
            : 0;

        if (content is null)
        {
            return new Size(Add(PrefixWidth, shortcutExtra), 1);
        }

        var desired = MeasureChild(
            content,
            new Constraint(Subtract(constraint.Width, PrefixWidth), constraint.Height));

        return content.Visibility == Visibility.Collapsed
            ? new Size(Add(PrefixWidth, shortcutExtra), 1)
            : new Size(
                Add(PrefixWidth, Add(Add(desired.Width, content.Margin.Horizontal), shortcutExtra)),
                Math.Max(1, Add(desired.Height, content.Margin.Vertical)));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Content is { } content)
        {
            var consumed = Math.Min(PrefixWidth, bounds.Width);
            ArrangeChild(
                content,
                new Rect(bounds.X + consumed, bounds.Y, bounds.Width - consumed, bounds.Height),
                ResolvedAxes.Both);
        }
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var style = ResolvedStyle;

        if (ControlAppearance.HasOpaqueFill(this, GetVisualState()))
        {
            canvas.Clear(Bounds, style);
        }

        var marker = Kind switch
        {
            MenuItemKind.Check => _isChecked ? "[✓] " : "[ ] ",
            MenuItemKind.Radio => _isChecked ? "◉ " : "○ ",
            MenuItemKind.Command => string.Empty,
            _ => throw new UnreachableException(),
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
            var shortcutX = Bounds.Right - ShortcutText.Length;

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
            FindMenu()?.NotifyItemFocused(this);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            if (Submenu is { } submenu)
            {
                submenu.ItemInvoked -= OnSubmenuItemInvoked;
            }

            _submenuPopup?.Dispose();
            Invoked = null;
        }
    }

    private void OnSubmenuItemInvoked(object? sender, MenuItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        if (_submenuPopup is { IsOpen: true } popup)
        {
            popup.IsOpen = false;
        }
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
    internal void PublishChecked() => NotifyPropertyChanged(nameof(IsChecked), ChangeImpact.None);

    /// <summary>Requests focus through this item's protected manager boundary.</summary>
    /// <returns>True when focus is acquired or already owned.</returns>
    /// <exception cref="InvalidOperationException">The attached item is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    internal bool RequestMenuFocus() => RequestFocus();

    /// <summary>Commits selected visual state from the containing menu.</summary>
    /// <param name="value">Whether this item is the menu's selected item.</param>
    internal void CommitSelection(bool value) => SetSelectedState(value);

    private int PrefixWidth => Kind == MenuItemKind.Check ? 4 : Kind == MenuItemKind.Radio ? 2 : 0;

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0, "MenuItem accumulation uses non-negative extents.");
        Debug.Assert(right >= 0, "MenuItem accumulation uses non-negative extents.");

        return (int) Math.Min(int.MaxValue, (long) left + right);
    }

    private static void CaptureFailure(Action action, ref ExceptionDispatchInfo? failure)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }
    }

    private static int? Subtract(int? value, int extent)
    {
        Debug.Assert(extent >= 0, "MenuItem subtraction extent is non-negative.");

        return value.HasValue
            ? Math.Max(0, value.Value - extent)
            : null;
    }

    private Menu? FindMenu()
    {
        for (var current = Parent; current is not null; current = current.Parent)
        {
            if (current is Menu menu)
            {
                return menu;
            }
        }

        return null;
    }
}
