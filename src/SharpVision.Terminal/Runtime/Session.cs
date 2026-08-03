// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using Backends;

using Capabilities;

using Xterm;

using MultiplexerRoute = Multiplexing.MultiplexerRoute;

/// <summary>
/// Owns terminal mode leases and serializes input, resize, closure, and cleanup.
/// </summary>
[DebuggerDisplay("Session {_options}")]
[PublicAPI]
public sealed class Session: IAsyncDisposable
{
    private readonly ITransport _transport;
    private readonly IResizeSource _resize;
    private readonly ISink _sink;
    private readonly Options _options;
    private readonly TimeProvider _timeProvider;
    private readonly Interpreter _programInterpreter;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<Lease> _leases = [];
    private readonly Lock _lifecycle = new();
    private static readonly AsyncLocal<bool> _dispatchingFromRun = new();
    private TerminalContext _context;
    private TaskCompletionSource? _completion;
    private Task? _disposal;
    private bool _disposed;
    private bool _running;

    #region Construction and lifecycle

    /// <summary>Initializes a session that owns transport and resize-source disposal.</summary>
    /// <param name="transport">The non-null terminal transport.</param>
    /// <param name="resize">The non-null resize source.</param>
    /// <param name="sink">The non-null ordered event sink.</param>
    /// <param name="options">Validated policy, or null for defaults.</param>
    /// <param name="timeProvider">Cleanup clock, or null for system time.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="NotSupportedException">The terminal profile is not suitable for full-screen use.</exception>
    public Session(
        ITransport transport,
        IResizeSource resize,
        ISink sink,
        Options? options = null,
        TimeProvider? timeProvider = null) : this(
        transport,
        resize,
        sink,
        options ?? new Options(),
        timeProvider,
        context: null)
    {
    }

    /// <summary>
    /// Initializes a session with the terminal context already resolved by its owning application.
    /// </summary>
    /// <param name="transport">The non-null terminal transport.</param>
    /// <param name="resize">The non-null resize source.</param>
    /// <param name="sink">The non-null ordered event sink.</param>
    /// <param name="options">The non-null validated policy.</param>
    /// <param name="context">The non-null application-owned terminal context.</param>
    /// <param name="timeProvider">Cleanup clock, or null for system time.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="context"/> does not contain the exact profile from <paramref name="options"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">The terminal profile is not suitable for full-screen use.</exception>
    internal Session(
        ITransport transport,
        IResizeSource resize,
        ISink sink,
        Options options,
        TerminalContext context,
        TimeProvider? timeProvider = null) : this(
        transport,
        resize,
        sink,
        RequireOptions(options),
        timeProvider,
        RequireContext(context))
    {
    }

    private Session(
        ITransport transport,
        IResizeSource resize,
        ISink sink,
        Options resolvedOptions,
        TimeProvider? timeProvider,
        TerminalContext? context)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(resize);
        ArgumentNullException.ThrowIfNull(sink);

        if (resolvedOptions.Profile.Description.Suitability != Suitability.Usable)
        {
            throw new NotSupportedException(
                $"Terminal description '{resolvedOptions.Profile.Description.Name}' is not suitable for full-screen use.");
        }

        if (context is not null && !ReferenceEquals(context.Profile, resolvedOptions.Profile))
        {
            throw new ArgumentException(
                "The terminal context must contain the exact profile from the session options.",
                nameof(context));
        }

        _transport = transport;
        _resize = resize;
        _sink = sink;
        _options = resolvedOptions;
        _context = context ?? resolvedOptions.CreateContext();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _programInterpreter = new Interpreter(resolvedOptions.Input.ProgramLimits);
    }

    /// <summary>Gets the first cleanup failure without replacing the primary failure.</summary>
    public Exception? LastCleanupException { get; private set; }

    /// <summary>Gets the fixed terminal backend identity selected when this session was created.</summary>
    internal TerminalBackend Backend => _context.Backend;

    /// <summary>Runs startup, ordered event delivery, and guaranteed reverse cleanup.</summary>
    /// <remarks>
    /// Claiming the run slot and observing disposal are one atomic step. A run that starts
    /// therefore always reaches reverse cleanup with a live transport, and a run requested after
    /// <see cref="DisposeAsync"/> has begun is rejected from the guard rather than failing later
    /// from inside the loop against already-disposed lifetime state.
    /// </remarks>
    /// <param name="cancellationToken">Cancels pending reads and resize waits.</param>
    /// <returns>The complete session operation.</returns>
    /// <exception cref="InvalidOperationException">The session is already running.</exception>
    /// <exception cref="ObjectDisposedException">The session is disposed or disposal has begun.</exception>
    public async ValueTask RunAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource completion;

        lock (_lifecycle)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_running)
            {
                throw new InvalidOperationException("The terminal session is already running.");
            }

            // Publishing the completion source under the same lock that disposal takes is what
            // makes reverse cleanup ordered: a concurrent DisposeAsync either sees this run and
            // waits for it, or wins the lock and this call never starts.
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = completion;
            _running = true;
        }

        Exception? primary = null;
        LastCleanupException = null;

        // Set for the duration of the run so DisposeAsync can detect - and refuse - being awaited
        // from inside a sink callback this same run raised, which would otherwise deadlock waiting
        // for the run to complete from inside itself (see #229). AsyncLocal only flows to this
        // call's own descendants, so an unrelated caller disposing the session never observes it.
        _dispatchingFromRun.Value = true;

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetime.Token);
            await StartAsync(linked.Token).ConfigureAwait(false);
            await EventsAsync(linked.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            primary = exception;

            if (exception is not OperationCanceledException)
            {
                try
                {
                    _sink.Fault(exception);
                }
                catch (Exception notificationException)
                {
                    LastCleanupException = notificationException;
                }
            }
        }
        finally
        {
            await CleanupAsync().ConfigureAwait(false);

            lock (_lifecycle)
            {
                _completion = null;
                _running = false;
            }

            _dispatchingFromRun.Value = false;

            // Signalled only after every reverse-mode byte was written and flushed, so a waiting
            // DisposeAsync can safely tear down the transport from here on.
            _ = completion.TrySetResult();
        }

        if (primary is not null)
        {
            ExceptionDispatchInfo.Capture(primary).Throw();
        }

        if (LastCleanupException is not null)
        {
            ExceptionDispatchInfo.Capture(LastCleanupException).Throw();
        }
    }

    /// <summary>
    /// Cancels active work, awaits reverse-mode cleanup, and disposes the owned resize source,
    /// transport, and lifetime state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reverse mode restoration writes through the transport, so disposal cannot tear the
    /// transport down while a run is still unwinding its mode leases. This call cancels the
    /// session lifetime, waits for the active run to finish writing and flushing every disable
    /// sequence, and only then releases owned resources. A caller that never started a run, or
    /// whose run already completed, observes no additional wait.
    /// </para>
    /// <para>
    /// Disposal is idempotent and safe to call concurrently. Every caller awaits the same
    /// underlying teardown and returns only after it finished, so no caller observes a
    /// half-disposed session. Reverse cleanup runs exactly once.
    /// </para>
    /// <para>
    /// This call must not be awaited from an <see cref="ISink"/> callback raised by its own run.
    /// Doing so asks the run to complete from inside itself, which would otherwise deadlock;
    /// this is detected and rejected with <see cref="InvalidOperationException"/> instead.
    /// Dispose the session from the code that owns <see cref="RunAsync"/> instead.
    /// </para>
    /// </remarks>
    /// <returns>The complete teardown operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Called from inside an <see cref="ISink"/> callback raised by this session's own run.
    /// </exception>
    public async ValueTask DisposeAsync()
    {
        if (_dispatchingFromRun.Value)
        {
            throw new InvalidOperationException(
                "DisposeAsync must not be awaited from an ISink callback raised by this session's " +
                "own run - that asks the run to complete from inside itself. Dispose the session " +
                "from the code that owns RunAsync instead.");
        }

        TaskCompletionSource? owner = null;
        Task disposal;

        lock (_lifecycle)
        {
            // Marking disposal before releasing the lock closes the window in which a new run
            // could start against lifetime state this call is about to dispose.
            _disposed = true;

            if (_disposal is null)
            {
                owner = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _disposal = owner.Task;
            }

            disposal = _disposal;
        }

        if (owner is null)
        {
            // A joining caller still waits for the single teardown to finish, but disposal is
            // idempotent: only the caller that performed it reports the failure, so this
            // continuation waits without rethrowing.
            await disposal.ContinueWith(
                static _ =>
                {
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default).ConfigureAwait(false);

            return;
        }

        try
        {
            await DisposeCoreAsync().ConfigureAwait(false);
            owner.SetResult();
        }
        catch (Exception exception)
        {
            owner.SetException(exception);

            // No joiner may ever await the shared task. The owner reports the failure by
            // rethrowing, so observe the copy that task retains.
            _ = owner.Task.Exception;

            throw;
        }
    }

    private async ValueTask DisposeCoreAsync()
    {
        // Every owned resource is attempted exactly once in the documented order even when an
        // earlier one throws. Abandoning the rest would leak the transport streams, handles, and
        // buffered output that the failing resource has nothing to do with.
        Exception? primary = null;

        try
        {
            _lifetime.Cancel();
        }
        catch (Exception exception)
        {
            primary = exception;
        }

        Task? completion;

        lock (_lifecycle)
        {
            completion = _completion?.Task;
        }

        if (completion is not null)
        {
            // RunAsync fulfils this only after CleanupAsync wrote and flushed every disable
            // sequence, which is the boundary that keeps the terminal restorable. The wait is
            // already bounded: the event loop drains its read within the cleanup budget and
            // CleanupAsync writes under its own finite timeout.
            await completion.ConfigureAwait(false);
        }

        try
        {
            await _resize.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            primary ??= exception;
        }

        try
        {
            await _transport.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            primary ??= exception;
        }

        try
        {
            _lifetime.Dispose();
        }
        catch (Exception exception)
        {
            primary ??= exception;
        }

        if (primary is not null)
        {
            ExceptionDispatchInfo.Capture(primary).Throw();
        }
    }

    #endregion

    #region Mode startup

    private async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_leases.Count == 0, "A new session run starts without retained terminal-mode leases.");

        if (_options.AlternateScreen &&
            TryCreateDescriptionLease(CapabilityNames.Smcup, CapabilityNames.Rmcup, out var alternateScreen))
        {
            await EnableAsync(alternateScreen, cancellationToken).ConfigureAwait(false);
        }

        if (_options.HideCursor &&
            TryCreateDescriptionLease(CapabilityNames.Civis, CapabilityNames.Cnorm, out var cursor))
        {
            await EnableAsync(cursor, cancellationToken).ConfigureAwait(false);
        }

        if (_context.Profile.KeyMap.RequiresApplicationMode &&
            TryCreateDescriptionLease(CapabilityNames.Smkx, CapabilityNames.Rmkx, out var keypad))
        {
            await EnableAsync(keypad, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask EnableOptionalAsync(
        TerminalCapabilities capabilities,
        CancellationToken cancellationToken)
    {
        Debug.Assert(capabilities is not null, "Session options always supply an immutable capability profile.");

        if (_options.Focus && IsPermitted(capabilities.FocusReporting))
        {
            await EnableAsync(CreateFocusLease(), cancellationToken).ConfigureAwait(false);
        }

        if (_options.Paste && IsPermitted(capabilities.BracketedPaste))
        {
            await EnableAsync(CreatePasteLease(), cancellationToken).ConfigureAwait(false);
        }

        var mouse = _options.Coordinates == MouseCoordinates.Pixel
            ? capabilities.PixelMouse
            : capabilities.CellMouse;

        if (_options.Tracking.HasValue && MouseSupported(capabilities) && IsPermitted(mouse))
        {
            await EnableAsync(CreateMouseLease(), cancellationToken).ConfigureAwait(false);
        }

        if (_options.Keyboard.HasValue && IsPermitted(capabilities.KittyKeyboard))
        {
            await EnableAsync(CreateKeyboardLease(), cancellationToken).ConfigureAwait(false);
        }
        else if (_options.ModifyOtherKeys.HasValue && IsPermitted(capabilities.XtermKeyboard))
        {
            await EnableAsync(CreateModifyOtherKeysLease(), cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask EnableAsync(Lease lease, CancellationToken cancellationToken)
    {
        Debug.Assert(!lease.Enable.IsEmpty, "A terminal-mode lease always owns enable bytes.");
        Debug.Assert(!lease.Disable.IsEmpty, "A terminal-mode lease always owns cleanup bytes.");

        // A transport may write a prefix before reporting failure or cancellation.
        // Record the exact reverse action first so cleanup conservatively repairs
        // every attempted acquisition without guessing what reached the terminal.
        _leases.Add(lease);
        await WriteAsync(lease.Enable, cancellationToken).ConfigureAwait(false);
    }

    private bool TryCreateDescriptionLease(
        string enableName,
        string disableName,
        out Lease lease)
    {
        var programs = _context.Profile.Programs;

        if (!programs.TryExpandPair(
                enableName,
                disableName,
                _programInterpreter,
                out var enable,
                out var disable))
        {
            lease = default;
            return false;
        }

        lease = new Lease(enable.Span, disable.Span);
        return true;
    }

    private static bool IsPermitted(Feature feature) => feature.IsAuthoritative;

    private static Lease CreateFocusLease()
    {
        var enable = new ArrayBufferWriter<byte>();
        var disable = new ArrayBufferWriter<byte>();
        ProtocolModes.FocusReporting(new Writer(enable), enabled: true);
        ProtocolModes.FocusReporting(new Writer(disable), enabled: false);
        return new Lease(enable.WrittenSpan, disable.WrittenSpan);
    }

    private static Lease CreatePasteLease()
    {
        var enable = new ArrayBufferWriter<byte>();
        var disable = new ArrayBufferWriter<byte>();
        ProtocolModes.BracketedPaste(new Writer(enable), enabled: true);
        ProtocolModes.BracketedPaste(new Writer(disable), enabled: false);
        return new Lease(enable.WrittenSpan, disable.WrittenSpan);
    }

    private Lease CreateMouseLease()
    {
        Debug.Assert(_options.Tracking.HasValue, "A mouse lease requires configured tracking.");
        var enable = new ArrayBufferWriter<byte>();
        var disable = new ArrayBufferWriter<byte>();
        ProtocolModes.Mouse(
            new Writer(enable),
            _options.Tracking.Value,
            _options.Coordinates,
            enabled: true);
        ProtocolModes.Mouse(
            new Writer(disable),
            _options.Tracking.Value,
            _options.Coordinates,
            enabled: false);
        return new Lease(enable.WrittenSpan, disable.WrittenSpan);
    }

    private Lease CreateKeyboardLease()
    {
        Debug.Assert(_options.Keyboard.HasValue, "A keyboard lease requires configured enhancement flags.");
        var enable = new ArrayBufferWriter<byte>();
        var disable = new ArrayBufferWriter<byte>();
        Kitty.Keyboard.KittyKeyboard.Push(new Writer(enable), _options.Keyboard.Value);
        Kitty.Keyboard.KittyKeyboard.Pop(new Writer(disable));
        return new Lease(enable.WrittenSpan, disable.WrittenSpan);
    }

    private Lease CreateModifyOtherKeysLease()
    {
        Debug.Assert(_options.ModifyOtherKeys.HasValue, "An xterm keyboard lease requires a configured level.");
        var enable = new ArrayBufferWriter<byte>();
        var disable = new ArrayBufferWriter<byte>();
        XtermModifyOtherKeys.Set(new Writer(enable), _options.ModifyOtherKeys.Value);
        XtermModifyOtherKeys.Restore(new Writer(disable));
        return new Lease(enable.WrittenSpan, disable.WrittenSpan);
    }

    #endregion

    #region Event loop

    private async ValueTask EventsAsync(CancellationToken cancellationToken)
    {
        var inputOptions = (_options.Input with
        {
            PixelMouse = _options.Coordinates == MouseCoordinates.Pixel
        }).WithKeyMap(
            _context.Profile.KeyMap,
            _context.Profile.UsesAnsiKeyGrammar);
        var candidateRoute = _options.Negotiation?.Multiplexing.Allows(
            Multiplexing.Operation.CapabilityQueries) == true
            ? new MultiplexerRoute(_options.Negotiation.Multiplexing)
            : null;
        var multiplexerRoute = candidateRoute?.CanRouteCapabilityQueries == true
            ? candidateRoute
            : null;
        var negotiationBaseline = multiplexerRoute?.Policy.OuterProfile?.Capabilities ??
                                  _context.Profile.Capabilities;
        var negotiator = _options.Negotiation is null
            ? null
            : new Negotiator(
                _options.Negotiation,
                negotiationBaseline,
                _timeProvider);
        IProtocolSink routeSink = negotiator is null
            ? _sink
            : new NegotiationSink(_sink, negotiator);
        using var router = multiplexerRoute is null
            ? new ProtocolRouter(routeSink, inputOptions, _timeProvider)
            : new ProtocolRouter(routeSink, multiplexerRoute, inputOptions, _timeProvider);
        Dimensions? localDimensions = null;

        if (_resize.TryReadCurrent(out var currentDimensions))
        {
            localDimensions = currentDimensions;
            router.SetGeometry(currentDimensions.Cells, currentDimensions.Pixels);
        }

        if (negotiator is null)
        {
            await EnableOptionalAsync(_context.Profile.Capabilities, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            var queries = new ArrayBufferWriter<byte>();
            var started = negotiator.TryStart(
                queries,
                localDimensions?.Cells,
                localDimensions?.Pixels,
                multiplexerRoute);

            if (started)
            {
                await _transport.WriteAsync(queries.WrittenMemory, cancellationToken)
                    .ConfigureAwait(false);
                await _transport.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var capabilities = negotiator.Capabilities;
                _context = _context.WithCapabilities(capabilities);
                _sink.Profile(capabilities);
            }
        }

        var buffer = ArrayPool<byte>.Shared.Rent(_options.ReadBufferSize);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var deadline = negotiator is null || negotiator.IsComplete
            ? null
            : DelayUntilAsync(negotiator.Deadline, linked.Token);
        var ready = negotiator is null || negotiator.IsComplete;
        var hasPendingResize = false;
        var pendingResize = default(Dimensions);

        // A source that only ever reports changes from ReadAsync (never seeding an initial
        // observation itself) must still unblock Application's readiness gate, which is driven
        // exclusively by ISink.Resize. Route the synchronous TryReadCurrent snapshot through the
        // same path as an ordinary resize event instead of leaving it feeding the router alone.
        if (localDimensions is { } snapshot)
        {
            if (ready)
            {
                _sink.Resize(in snapshot);
            }
            else
            {
                pendingResize = snapshot;
                hasPendingResize = true;
            }
        }

        // The loop tasks are declared outside the try so cleanup can drain the read
        // that still borrows the rented buffer and observe every abandoned task.
        Task<int>? read = null;
        Task<Dimensions>? resize = null;
        var preferResize = false;

        // Capability publication is the startup barrier. Input may refine the
        // negotiator immediately, while only the newest pre-publication resize
        // is retained so controls never observe geometry under a stale profile.
        try
        {
            read = _transport.ReadAsync(
                buffer.AsMemory(0, _options.ReadBufferSize),
                linked.Token).AsTask();
            resize = _resize.ReadAsync(linked.Token).AsTask();

            while (true)
            {
                // Task.WhenAny returns whichever awaited task the runtime observes complete
                // first; when read and resize are both already complete at the moment it is
                // called (a transport or resize source that completes synchronously), it always
                // resolved to whichever was passed first — read here — regardless of the other's
                // readiness. A synchronous read burst could then monopolize the loop forever,
                // starving resize and an elapsed negotiation deadline (see #21). Re-checking the
                // ready set explicitly on every iteration, instead of trusting WhenAny's pick
                // among already-completed tasks, restores bounded fairness: an elapsed deadline
                // always wins outright, and read/resize alternate when both are simultaneously
                // ready, so neither can starve the other for more than one iteration.
                var deadlineReady = deadline is not null && deadline.IsCompleted;
                Task completed;

                if (!deadlineReady && !read.IsCompleted && !resize.IsCompleted)
                {
                    completed = deadline is null
                        ? await Task.WhenAny(read, resize).ConfigureAwait(false)
                        : await Task.WhenAny(read, resize, deadline).ConfigureAwait(false);
                }
                else
                {
                    completed = deadlineReady switch
                    {
                        true => deadline!,
                        false when read.IsCompleted && resize.IsCompleted => preferResize ? resize : read,
                        false when read.IsCompleted => read,
                        _ => resize
                    };

                    if (read.IsCompleted && resize.IsCompleted)
                    {
                        preferResize = !preferResize;
                    }
                }

                if (deadline is not null && ReferenceEquals(completed, deadline))
                {
                    await deadline.ConfigureAwait(false);

                    // A timer callback may arrive before its provider's wall clock
                    // reaches the requested deadline. Wait out that remainder before
                    // reading the profile that Expire publishes.
                    if (!negotiator!.Expire())
                    {
                        deadline = DelayUntilAsync(negotiator.Deadline, linked.Token);
                        continue;
                    }

                    var capabilities = negotiator.Capabilities;
                    _context = _context.WithCapabilities(capabilities);
                    _sink.Profile(capabilities);
                    await EnableOptionalAsync(capabilities, linked.Token)
                        .ConfigureAwait(false);
                    ready = true;
                    deadline = null;

                    if (hasPendingResize)
                    {
                        _sink.Resize(in pendingResize);
                        hasPendingResize = false;
                    }

                    continue;
                }

                if (ReferenceEquals(completed, resize))
                {
                    var dimensions = await resize.ConfigureAwait(false);
                    router.SetGeometry(dimensions.Cells, dimensions.Pixels);

                    if (ready)
                    {
                        _sink.Resize(in dimensions);
                    }
                    else
                    {
                        pendingResize = dimensions;
                        hasPendingResize = true;
                    }

                    resize = _resize.ReadAsync(linked.Token).AsTask();
                    continue;
                }

                var count = await read.ConfigureAwait(false);
                Debug.Assert((uint) count <= (uint) _options.ReadBufferSize,
                    "Transport reads fit the rented session buffer.");

                if (count == 0)
                {
                    router.Complete();

                    if (!ready)
                    {
                        _ = negotiator!.Complete();
                        var capabilities = negotiator.Capabilities;
                        _context = _context.WithCapabilities(capabilities);
                        _sink.Profile(capabilities);
                        ready = true;
                        deadline = null;

                        if (hasPendingResize)
                        {
                            _sink.Resize(in pendingResize);
                            hasPendingResize = false;
                        }
                    }
                    else if (resize.IsCompleted)
                    {
                        // A resize that becomes ready in the same tick as this EOF read must
                        // still be observed. Once ready, resize events forward immediately
                        // rather than buffering into hasPendingResize/pendingResize above, so
                        // without this check a resize that only ever wins a tie against a
                        // closing read would be silently dropped by the early return below (see #21).
                        var dimensions = await resize.ConfigureAwait(false);
                        router.SetGeometry(dimensions.Cells, dimensions.Pixels);
                        _sink.Resize(in dimensions);
                    }

                    _sink.Closed();
                    return;
                }

                router.Route(buffer.AsSpan(0, count));

                Debug.Assert(ready || negotiator is not null, "Incomplete startup always owns a negotiator.");

                if (!ready && negotiator!.IsComplete)
                {
                    var capabilities = negotiator.Capabilities;
                    _context = _context.WithCapabilities(capabilities);
                    _sink.Profile(capabilities);
                    await EnableOptionalAsync(capabilities, linked.Token)
                        .ConfigureAwait(false);
                    ready = true;
                    deadline = null;

                    if (hasPendingResize)
                    {
                        _sink.Resize(in pendingResize);
                        hasPendingResize = false;
                    }
                }

                read = _transport.ReadAsync(
                    buffer.AsMemory(0, _options.ReadBufferSize),
                    linked.Token).AsTask();
            }
        }
        finally
        {
            linked.Cancel();

            // ITransport borrows the read destination until the returned operation completes,
            // and cancellation is only a request. A sink failure, an optional-mode failure, or
            // a transport whose cancellation completes asynchronously can therefore reach this
            // point with the read still owning the rental. Returning it here would let the pool
            // reissue storage a live read can still fill, and clearArray would write zeroes into
            // storage that read is still filling. Drain first, and permanently abandon the array
            // when a non-cooperative transport outlives the bounded shutdown budget.
            Observe(resize);
            Observe(deadline);

            if (await DrainAsync(read).ConfigureAwait(false))
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }
    }

    private async ValueTask<bool> DrainAsync(Task<int>? read)
    {
        // Awaits the outstanding read to terminal completion and reports whether the rented
        // input buffer is free again.
        if (read is null)
        {
            return true;
        }

        if (!read.IsCompleted)
        {
            // Shutdown stays bounded by the cleanup timeout. A transport that neither completes
            // nor honors cancellation within that budget permanently costs one pooled array,
            // which is strictly cheaper than publishing storage it can still write into.
            using var expiry = new CancellationTokenSource();
            var budget = Task.Delay(_options.CleanupTimeout, _timeProvider, expiry.Token);
            var completed = await Task.WhenAny(read, budget).ConfigureAwait(false);
            await expiry.CancelAsync().ConfigureAwait(false);
            Observe(budget);

            if (!ReferenceEquals(completed, read))
            {
                Observe(read);
                return false;
            }
        }

        Observe(read);
        return true;
    }

    private static void Observe(Task? task)
    {
        // The event loop drops its read, resize, and deadline tasks on every exit path. An
        // unobserved faulted task would otherwise surface much later as a process-wide
        // TaskScheduler.UnobservedTaskException unrelated to the real failure.
        if (task is null)
        {
            return;
        }

        if (task.IsCompleted)
        {
            _ = task.Exception;
            return;
        }

        _ = task.ContinueWith(
            static completed =>
            {
                _ = completed.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    #endregion

    #region Cleanup and mode encoding

    private Task DelayUntilAsync(DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        var delay = deadline - _timeProvider.GetUtcNow();

        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        return Task.Delay(delay, _timeProvider, cancellationToken);
    }

    private async ValueTask CleanupAsync()
    {
        using var timeout = new CancellationTokenSource(
            _options.CleanupTimeout,
            _timeProvider);

        // Terminal modes form a stack: unwind in reverse enable order and keep
        // attempting later restores even when one terminal write fails.
        for (var index = _leases.Count - 1; index >= 0; index--)
        {
            try
            {
                await WriteAsync(_leases[index].Disable, timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LastCleanupException ??= exception;
            }
        }

        _leases.Clear();
    }

    private async ValueTask WriteAsync(
        ReadOnlyMemory<byte> command,
        CancellationToken cancellationToken)
    {
        Debug.Assert(!command.IsEmpty, "Mode output always contains exact bytes.");
        await _transport.WriteAsync(command, cancellationToken)
            .ConfigureAwait(false);
        await _transport.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool MouseSupported(TerminalCapabilities capabilities)
    {
        Debug.Assert(capabilities is not null, "Mouse capability checks require the active immutable profile.");

        return _options.Coordinates == MouseCoordinates.Pixel
            ? capabilities.PixelMouse.IsSupported
            : capabilities.CellMouse.IsSupported;
    }

    private static Options RequireOptions(Options options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options;
    }

    private static TerminalContext RequireContext(TerminalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context;
    }

    #endregion
}
