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
    private readonly Dispatcher _dispatcher;
    private readonly Lock _gate = new();
    private readonly ITimer _timer;
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
        _timer = dispatcher.TimeProvider.CreateTimer(
            static state => ((DispatcherTimer) state!).OnElapsed(),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
    }

    /// <summary>Raised on the owning dispatcher after one eligible interval.</summary>
    public event EventHandler? Tick;

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

                if (_isRunning)
                {
                    ObjectDisposedException.ThrowIf(!_timer.Change(value, value), this);
                }

                _interval = value;
                _generation = checked(_generation + 1);
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

            ObjectDisposedException.ThrowIf(!_timer.Change(_interval, _interval), this);

            _generation = checked(_generation + 1);
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

            ObjectDisposedException.ThrowIf(
                !_timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan),
                this);

            _generation = checked(_generation + 1);
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

        lock (_gate)
        {
            _isRunning = false;
            _generation = checked(_generation + 1);
            Tick = null;
        }

        _timer.Dispose();
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

    private void OnElapsed()
    {
        if (Interlocked.CompareExchange(ref _pending, 1, 0) != 0)
        {
            return;
        }

        int generation;

        lock (_gate)
        {
            if (_disposed != 0 || !_isRunning)
            {
                _ = Interlocked.Exchange(ref _pending, 0);
                return;
            }

            generation = _generation;
        }

        try
        {
            _dispatcher.Post(() => Deliver(generation));
        }
        catch (InvalidOperationException)
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
