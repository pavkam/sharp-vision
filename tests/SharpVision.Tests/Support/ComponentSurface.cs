// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Mounts one control in a real application and exposes its modeled terminal surface.</summary>
internal sealed class ComponentSurface: IAsyncDisposable
{
    private const State _observableStates = State.Hovered | State.Focused | State.Pressed | State.Disabled;
    private readonly Application _application;
    private readonly Control _mounted;
    private readonly ComponentTerminal _terminal;

    private ComponentSurface(Application application, Control mounted, ComponentTerminal terminal)
    {
        _application = application;
        _mounted = mounted;
        _terminal = terminal;
    }

    /// <summary>Mounts one detached control in a positive fixed-size terminal surface.</summary>
    /// <param name="control">The non-null detached control to mount.</param>
    /// <param name="size">The positive terminal surface size.</param>
    /// <param name="cancellationToken">Requests cancellation while the first frame settles.</param>
    /// <returns>The started component surface after its first rendered frame.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A surface dimension is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is attached or already owned.</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="control"/> is disposed.</exception>
    internal static async Task<ComponentSurface> MountAsync(
        Control control,
        Size size,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Height);
        ObjectDisposedException.ThrowIf(control.IsDisposed, control);

        if (control.Parent is not null || control.Dispatcher is not null || control.OwningSlot is not null)
        {
            throw new ArgumentException("The mounted control must be detached and unowned.", nameof(control));
        }

        var host = new Overlay();
        host.Children.Add(control);
        var terminal = new ComponentTerminal(size);
        terminal.QueueResize(new Dimensions(size));
        var application = new Application(host, terminal, terminal, TerminalOptions.Minimal);

        try
        {
            await application.StartAsync(cancellationToken);
            return new ComponentSurface(application, control, terminal);
        }
        catch
        {
            await application.DisposeAsync();
            throw;
        }
    }

    /// <summary>Gets one immutable cell from the current settled terminal surface.</summary>
    /// <param name="point">The zero-based surface coordinate.</param>
    /// <returns>The copied semantic terminal cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="point"/> is outside the surface.</exception>
    internal SurfaceCell Cell(Point point) => _terminal.Screen.Cell(point);

    /// <summary>Asserts the mounted control has exactly the observable expected visual-state flags.</summary>
    /// <param name="control">The mounted control.</param>
    /// <param name="expected">The expected Normal, Hovered, Focused, Pressed, or Disabled flags.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is not mounted by this surface.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expected"/> contains unsupported flags.</exception>
    internal void ShouldHaveState(Control control, State expected)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (!ReferenceEquals(control, _mounted))
        {
            throw new ArgumentException("The state target is not the control mounted by this surface.", nameof(control));
        }

        if ((expected & ~_observableStates) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expected), expected, "The expected state contains unobservable flags.");
        }

        var actual = State.Normal;

        if (control.IsHovered)
        {
            actual |= State.Hovered;
        }

        if (control.IsFocused)
        {
            actual |= State.Focused;
        }

        if (control.IsPressed)
        {
            actual |= State.Pressed;
        }

        if (!control.EffectiveIsEnabled)
        {
            actual |= State.Disabled;
        }

        actual.ShouldBe(expected);
    }

    /// <summary>Asserts exact fixed-size surface text with omitted trailing blanks right-padded.</summary>
    /// <param name="expected">The non-null newline-separated expected rows.</param>
    /// <exception cref="ArgumentNullException"><paramref name="expected"/> is null.</exception>
    /// <exception cref="ArgumentException">The expected text has too many rows or a row is too wide.</exception>
    internal void ShouldRender(string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        var normalized = expected.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var rows = normalized.Split('\n');

        if (rows.Length > _terminal.Screen.Size.Height)
        {
            throw new ArgumentException("The expected snapshot has more rows than the component surface.", nameof(expected));
        }

        var value = new StringBuilder();

        for (var y = 0; y < _terminal.Screen.Size.Height; y++)
        {
            if (y > 0)
            {
                _ = value.Append('\n');
            }

            var row = y < rows.Length ? rows[y] : string.Empty;

            if (row.Length > _terminal.Screen.Size.Width)
            {
                throw new ArgumentException($"Expected row {y} is wider than the component surface.", nameof(expected));
            }

            _ = value.Append(row);
            _ = value.Append(' ', _terminal.Screen.Size.Width - row.Length);
        }

        _terminal.Screen.CopyText().ShouldBe(value.ToString());
    }

    /// <summary>Stops the application and releases its mounted tree and terminal resources.</summary>
    public ValueTask DisposeAsync() => _application.DisposeAsync();
}
