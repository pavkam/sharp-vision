// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

/// <summary>Provides deterministic query-deadline time.</summary>
internal sealed class ManualTimeProvider: TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    /// <inheritdoc/>
    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ValidateDuration(dueTime, nameof(dueTime));
        ValidateDuration(period, nameof(period));
        var timer = new ManualTimer(this, callback, state);

        lock (_gate)
        {
            timer.Schedule(_now, dueTime, period);
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>Advances the current time by one test-controlled duration.</summary>
    /// <param name="duration">The duration to add.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is negative.</exception>
    internal void Advance(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        ManualTimer[] due;

        lock (_gate)
        {
            _now += duration;
            due = [.. _timers.Where(timer => timer.Prepare(_now))];
        }

        foreach (var timer in due)
        {
            timer.Invoke();
        }
    }

    /// <summary>Changes one provider-owned timer schedule.</summary>
    /// <param name="timer">The non-null provider-owned timer.</param>
    /// <param name="dueTime">The due time relative to the current clock.</param>
    /// <param name="period">The repeat period or infinite duration.</param>
    /// <returns>Whether the timer remained active and was changed.</returns>
    internal bool Change(ManualTimer timer, TimeSpan dueTime, TimeSpan period)
    {
        ValidateDuration(dueTime, nameof(dueTime));
        ValidateDuration(period, nameof(period));

        lock (_gate)
        {
            if (timer.IsDisposed)
            {
                return false;
            }

            timer.Schedule(_now, dueTime, period);
            return true;
        }
    }

    /// <summary>Removes one provider-owned timer idempotently.</summary>
    /// <param name="timer">The non-null provider-owned timer.</param>
    internal void Dispose(ManualTimer timer)
    {
        lock (_gate)
        {
            timer.MarkDisposed();
            _ = _timers.Remove(timer);
        }
    }

    private static void ValidateDuration(TimeSpan value, string parameterName)
    {
        if (value < Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "A timer duration cannot be less than infinite.");
        }
    }
}
