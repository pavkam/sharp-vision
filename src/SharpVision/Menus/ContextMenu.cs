// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

using Popups;

/// <summary>Displays a vertical menu at an arbitrary cell position with light dismiss.</summary>
[PublicAPI]
public class ContextMenu: Control, IContextMenu
{
    private readonly Popup _popup;
    private LightDismiss? _lightDismiss;

    /// <summary>Initializes a closed context menu.</summary>
    public ContextMenu()
    {
        Menu = new Menu { Orientation = Orientation.Vertical };
        Menu.ItemInvoked += OnMenuItemInvoked;
        _popup = new Popup
        {
            Content = Menu,
            SuppressCloseOtherPopups = true,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _popup.Closing += OnPopupClosing;
        _popup.Closed += OnPopupClosed;
    }

    /// <summary>Raised immediately before the menu is shown, allowing dynamic item updates.</summary>
    public event EventHandler? Opening;

    /// <summary>Raised immediately before a closing context menu hides its content.</summary>
    public event EventHandler? Closing;

    /// <summary>Raised after the context menu has hidden its content.</summary>
    public event EventHandler? Closed;

    /// <summary>Gets the typed managed menu items.</summary>
    public MenuEntryCollection Items => Menu.Items;

    /// <summary>Gets whether the context menu is currently open.</summary>
    public bool IsOpen => _popup.IsOpen;

    /// <inheritdoc/>
    public Control Presentation => _popup;

    internal Menu Menu { get; }

    /// <summary>Opens the context menu at the specified cell position.</summary>
    /// <param name="row">The zero-based row in root coordinates.</param>
    /// <param name="col">The zero-based column in root coordinates.</param>
    public void Show(int row, int col)
    {
        if (_popup.Parent is null)
        {
            return;
        }

        Opening?.Invoke(this, EventArgs.Empty);

        _popup.FixedOrigin = new Point(col, row);
        _popup.IsOpen = true;
        _lightDismiss?.Dispose();
        _lightDismiss = new LightDismiss(
            _popup,
            anchor: null,
            () => _popup.IsOpen,
            () => _popup.SurfaceBounds,
            Close);
    }

    /// <summary>Programmatically closes the context menu.</summary>
    public void Close()
    {
        if (!_popup.IsOpen)
        {
            return;
        }

        _lightDismiss?.Dispose();
        _lightDismiss = null;
        _popup.IsOpen = false;
        _popup.FixedOrigin = null;
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        if (reason == ReleaseReason.Disposed)
        {
            _lightDismiss?.Dispose();
            _popup.Closing -= OnPopupClosing;
            _popup.Closed -= OnPopupClosed;
            Menu.ItemInvoked -= OnMenuItemInvoked;
            Opening = null;
            Closing = null;
            Closed = null;
        }

        base.OnUnavailable(reason);
    }

    private void OnMenuItemInvoked(object? sender, MenuItemInvokedEventArgs e) => Close();

    private void OnPopupClosing(object? sender, EventArgs e) => Closing?.Invoke(this, EventArgs.Empty);

    private void OnPopupClosed(object? sender, EventArgs e) => Closed?.Invoke(this, EventArgs.Empty);

}
