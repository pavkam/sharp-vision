// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using Input;

/// <summary>Mounts one control in a real application and exposes its modeled terminal surface.</summary>
internal sealed class ComponentSurface: IAsyncDisposable
{
    private const VisualState _observableStates = VisualState.PointerOver |
                                                  VisualState.Focused |
                                                  VisualState.Pressed |
                                                  VisualState.Disabled;

    private readonly CancellationToken _cancellationToken;
    private readonly ManualTimeProvider? _timeProvider;
    private readonly ControlBase _mounted;
    private readonly ComponentTerminal _terminal;

    private ComponentSurface(
        Application application,
        ControlBase mounted,
        ComponentTerminal terminal,
        ManualTimeProvider? timeProvider,
        CancellationToken cancellationToken)
    {
        Application = application;
        _cancellationToken = cancellationToken;
        _mounted = mounted;
        _terminal = terminal;
        _timeProvider = timeProvider;
        Pointer = new ComponentPointer(this);
        Keyboard = new ComponentKeyboard(this);
    }

    /// <summary>Gets the pointer driver that emits real terminal mouse reports.</summary>
    internal ComponentPointer Pointer { get; }

    /// <summary>Gets the keyboard driver that emits real terminal key sequences.</summary>
    internal ComponentKeyboard Keyboard { get; }

    /// <summary>Gets the real application hosting this mounted component.</summary>
    internal Application Application { get; }

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
        ControlBase control,
        Size size,
        CancellationToken cancellationToken) =>
        await MountAsync(control, size, timeProvider: null, cancellationToken);

    /// <summary>Mounts one detached control with an explicit application theme.</summary>
    internal static async Task<ComponentSurface> MountAsync(
        ControlBase control,
        Size size,
        Theme theme,
        CancellationToken cancellationToken) =>
        await MountAsync(control, size, timeProvider: null, TerminalOptions.Minimal, theme, cancellationToken);

    /// <summary>Mounts one detached Screen as the real application root.</summary>
    /// <param name="screen">The non-null detached Screen to bind to the application.</param>
    /// <param name="size">The positive terminal surface size.</param>
    /// <param name="cancellationToken">Requests cancellation while the first frame settles.</param>
    /// <returns>The started screen surface after its startup hook and first committed frame.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="screen"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A surface dimension is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="screen"/> is attached or already owned.</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="screen"/> is disposed.</exception>
    internal static async Task<ComponentSurface> MountScreenAsync(
        Screen screen,
        Size size,
        CancellationToken cancellationToken) =>
        await MountScreenAsync(screen, size, TerminalOptions.Minimal, cancellationToken);

    /// <summary>Mounts one detached Screen with an explicit terminal profile.</summary>
    internal static async Task<ComponentSurface> MountScreenAsync(
        Screen screen,
        Size size,
        TerminalOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Height);
        ObjectDisposedException.ThrowIf(screen.Disposed, screen);

        if (screen.Parent is not null || screen.Dispatcher is not null || screen.OwningSlot is not null)
        {
            throw new ArgumentException("The mounted Screen must be detached and unowned.", nameof(screen));
        }

        var terminal = new ComponentTerminal(size);
        _ = terminal.QueueResize(new Dimensions(size));
        var application = new Application(
            screen,
            terminal,
            terminal,
            options);

        try
        {
            // Screen.Attach runs OnAttach, the documented place for theme publication - which
            // requires the owning dispatcher thread; the dispatcher is already live from the
            // constructor, before StartAsync.
            await application.Dispatcher.InvokeAsync(() => screen.Attach(application), cancellationToken);
            await application.StartAsync(cancellationToken);
            return new ComponentSurface(
                application,
                screen,
                terminal,
                timeProvider: null,
                cancellationToken);
        }
        catch
        {
            await application.DisposeAsync();
            throw;
        }
    }

    /// <summary>Mounts one detached control with a deterministic application clock.</summary>
    /// <param name="control">The non-null detached control to mount.</param>
    /// <param name="size">The positive terminal surface size.</param>
    /// <param name="timeProvider">The non-null deterministic application clock.</param>
    /// <param name="cancellationToken">Requests cancellation while the first frame settles.</param>
    /// <returns>The started component surface after its first rendered frame.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A surface dimension is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is attached or already owned.</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="control"/> is disposed.</exception>
    internal static async Task<ComponentSurface> MountAsync(
        ControlBase control,
        Size size,
        ManualTimeProvider? timeProvider,
        CancellationToken cancellationToken) =>
        await MountAsync(control, size, timeProvider, TerminalOptions.Minimal, theme: null, cancellationToken);

    /// <summary>Mounts one detached control with an explicit terminal profile and mode policy.</summary>
    internal static async Task<ComponentSurface> MountAsync(
        ControlBase control,
        Size size,
        TerminalOptions options,
        CancellationToken cancellationToken) =>
        await MountAsync(control, size, timeProvider: null, options, theme: null, cancellationToken);

    /// <summary>Mounts one detached control with an explicit terminal profile and application theme.</summary>
    internal static async Task<ComponentSurface> MountAsync(
        ControlBase control,
        Size size,
        TerminalOptions options,
        Theme theme,
        CancellationToken cancellationToken) =>
        await MountAsync(control, size, timeProvider: null, options, theme, cancellationToken);

    private static async Task<ComponentSurface> MountAsync(
        ControlBase control,
        Size size,
        ManualTimeProvider? timeProvider,
        TerminalOptions options,
        Theme? theme,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Height);
        ObjectDisposedException.ThrowIf(control.Disposed, control);

        if (control.Parent is not null || control.Dispatcher is not null || control.OwningSlot is not null)
        {
            throw new ArgumentException("The mounted control must be detached and unowned.", nameof(control));
        }

        var host = new Overlay { Focusable = true };
        host.Children.Add(control);
        var terminal = new ComponentTerminal(size);
        _ = terminal.QueueResize(new Dimensions(size));
        var application = new Application(
            host,
            terminal,
            terminal,
            options,
            timeProvider: timeProvider);
        if (theme is not null)
        {
            // Application.Theme requires the owning dispatcher thread; the dispatcher is already
            // live from the constructor, before StartAsync.
            await application.Dispatcher.InvokeAsync(() => { application.Theme = theme; }, cancellationToken);
        }

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

                // The Idle event fires the instant the dispatcher queue drains, with no debounce,
                // so this timeout only ever backstops a genuine hang - it never bounds a passing run.
                await idle.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (TimeoutException exception)
            {
                string state;

                try
                {
                    // The diagnostic read must never outlive the failure it reports: if the mount
                    // hung because the dispatcher itself is wedged, InvokeAsync would otherwise wait
                    // forever and turn a bounded timeout into an unbounded one.
                    state = await application.Dispatcher.InvokeAsync(
                        () => $"root={host.Pending}, mounted={control.Pending}",
                        cancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (TimeoutException)
                {
                    state = "dispatcher unresponsive";
                }

                throw new TimeoutException(
                    $"Component mount did not settle ({state}). Latest surface:{Environment.NewLine}{terminal.Screen.CopyText()}",
                    exception);
            }
            finally
            {
                application.Idle -= OnIdle;
            }

            return new ComponentSurface(
                application,
                control,
                terminal,
                timeProvider,
                cancellationToken);
        }
        catch
        {
            await application.DisposeAsync();
            throw;
        }
    }

    /// <summary>Advances the deterministic application clock and waits for resulting work to settle.</summary>
    /// <param name="value">The non-negative duration to advance.</param>
    /// <param name="description">The non-empty diagnostic action description.</param>
    /// <returns>A task completed after timer work, rendering, and output settle.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="description"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The surface has no deterministic clock.</exception>
    /// <exception cref="TimeoutException">The component application does not settle within two seconds.</exception>
    internal async Task AdvanceAsync(TimeSpan value, string description)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
        ArgumentException.ThrowIfNullOrEmpty(description);
        var timeProvider = _timeProvider ?? throw new InvalidOperationException(
            "The component surface was mounted without a deterministic clock.");
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnIdle(object? sender, EventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            _ = idle.TrySetResult();
        }

        Application.Idle += OnIdle;

        try
        {
            // Advance on the dispatcher, not on the calling thread. ManualTimeProvider walks each
            // due timer in turn and publishes that timer's own due time as the current timestamp
            // before invoking it, only settling on the final value once the walk finishes. Timer
            // callbacks marshal their work onto the dispatcher, and a control that measures its
            // own elapsed time samples the clock when that work runs, not when the callback fired.
            // Advancing from the calling thread therefore let the dispatcher observe an
            // intermediate timestamp and compute a short interval - an animation would advance by
            // part of a step while the surface reported itself settled. Running the advance as
            // dispatcher work puts every write and every read of the clock on one thread, so the
            // interval a callback observes is always the whole requested duration.
            await Application.Dispatcher.InvokeAsync(
                () => timeProvider.Advance(value),
                _cancellationToken);
            await Application.Dispatcher.InvokeAsync(static () => { }, _cancellationToken);
            await idle.Task.WaitAsync(TimeSpan.FromSeconds(2), _cancellationToken);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(
                $"Component clock action '{description}' did not settle. Latest surface:{Environment.NewLine}{_terminal.Screen.CopyText()}",
                exception);
        }
        finally
        {
            Application.Idle -= OnIdle;
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

    /// <summary>Asserts exact final cursor position, visibility, and modeled shape.</summary>
    internal void ShouldHaveCursor(Point position, bool visible, CursorShape shape)
    {
        ShouldHaveCursor(position, visible);
        _terminal.Screen.CursorShape.ShouldBe(shape);
    }

    /// <summary>Resolves a mounted control's deterministic interior point on the UI dispatcher.</summary>
    /// <param name="control">The mounted control or one of its owned descendants.</param>
    /// <returns>The zero-based center point of its non-empty arranged bounds.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is not owned by this surface.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="control"/> has empty arranged bounds.</exception>
    internal async Task<Point> ResolvePointAsync(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return await Application.Dispatcher.InvokeAsync(() =>
        {
            if (!IsOwned(control))
            {
                throw new ArgumentException("The pointer target is not owned by this component surface.",
                    nameof(control));
            }

            var bounds = control.Bounds;

            return bounds is { Width: > 0, Height: > 0 }
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
    internal async Task<Point> ResolvePointAsync(ControlBase control, Point relative)
    {
        ArgumentNullException.ThrowIfNull(control);
        return await Application.Dispatcher.InvokeAsync(() =>
        {
            if (!IsOwned(control))
            {
                throw new ArgumentException("The pointer target is not owned by this component surface.",
                    nameof(control));
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

        Application.Idle += OnIdle;
        Application.FrameRendered += OnFrameRendered;

        try
        {
            consumed = _terminal.QueueInput(value.Span);
            await consumed.WaitAsync(TimeSpan.FromSeconds(2), _cancellationToken);
            await Application.Dispatcher.InvokeAsync(static () => { }, _cancellationToken);
            await idle.Task.WaitAsync(TimeSpan.FromSeconds(2), _cancellationToken);
        }
        catch (TimeoutException exception)
        {
            var state = await Application.Dispatcher.InvokeAsync(
                () => $"root={Application.Root.Pending}, mounted={_mounted.Pending}",
                _cancellationToken);
            throw new TimeoutException(
                $"Component action '{description}' did not settle after {frames} frames and {idles} idle notifications " +
                $"({state}). Latest surface:{Environment.NewLine}{_terminal.Screen.CopyText()}",
                exception);
        }
        finally
        {
            Application.Idle -= OnIdle;
            Application.FrameRendered -= OnFrameRendered;
        }
    }

    /// <summary>Runs one public control mutation on the application dispatcher and waits for idle.</summary>
    /// <param name="update">The non-null mutation to execute.</param>
    /// <param name="description">The non-empty diagnostic action description.</param>
    /// <returns>A task completed after mutation, layout, rendering, and output settle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="update"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="description"/> is empty.</exception>
    /// <exception cref="TimeoutException">The component application does not settle within two seconds.</exception>
    internal async Task UpdateAsync(Action update, string description)
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

        Application.Idle += OnIdle;

        try
        {
            await Application.Dispatcher.InvokeAsync(update, _cancellationToken);
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
            Application.Idle -= OnIdle;
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

            if (Application.Size == size)
            {
                _ = idle.TrySetResult();
            }
        }

        Application.Idle += OnIdle;

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
            Application.Idle -= OnIdle;
        }
    }

    /// <summary>Asserts the mounted control has exactly the observable expected visual-state flags.</summary>
    /// <param name="control">The mounted control or one of its owned descendants.</param>
    /// <param name="expected">The expected Normal, Hovered, Focused, Pressed, or Disabled flags.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is not mounted by this surface.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expected"/> contains unsupported flags.</exception>
    internal void ShouldHaveState(ControlBase control, VisualState expected)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (!IsOwned(control))
        {
            throw new ArgumentException("The state target is not owned by this component surface.", nameof(control));
        }

        if ((expected & ~_observableStates) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expected), expected,
                "The expected state contains unobservable flags.");
        }

        var actual = VisualState.Normal;

        if (control.PointerOver)
        {
            actual |= VisualState.PointerOver;
        }

        if (control.Focused)
        {
            actual |= VisualState.Focused;
        }

        if (control.Pressed)
        {
            actual |= VisualState.Pressed;
        }

        if (!control.EffectiveIsEnabled)
        {
            actual |= VisualState.Disabled;
        }

        actual.ShouldBe(expected);
    }

    /// <summary>Asserts the exact public control that owns application keyboard focus.</summary>
    /// <param name="expected">The owned control expected to own focus, or null.</param>
    /// <exception cref="ArgumentException"><paramref name="expected"/> is not owned by this surface.</exception>
    internal void ShouldHaveFocus(ControlBase? expected)
    {
        if (expected is not null && !IsOwned(expected))
        {
            throw new ArgumentException("The focus target is not owned by this component surface.", nameof(expected));
        }

        Application.Focus.Focused.ShouldBeSameAs(expected);
    }

    /// <summary>Asserts the exact public control that owns application pointer capture.</summary>
    /// <param name="expected">The owned control expected to own capture, or null.</param>
    /// <exception cref="ArgumentException"><paramref name="expected"/> is not owned by this surface.</exception>
    internal void ShouldHaveCapture(ControlBase? expected)
    {
        if (expected is not null && !IsOwned(expected))
        {
            throw new ArgumentException("The capture target is not owned by this component surface.", nameof(expected));
        }

        Application.Capture.Captured.ShouldBeSameAs(expected);
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
            throw new ArgumentException("The expected snapshot has more rows than the component surface.",
                nameof(expected));
        }

        var value = new StringBuilder();

        for (var y = 0; y < _terminal.Screen.Size.Height; y++)
        {
            if (y > 0)
            {
                _ = value.Append('\n');
            }

            var row = y < rows.Length ? rows[y] : string.Empty;
            var measurement = Width.Measure(row);

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
    public ValueTask DisposeAsync() => Application.DisposeAsync();

    private bool IsOwned(ControlBase control)
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
