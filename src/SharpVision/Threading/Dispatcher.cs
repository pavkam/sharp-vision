// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Threading;

/// <summary>
/// Serializes UI work, callbacks, and idle transitions on one dedicated thread.
/// </summary>
/// <remarks>
/// <para>The queue is bounded. User callbacks and events never run while its lock is
/// held. Dispose cancels queued invocations and waits for the active callback to
/// return; callbacks must therefore remain finite.</para>
/// <para>Once shutdown starts, the callback that is still running may run to completion, may
/// invoke inline through <see cref="InvokeAsync(Action, CancellationToken)"/>, and may take
/// pending holds; it may not enqueue new work. Enqueueing operations - <see cref="Post(Action)"/>
/// and the off-thread <c>InvokeAsync</c> path - throw <see cref="ObjectDisposedException"/>,
/// because the queue has already been cancelled and nothing would ever run them. The inline
/// operations schedule nothing, so refusing them would contradict the promise above rather than
/// enforce it.</para>
/// </remarks>
[DebuggerDisplay("Dispatcher Queue={_queue.Count}/{_capacity}")]
[PublicAPI]
public sealed class Dispatcher: IAsyncDisposable
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly Queue<Work> _queue = [];

    private readonly TaskCompletionSource _stopped =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Thread _thread;
    private int _disposed;
    private Exception? _fatalException;
    private bool _idleRaised;
    private int _ownerThreadId;
    private int _pending;
    private bool _stopping;

    private Dispatcher(int capacity, string name, TimeProvider timeProvider, ManualResetEventSlim? releaseGate)
    {
        _capacity = capacity;
        TimeProvider = timeProvider;

        using ManualResetEventSlim started = new();

        _thread = new Thread(() => Run(started, releaseGate)) { IsBackground = true, Name = name };
        _thread.Start();

        started.Wait();
    }

    /// <summary>Raised once when ready and pending work transitions to empty.</summary>
    /// <remarks>
    /// The event runs on the dispatcher thread. Work posted by a handler is
    /// drained before the dispatcher waits again.
    /// </remarks>
    public event EventHandler? Idle;

    /// <summary>Raised for a fire-and-observe callback failure.</summary>
    /// <remarks>
    /// The event runs on the dispatcher thread outside the queue lock. Leaving
    /// the failure unhandled stops the dispatcher after the event returns.
    /// </remarks>
    public event EventHandler<UnhandledEventArgs>? UnhandledException;

    /// <summary>Starts one bounded dispatcher on a dedicated background thread.</summary>
    /// <param name="capacity">The positive maximum queued callback count.</param>
    /// <param name="name">The optional non-blank thread name.</param>
    /// <param name="timeProvider">The optional clock used by dispatcher-owned timers.</param>
    /// <returns>The running dispatcher.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public static Dispatcher Start(
        int capacity = 4096,
        string? name = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        var threadName = name is null
            ? "SharpVision.Dispatcher"
            : !string.IsNullOrWhiteSpace(name)
                ? name
                : throw new ArgumentException(
                    "The dispatcher thread name cannot be blank.",
                    nameof(name));
        return new Dispatcher(capacity, threadName, timeProvider ?? TimeProvider.System, releaseGate: null);
    }

    /// <summary>Starts one dispatcher whose thread blocks after startup until <paramref
    /// name="releaseGate"/> is signaled, immediately before its first idle-detection attempt.</summary>
    /// <remarks>
    /// A testability seam only: it lets a test subscribe to <see cref="Idle"/> before the
    /// dispatcher can possibly raise its one-shot no-prior-work notification, closing the race
    /// between <see cref="Start"/> returning and the background thread reaching that check.
    /// </remarks>
    /// <param name="releaseGate">The non-null gate the caller signals once ready to proceed.</param>
    /// <param name="capacity">The positive maximum queued callback count.</param>
    /// <param name="name">The optional non-blank thread name.</param>
    /// <param name="timeProvider">The optional clock used by dispatcher-owned timers.</param>
    /// <returns>The running, currently paused dispatcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="releaseGate"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    internal static Dispatcher StartPaused(
        ManualResetEventSlim releaseGate,
        int capacity = 4096,
        string? name = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(releaseGate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        var threadName = name is null
            ? "SharpVision.Dispatcher"
            : !string.IsNullOrWhiteSpace(name)
                ? name
                : throw new ArgumentException(
                    "The dispatcher thread name cannot be blank.",
                    nameof(name));
        return new Dispatcher(capacity, threadName, timeProvider ?? TimeProvider.System, releaseGate);
    }

    /// <summary>Gets the callback failure that stopped this dispatcher, or null when it is running
    /// or was stopped by <see cref="DisposeAsync"/>.</summary>
    /// <remarks>
    /// Set once, whenever a callback failure is left unhandled - because no
    /// <see cref="UnhandledException"/> subscriber exists, or because a subscriber left
    /// <see cref="UnhandledEventArgs.Handled"/> false, or because a subscriber itself threw, in
    /// which case both exceptions are retained. Every one of those arms stops the dispatcher, so
    /// every one of them leaves the cause here. Without a subscriber - the default for a bare
    /// dispatcher - this is the only record of why later calls started throwing
    /// <see cref="ObjectDisposedException"/> on an object nobody disposed. It is readable from any
    /// thread and outlives disposal, which completes normally in every case so an unawaited
    /// <see cref="DisposeAsync"/> can never surface as an unobserved task exception.
    /// </remarks>
    public Exception? FatalException => Volatile.Read(ref _fatalException);

    /// <summary>Gets the shared clock used by timers associated with this dispatcher.</summary>
    internal TimeProvider TimeProvider { get; }

    /// <summary>Gets whether the current thread owns this dispatcher.</summary>
    /// <returns>True only on the dedicated dispatcher thread.</returns>
    public bool CheckAccess() => Environment.CurrentManagedThreadId ==
                                 Volatile.Read(ref _ownerThreadId);

    /// <summary>Throws unless the current thread owns this dispatcher.</summary>
    /// <exception cref="InvalidOperationException">The caller is not on the dispatcher thread.</exception>
    public void VerifyAccess()
    {
        if (!CheckAccess())
        {
            throw new InvalidOperationException("The operation must run on the owning dispatcher thread.");
        }
    }

    /// <summary>Queues one fire-and-observe callback.</summary>
    /// <param name="action">The non-null finite callback.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The bounded queue is full.</exception>
    /// <exception cref="ObjectDisposedException">Shutdown has started.</exception>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Enqueue(new PostWork(action));
    }

    /// <summary>
    /// Queues one fire-and-observe callback, invoking <paramref name="onCancelled"/> instead if
    /// dispatcher shutdown cancels the queued work before it runs. Internal because ordinary
    /// callers observe completion through <see cref="InvokeAsync(Action, CancellationToken)"/>
    /// instead; this exists only for callers that must release state they set before posting even
    /// when a synchronous <see cref="Post(Action)"/> success is later followed by the work never
    /// actually running.
    /// </summary>
    /// <param name="action">The non-null finite callback.</param>
    /// <param name="onCancelled">The non-null callback invoked if the work is cancelled instead.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> or
    /// <paramref name="onCancelled"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The bounded queue is full.</exception>
    /// <exception cref="ObjectDisposedException">Shutdown has started.</exception>
    internal void Post(Action action, Action onCancelled)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(onCancelled);
        Enqueue(new PostWork(action, onCancelled));
    }

    /// <summary>Invokes an action on the dispatcher and observes completion.</summary>
    /// <remarks>
    /// Called from the dispatcher thread the action runs inline and nothing is queued, so neither
    /// declared exception applies there - including during shutdown, when the active callback is
    /// still entitled to invoke inline.
    /// </remarks>
    /// <param name="action">The non-null finite callback.</param>
    /// <param name="cancellationToken">Cancels the callback before execution.</param>
    /// <returns>The callback completion.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The bounded queue is full, off-thread.</exception>
    /// <exception cref="ObjectDisposedException">Shutdown has started, off-thread.</exception>
    public ValueTask InvokeAsync(
        Action action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        if (CheckAccess())
        {
            try
            {
                action();
                return ValueTask.CompletedTask;
            }
            catch (Exception exception)
            {
                return ValueTask.FromException(exception);
            }
        }

        var work = new ActionWork(action, cancellationToken);
        Enqueue(work);
        return new ValueTask(work.Completion);
    }

    /// <summary>Invokes a function on the dispatcher and observes its result.</summary>
    /// <remarks>
    /// Called from the dispatcher thread the function runs inline and nothing is queued, so neither
    /// declared exception applies there - including during shutdown, when the active callback is
    /// still entitled to invoke inline.
    /// </remarks>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="function">The non-null finite function.</param>
    /// <param name="cancellationToken">Cancels the function before execution.</param>
    /// <returns>The function completion and result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="function"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The bounded queue is full, off-thread.</exception>
    /// <exception cref="ObjectDisposedException">Shutdown has started, off-thread.</exception>
    public ValueTask<T> InvokeAsync<T>(
        Func<T> function,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(function);

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<T>(cancellationToken);
        }

        if (CheckAccess())
        {
            try
            {
                return ValueTask.FromResult(function());
            }
            catch (Exception exception)
            {
                return ValueTask.FromException<T>(exception);
            }
        }

        var work = new FunctionWork<T>(function, cancellationToken);
        Enqueue(work);
        return new ValueTask<T>(work.Completion);
    }

    /// <summary>Suppresses idle while one asynchronous dispatcher phase is pending.</summary>
    /// <remarks>
    /// Callable during shutdown. <see cref="VerifyAccess"/> makes this reachable only from the
    /// active callback, which dispose has already promised to let run to completion, and a hold
    /// enqueues nothing: once stopping, the run loop exits on an empty queue whatever
    /// <c>_pending</c> says, so a late hold cannot delay shutdown. Revoking it mid-flight only
    /// forced the callback's own callers to guard against a lease they were entitled to take.
    /// </remarks>
    /// <returns>An idempotent lease that may be released from any thread.</returns>
    /// <exception cref="InvalidOperationException">The caller is not on the dispatcher thread.</exception>
    internal IDisposable Hold()
    {
        VerifyAccess();

        lock (_gate)
        {
            _pending = checked(_pending + 1);
            _idleRaised = false;
        }

        return new PendingLease(this);
    }

    /// <summary>Cancels queued work and waits for the active callback to return.</summary>
    /// <remarks>
    /// Completes normally even when a callback failure stopped the dispatcher; read
    /// <see cref="FatalException"/> to learn whether it did.
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        Work[] cancelled;

        lock (_gate)
        {
            if (_stopping)
            {
                return CheckAccess()
                    ? ValueTask.CompletedTask
                    : new ValueTask(_stopped.Task);
            }

            _stopping = true;
            cancelled = [.. _queue];
            _queue.Clear();
            Monitor.PulseAll(_gate);
        }

        foreach (var work in cancelled)
        {
            work.Cancel();
        }

        return CheckAccess()
            ? ValueTask.CompletedTask
            : new ValueTask(_stopped.Task);
    }

    private void Enqueue(Work work)
    {
        lock (_gate)
        {
            ThrowIfStopping();

            if (_queue.Count >= _capacity)
            {
                throw new InvalidOperationException("The dispatcher queue is full.");
            }

            _queue.Enqueue(work);
            _idleRaised = false;
            Monitor.Pulse(_gate);
        }
    }

    private void Run(ManualResetEventSlim started, ManualResetEventSlim? releaseGate)
    {
        Volatile.Write(ref _ownerThreadId, Environment.CurrentManagedThreadId);
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(this));
        started.Set();
        releaseGate?.Wait();

        try
        {
            while (TryTake(out var work, out var raiseIdle))
            {
                if (work is not null)
                {
                    try
                    {
                        work.Execute();
                    }
                    catch (Exception exception)
                    {
                        Report(exception);
                    }
                }
                else if (raiseIdle)
                {
                    try
                    {
                        Idle?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception exception)
                    {
                        Report(exception);
                    }
                }
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
            Volatile.Write(ref _disposed, 1);
            Volatile.Write(ref _ownerThreadId, 0);

            // Always a plain completion signal, never a fault. The cause of a fatal stop lives on
            // FatalException instead: faulting here made an unawaited DisposeAsync surface as a
            // process-wide TaskScheduler.UnobservedTaskException, and made every
            // `await using var dispatcher = Dispatcher.Start(...)` throw at implicit dispose -
            // including Application.DisposeAsync's finally, where it would mask whatever the try
            // was already propagating.
            _ = _stopped.TrySetResult();
        }
    }

    private bool TryTake(out Work? work, out bool raiseIdle)
    {
        lock (_gate)
        {
            while (true)
            {
                if (_queue.TryDequeue(out work))
                {
                    raiseIdle = false;
                    return true;
                }

                if (_stopping)
                {
                    work = null;
                    raiseIdle = false;
                    return false;
                }

                if (_pending == 0 && !_idleRaised)
                {
                    _idleRaised = true;
                    work = null;
                    raiseIdle = true;
                    return true;
                }

                _ = Monitor.Wait(_gate);
            }
        }
    }

    private void Report(Exception exception)
    {
        var eventArgs = new UnhandledEventArgs(exception);
        Exception? handlerException = null;

        try
        {
            UnhandledException?.Invoke(this, eventArgs);
        }
        catch (Exception thrown)
        {
            // A handler that itself throws is treated as though it never marked the failure
            // handled, and its own exception is preserved alongside the original rather than
            // replacing it.
            eventArgs.Handled = false;
            handlerException = thrown;
        }

        if (eventArgs.Handled)
        {
            return;
        }

        // Every arm that reaches here stops the dispatcher, so every arm must leave the cause
        // retrievable. Recording it only when the handler threw meant a bare dispatcher - no
        // subscriber at all, which is the default - stopped with the failure existing nowhere,
        // and the caller then saw only ObjectDisposedException from a later Post, on an object
        // nobody disposed. First failure wins; Report runs only on the dispatcher thread, so the
        // check needs no interlock.
        if (Volatile.Read(ref _fatalException) is null)
        {
            Volatile.Write(
                ref _fatalException,
                handlerException is null
                    ? exception
                    : new AggregateException(exception, handlerException));
        }

        RequestStop();
    }

    private void RequestStop()
    {
        Work[] cancelled;

        lock (_gate)
        {
            if (_stopping)
            {
                return;
            }

            _stopping = true;
            cancelled = [.. _queue];
            _queue.Clear();
            Monitor.PulseAll(_gate);
        }

        foreach (var work in cancelled)
        {
            work.Cancel();
        }
    }

    /// <summary>Releases one active asynchronous pending-work lease.</summary>
    internal void ReleasePending()
    {
        lock (_gate)
        {
            Debug.Assert(_pending > 0, "A pending lease must be active before release.");
            _pending--;
            Monitor.Pulse(_gate);
        }
    }

    private void ThrowIfStopping() =>
        ObjectDisposedException.ThrowIf(_stopping || Volatile.Read(ref _disposed) != 0, this);
}
