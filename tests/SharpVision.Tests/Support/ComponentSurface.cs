// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Mounts one control in a real application and exposes its modeled terminal surface.</summary>
internal sealed class ComponentSurface: IAsyncDisposable
{
    private const VisualState _observableStates = VisualState.PointerOver | VisualState.Focused | VisualState.Pressed | VisualState.Disabled;
    private readonly Application _application;
    private readonly CancellationToken _cancellationToken;
    private readonly Control _mounted;
    private readonly ComponentTerminal _terminal;

    private ComponentSurface(
        Application application,
        Control mounted,
        ComponentTerminal terminal,
        CancellationToken cancellationToken)
    {
        _application = application;
        _cancellationToken = cancellationToken;
        _mounted = mounted;
        _terminal = terminal;
        Pointer = new ComponentPointer(this);
        Keyboard = new ComponentKeyboard(this);
    }

    /// <summary>Gets the pointer driver that emits real terminal mouse reports.</summary>
    internal ComponentPointer Pointer { get; }

    /// <summary>Gets the keyboard driver that emits real terminal key sequences.</summary>
    internal ComponentKeyboard Keyboard { get; }

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

        var host = new Overlay { Focusable = true };
        host.Children.Add(control);
        var terminal = new ComponentTerminal(size);
        _ = terminal.QueueResize(new Dimensions(size));
        var application = new Application(host, terminal, terminal, TerminalOptions.Minimal);

        try
        {
            await application.StartAsync(cancellationToken);
            var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnIdle(object? sender, EventArgs eventArgs)
            {
                _ = sender;
                _ = eventArgs;
                _ = idle.TrySetResult();
            }

            application.Idle += OnIdle;

            try
            {
                await application.Dispatcher.InvokeAsync(
                    () => application.Focus.Focus(host).ShouldBeTrue(),
                    cancellationToken);
                await idle.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            }
            finally
            {
                application.Idle -= OnIdle;
            }

            return new ComponentSurface(application, control, terminal, cancellationToken);
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

    /// <summary>Asserts exact final terminal cursor position and visibility.</summary>
    /// <param name="position">The in-bounds zero-based expected cursor cell.</param>
    /// <param name="visible">Whether the terminal cursor must be visible.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is outside the current surface.</exception>
    internal void ShouldHaveCursor(Point position, bool visible)
    {
        var screen = _terminal.Screen;
        _ = screen.Cell(position);
        screen.CursorPosition.ShouldBe(position);
        screen.CursorVisible.ShouldBe(visible);
    }

    /// <summary>Resolves a mounted control's deterministic interior point on the UI dispatcher.</summary>
    /// <param name="control">The mounted control or one of its owned descendants.</param>
    /// <returns>The zero-based center point of its non-empty arranged bounds.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is not owned by this surface.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="control"/> has empty arranged bounds.</exception>
    internal async Task<Point> ResolvePointAsync(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return await _application.Dispatcher.InvokeAsync(() =>
        {
            if (!IsOwned(control))
            {
                throw new ArgumentException("The pointer target is not owned by this component surface.", nameof(control));
            }

            var bounds = control.Bounds;

            return bounds.Width > 0 && bounds.Height > 0
                ? new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2))
                : throw new InvalidOperationException("The pointer target has empty arranged bounds.");
        }, _cancellationToken);
    }

    /// <summary>Resolves one validated control-relative cell on the UI dispatcher.</summary>
    /// <param name="control">The mounted control or one of its owned descendants.</param>
    /// <param name="relative">The zero-based cell inside the control's arranged bounds.</param>
    /// <returns>The corresponding zero-based absolute surface cell.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is not owned by this surface.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="control"/> has empty arranged bounds.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="relative"/> lies outside the arranged bounds.</exception>
    internal async Task<Point> ResolvePointAsync(Control control, Point relative)
    {
        ArgumentNullException.ThrowIfNull(control);
        return await _application.Dispatcher.InvokeAsync(() =>
        {
            if (!IsOwned(control))
            {
                throw new ArgumentException("The pointer target is not owned by this component surface.", nameof(control));
            }

            var bounds = control.Bounds;

            return bounds.Width <= 0 || bounds.Height <= 0
                ? throw new InvalidOperationException("The pointer target has empty arranged bounds.")
                : relative.X < 0 || relative.Y < 0 ||
                relative.X >= bounds.Width || relative.Y >= bounds.Height
                ? throw new ArgumentOutOfRangeException(
                    nameof(relative),
                    relative,
                    "The relative pointer cell must lie inside the target's arranged bounds.")
                : new Point(bounds.X + relative.X, bounds.Y + relative.Y);
        }, _cancellationToken);
    }

    /// <summary>Validates and emits one complete terminal input action, then waits for application idle.</summary>
    /// <param name="value">The non-empty complete terminal input sequence.</param>
    /// <param name="description">The non-empty diagnostic action description.</param>
    /// <returns>A task completed after input, routed work, layout, and rendering settle.</returns>
    /// <exception cref="ArgumentException">An input or description is empty.</exception>
    /// <exception cref="TimeoutException">The component application does not settle within two seconds.</exception>
    internal async Task SendAsync(ReadOnlyMemory<byte> value, string description)
    {
        if (value.IsEmpty)
        {
            throw new ArgumentException("Terminal input cannot be empty.", nameof(value));
        }

        ArgumentException.ThrowIfNullOrEmpty(description);
        Task? consumed = null;
        var frames = 0;
        var idles = 0;
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnIdle(object? sender, EventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            _ = Interlocked.Increment(ref idles);

            if (consumed?.IsCompletedSuccessfully == true)
            {
                _ = idle.TrySetResult();
            }
        }

        void OnFrameRendered(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            _ = Interlocked.Increment(ref frames);
        }

        _application.Idle += OnIdle;
        _application.FrameRendered += OnFrameRendered;

        try
        {
            consumed = _terminal.QueueInput(value.Span);
            await consumed.WaitAsync(TimeSpan.FromSeconds(2), _cancellationToken);
            await _application.Dispatcher.InvokeAsync(static () => { }, _cancellationToken);
            await idle.Task.WaitAsync(TimeSpan.FromSeconds(2), _cancellationToken);
        }
        catch (TimeoutException exception)
        {
            var state = await _application.Dispatcher.InvokeAsync(
                () => $"root={_application.Root.Pending}, mounted={_mounted.Pending}",
                _cancellationToken);
            throw new TimeoutException(
                $"Component action '{description}' did not settle after {frames} frames and {idles} idle notifications " +
                $"({state}). Latest surface:{Environment.NewLine}{_terminal.Screen.CopyText()}",
                exception);
        }
        finally
        {
            _application.Idle -= OnIdle;
            _application.FrameRendered -= OnFrameRendered;
        }
    }

    /// <summary>Runs one public control mutation on the application dispatcher and waits for idle.</summary>
    /// <param name="update">The non-null mutation to execute.</param>
    /// <param name="description">The non-empty diagnostic action description.</param>
    /// <returns>A task completed after mutation, layout, rendering, and output settle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="update"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="description"/> is empty.</exception>
    /// <exception cref="TimeoutException">The component application does not settle within two seconds.</exception>
    internal async Task UpdateAsync(System.Action update, string description)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentException.ThrowIfNullOrEmpty(description);
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnIdle(object? sender, EventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            _ = idle.TrySetResult();
        }

        _application.Idle += OnIdle;

        try
        {
            await _application.Dispatcher.InvokeAsync(update, _cancellationToken);
            await idle.Task.WaitAsync(TimeSpan.FromSeconds(2), _cancellationToken);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(
                $"Component action '{description}' did not settle. Latest surface:{Environment.NewLine}{_terminal.Screen.CopyText()}",
                exception);
        }
        finally
        {
            _application.Idle -= OnIdle;
        }
    }

    /// <summary>Queues a positive terminal resize and waits for committed layout and rendering.</summary>
    /// <param name="size">The new positive terminal cell size.</param>
    /// <returns>A task completed after the resized frame reaches the modeled terminal.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive.</exception>
    /// <exception cref="TimeoutException">The component application does not settle within two seconds.</exception>
    internal async Task ResizeAsync(Size size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Height);
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnIdle(object? sender, EventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;

            if (_application.Size == size)
            {
                _ = idle.TrySetResult();
            }
        }

        _application.Idle += OnIdle;

        try
        {
            var consumed = _terminal.QueueResize(new Dimensions(size));
            await consumed.WaitAsync(TimeSpan.FromSeconds(2), _cancellationToken);
            await idle.Task.WaitAsync(TimeSpan.FromSeconds(2), _cancellationToken);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(
                $"Component resize to {size} did not settle. Latest surface:{Environment.NewLine}{_terminal.Screen.CopyText()}",
                exception);
        }
        finally
        {
            _application.Idle -= OnIdle;
        }
    }

    /// <summary>Asserts the mounted control has exactly the observable expected visual-state flags.</summary>
    /// <param name="control">The mounted control or one of its owned descendants.</param>
    /// <param name="expected">The expected Normal, Hovered, Focused, Pressed, or Disabled flags.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is not mounted by this surface.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expected"/> contains unsupported flags.</exception>
    internal void ShouldHaveState(Control control, VisualState expected)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (!IsOwned(control))
        {
            throw new ArgumentException("The state target is not owned by this component surface.", nameof(control));
        }

        if ((expected & ~_observableStates) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expected), expected, "The expected state contains unobservable flags.");
        }

        var actual = VisualState.Normal;

        if (control.IsPointerOver)
        {
            actual |= VisualState.PointerOver;
        }

        if (control.IsFocused)
        {
            actual |= VisualState.Focused;
        }

        if (control.IsPressed)
        {
            actual |= VisualState.Pressed;
        }

        if (!control.EffectiveIsEnabled)
        {
            actual |= VisualState.Disabled;
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
            var measurement = Width.Measure(row, Ambiguous.Narrow);

            if (measurement.Controls > 0)
            {
                throw new ArgumentException($"Expected row {y} contains a terminal control.", nameof(expected));
            }

            if (measurement.Cells > _terminal.Screen.Size.Width)
            {
                throw new ArgumentException($"Expected row {y} is wider than the component surface.", nameof(expected));
            }

            _ = value.Append(row);
            _ = value.Append(' ', _terminal.Screen.Size.Width - measurement.Cells);
        }

        _terminal.Screen.CopyText().ShouldBe(value.ToString());
    }

    /// <summary>Stops the application and releases its mounted tree and terminal resources.</summary>
    public ValueTask DisposeAsync() => _application.DisposeAsync();

    private bool IsOwned(Control control)
    {
        for (var current = control; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, _mounted))
            {
                return true;
            }
        }

        return false;
    }
}
