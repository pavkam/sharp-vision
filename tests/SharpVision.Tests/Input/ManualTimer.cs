// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Provides one deterministic timer owned by a <see cref="ManualTimeProvider"/>.</summary>
internal sealed class ManualTimer: ITimer
{
    private readonly TimerCallback _callback;
    private readonly ManualTimeProvider _owner;
    private readonly object? _state;
    private int _disposed;

    /// <summary>Initializes one disabled deterministic timer.</summary>
    internal ManualTimer(
        ManualTimeProvider owner,
        TimerCallback callback,
        object? state,
        long order)
    {
        _owner = owner;
        _callback = callback;
        _state = state;
        Order = order;
        DueTimestamp = long.MaxValue;
    }

    /// <summary>Gets the deterministic creation order.</summary>
    internal long Order { get; }

    /// <summary>Gets whether the timer is disposed.</summary>
    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Gets or sets the next provider timestamp at which the timer is due.</summary>
    internal long DueTimestamp { get; set; }

    /// <summary>Gets or sets the positive repeat period in provider ticks, or zero for one shot.</summary>
    internal long PeriodTicks { get; set; }

    /// <inheritdoc/>
    public bool Change(TimeSpan dueTime, TimeSpan period) =>
        _owner.Change(this, dueTime, period);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _owner.Remove(this);
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Invokes the callback unless disposal won the due-time race.</summary>
    internal void Invoke()
    {
        if (!IsDisposed)
        {
            _callback(_state);
        }
    }
}
