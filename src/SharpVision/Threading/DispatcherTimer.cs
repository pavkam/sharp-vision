// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Threading;

/// <summary>Raises coalesced periodic callbacks on one dispatcher.</summary>
/// <remarks>
/// The underlying clock may signal from any thread. At most one signal is queued
/// to the dispatcher, and elapsed periods are skipped while that signal remains pending.
/// </remarks>
[PublicAPI]
public sealed class DispatcherTimer: IDisposable
{
    private static readonly TimeSpan _maximumInterval = TimeSpan.FromMilliseconds(int.MaxValue);

    // The callback's `state` binds one immutable (timer, generation) pair at CreateTimer time.
    // ITimer.Change cannot rebind that state, so every arm point that must hand the callback a
    // fresh generation replaces the ITimer instance instead of calling Change on the old one.
    private static readonly TimerCallback _onElapsedCallback = static state =>
    {
        var (timer, generation) = ((DispatcherTimer, int)) state!;
        timer.OnElapsed(generation);
    };

    private readonly Dispatcher _dispatcher;
    private readonly Lock _gate = new();
    private ITimer _timer;
    private int _disposed;
    private int _generation;
    private int _pending;
    private TimeSpan _interval;
    private bool _isRunning;

    /// <summary>Initializes one stopped dispatcher timer.</summary>
    /// <param name="dispatcher">The non-null dispatcher that owns timer callbacks.</param>
    /// <param name="interval">The interval from one through 2,147,483,647 milliseconds.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatcher"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is outside the supported range.</exception>
    public DispatcherTimer(Dispatcher dispatcher, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ValidateInterval(interval, nameof(interval));
        _dispatcher = dispatcher;
        _interval = interval;
        _timer = CreateArmedTimer(_generation, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Raised on the owning dispatcher after one eligible interval.</summary>
    public event EventHandler? Tick;

    /// <summary>Gets the current <see cref="Tick"/> subscriber count for duplicate-subscription
    /// test seams; a field-like event has no other reachable accessibility.</summary>
    internal int TickSubscribers => Tick?.GetInvocationList().Length ?? 0;

    /// <summary>Gets the current arm generation for stale-callback regression test seams.</summary>
    internal int Generation
    {
        get
        {
            lock (_gate)
            {
                return _generation;
            }
        }
    }

    /// <summary>Gets or sets the interval between eligible ticks.</summary>
    /// <remarks>Changing a running timer starts one complete new interval.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the supported range.</exception>
    /// <exception cref="InvalidOperationException">The caller is not on the owning dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The timer is disposed.</exception>
    public TimeSpan Interval
    {
        get
        {
            lock (_gate)
            {
                return _interval;
            }
        }
        set
        {
            ValidateInterval(value, nameof(value));
            _dispatcher.VerifyAccess();

            lock (_gate)
            {
                ThrowIfDisposed();

                if (_interval == value)
                {
                    return;
                }

                _interval = value;
                _generation = checked(_generation + 1);

                if (_isRunning)
                {
                    var previous = _timer;
                    _timer = CreateArmedTimer(_generation, value, value);
                    previous.Dispose();
                }
            }
        }
    }

    /// <summary>Gets whether periodic signaling is enabled.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _isRunning;
            }
        }
    }

    /// <summary>Starts periodic signaling after one complete interval.</summary>
    /// <exception cref="InvalidOperationException">The caller is not on the owning dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The timer is disposed.</exception>
    public void Start()
    {
        _dispatcher.VerifyAccess();

        lock (_gate)
        {
            ThrowIfDisposed();

            if (_isRunning)
            {
                return;
            }

            _generation = checked(_generation + 1);

            var previous = _timer;
            _timer = CreateArmedTimer(_generation, _interval, _interval);
            previous.Dispose();

            _isRunning = true;
        }
    }

    /// <summary>Stops periodic signaling without clearing event handlers.</summary>
    /// <exception cref="InvalidOperationException">The caller is not on the owning dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The timer is disposed.</exception>
    public void Stop()
    {
        _dispatcher.VerifyAccess();

        lock (_gate)
        {
            ThrowIfDisposed();

            if (!_isRunning)
            {
                return;
            }

            _generation = checked(_generation + 1);

            var previous = _timer;
            _timer = CreateArmedTimer(_generation, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            previous.Dispose();

            _isRunning = false;
        }
    }

    /// <summary>Stops signaling, suppresses queued ticks, and releases the clock timer.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        ITimer timer;

        lock (_gate)
        {
            _isRunning = false;
            _generation = checked(_generation + 1);
            Tick = null;
            timer = _timer;
        }

        timer.Dispose();
    }

    /// <summary>Validates one timer interval before observable state changes.</summary>
    /// <param name="value">The interval to validate.</param>
    /// <param name="parameterName">The public parameter or property value name.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is outside the supported range.</exception>
    internal static void ValidateInterval(TimeSpan value, string parameterName)
    {
        if (value < TimeSpan.FromMilliseconds(1) || value > _maximumInterval)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The interval must be from 1 through 2,147,483,647 milliseconds.");
        }
    }

    /// <summary>Creates one <see cref="ITimer"/> whose callback state binds the given generation
    /// for the lifetime of that instance; the caller replaces and disposes the previous instance
    /// under <see cref="_gate"/> so the callback's generation can never be observed as stale by
    /// virtue of a later in-place <see cref="ITimer.Change"/> (which cannot rebind state).</summary>
    private ITimer CreateArmedTimer(int generation, TimeSpan dueTime, TimeSpan period) =>
        _dispatcher.TimeProvider.CreateTimer(_onElapsedCallback, (this, generation), dueTime, period);

    /// <summary>Invokes the same delivery path a real clock callback would, for one specific
    /// generation, bypassing the underlying <see cref="ITimer"/> entirely. This lets tests model
    /// an in-flight pre-restart callback arriving after <see cref="Start"/>, <see cref="Stop"/>,
    /// or the <see cref="Interval"/> setter has already bumped the live generation.</summary>
    internal void SimulateElapsedForGeneration(int generation) => OnElapsed(generation);

    private void OnElapsed(int generation)
    {
        if (Interlocked.CompareExchange(ref _pending, 1, 0) != 0)
        {
            return;
        }

        lock (_gate)
        {
            if (_disposed != 0 || !_isRunning)
            {
                _ = Interlocked.Exchange(ref _pending, 0);
                return;
            }
        }

        try
        {
            // The onCancelled callback covers the narrow race Post's synchronous exception
            // cannot: dispatcher shutdown may cancel this already-queued work before the
            // dispatcher thread reaches it, in which case Deliver's own _pending reset (its
            // first statement) never runs. Without this, _pending would latch at 1 forever and
            // this timer would silently never post again.
            //
            // OnElapsed itself runs on the TimeProvider's own periodic timer callback thread, not
            // the dispatcher thread. A full queue is swallowed here (catch below resets _pending
            // the same way) rather than propagated, because this is a recurring callback: letting
            // InvalidOperationException escape would be process-fatal on a ThreadPool-owned timer
            // thread by default, repeated every period under sustained load, over what a caller
            // can simply catch up on next tick.
            _dispatcher.Post(() => Deliver(generation), () => Interlocked.Exchange(ref _pending, 0));
        }
        catch (Exception exception) when (exception is ObjectDisposedException or InvalidOperationException)
        {
            _ = Interlocked.Exchange(ref _pending, 0);
        }
    }

    private void Deliver(int generation)
    {
        _ = Interlocked.Exchange(ref _pending, 0);
        EventHandler? callback;

        lock (_gate)
        {
            if (_disposed != 0 || !_isRunning || generation != _generation)
            {
                return;
            }

            callback = Tick;
        }

        callback?.Invoke(this, EventArgs.Empty);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed != 0, this);
}
