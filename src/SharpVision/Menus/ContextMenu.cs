// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Menus;

using Popups;

/// <summary>Displays a vertical menu at an arbitrary cell position with light dismiss.</summary>
[PublicAPI]
public class ContextMenu: IDisposable
{
    private readonly Popup _popup;
    private LightDismiss? _lightDismiss;
    private bool _isDisposed;
    private long _presentationVersion;

    /// <summary>Initializes a closed context menu with its own empty vertical menu.</summary>
    public ContextMenu() : this(new Menu { Orientation = Orientation.Vertical })
    {
    }

    /// <summary>Initializes a closed context menu that adopts an already-built menu.</summary>
    /// <param name="menu">
    /// The menu to present, typically composed with <see cref="MenuBuilder.Vertical()"/>. The
    /// caller is responsible for building it vertical — this constructor uses the menu's
    /// orientation as given rather than coercing it, matching how <see cref="MenuItem.Submenu"/>
    /// adopts a menu.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="menu"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="menu"/> already belongs to a tree.</exception>
    public ContextMenu(Menu menu)
    {
        ArgumentNullException.ThrowIfNull(menu);

        Menu = menu;
        Menu.ItemInvoked += OnMenuItemInvoked;
        _popup = new Popup
        {
            Content = Menu,
            SuppressCloseOtherPopups = true,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _popup.Closing += OnPopupClosing;
        _popup.Closed += OnPopupClosed;
        _popup.PropertyChanged += OnPopupPropertyChanged;
        _popup.ParentChanged += OnPopupParentChanged;
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

    internal ControlBase Presentation => _popup;

    internal Menu Menu { get; }

    /// <summary>Gets or sets the owned popup's border and shadow together.</summary>
    /// <remarks>
    /// A component left null keeps the popup on its own <see cref="PopupChrome"/> role
    /// appearance for that part.
    /// </remarks>
    public PopupChrome PopupChrome
    {
        get => _popup.Style;
        set => _popup.Style = value;
    }

    /// <summary>Returns the popup's border and shadow to <see cref="PopupChrome"/> ownership.</summary>
    public void ResetPopupChrome() => PopupChrome = default;

    /// <summary>Opens the context menu at the specified cell position.</summary>
    /// <param name="row">The zero-based row in root coordinates.</param>
    /// <param name="col">The zero-based column in root coordinates.</param>
    /// <remarks>
    /// A no-op until this menu is assigned to some <see cref="ControlBase.ContextMenu"/> — only that
    /// assignment attaches the retained popup this method opens.
    /// </remarks>
    public void Show(int row, int col)
    {
        if (_popup.Parent is null)
        {
            return;
        }

        var presentationVersion = _presentationVersion;
        Opening?.Invoke(this, EventArgs.Empty);

        if (_isDisposed || presentationVersion != _presentationVersion || _popup.Parent is null)
        {
            return;
        }

        _popup.FixedOrigin = new Point(col, row);
        _popup.IsOpen = true;
    }

    /// <summary>Programmatically closes the context menu.</summary>
    public void Close()
    {
        if (_popup.IsOpen)
        {
            _popup.IsOpen = false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases owned handlers and light-dismiss state.</summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/> rather than a finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed || !disposing)
        {
            return;
        }

        _isDisposed = true;
        _lightDismiss?.Dispose();
        _popup.Closing -= OnPopupClosing;
        _popup.Closed -= OnPopupClosed;
        _popup.PropertyChanged -= OnPopupPropertyChanged;
        _popup.ParentChanged -= OnPopupParentChanged;
        Menu.ItemInvoked -= OnMenuItemInvoked;
        Opening = null;
        Closing = null;
        Closed = null;
    }

    private void OnMenuItemInvoked(object? sender, MenuItemInvokedEventArgs e) => Close();

    private void OnPopupClosing(object? sender, EventArgs e) => Closing?.Invoke(this, EventArgs.Empty);

    private void OnPopupClosed(object? sender, EventArgs e) => Closed?.Invoke(this, EventArgs.Empty);

    private void OnPopupParentChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        _presentationVersion++;
    }

    // IsOpen changes on every closure path, including indirect ones the popup
    // reaches on its own — detachment (a caller replaces or clears
    // ControlBase.ContextMenu while open) and disposal never raise Closing/Closed,
    // but they do flip IsOpen through CommitClosedState. Reacting here instead
    // of only inside Close() is what stops the root light-dismiss handler from
    // outliving a popup that closed itself.
    private void OnPopupPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _ = sender;

        if (e.PropertyName != nameof(Popup.IsOpen))
        {
            return;
        }

        if (_popup.IsOpen)
        {
            _lightDismiss?.Dispose();
            _lightDismiss = new LightDismiss(
                _popup,
                anchor: null,
                () => _popup.IsOpen,
                () => _popup.SurfaceBounds,
                Close);
        }
        else
        {
            _lightDismiss?.Dispose();
            _lightDismiss = null;
            _popup.FixedOrigin = null;
        }
    }
}
