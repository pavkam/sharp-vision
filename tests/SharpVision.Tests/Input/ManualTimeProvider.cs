// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Provides deterministic monotonic time for routed gesture tests.</summary>
internal sealed class ManualTimeProvider: TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private long _nextOrder;
    private long _timestamp;

    /// <inheritdoc/>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc/>
    public override long GetTimestamp() => _timestamp;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() =>
        DateTimeOffset.UnixEpoch + TimeSpan.FromTicks(_timestamp);

    /// <inheritdoc/>
    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new ManualTimer(this, callback, state, checked(_nextOrder++));

        lock (_gate)
        {
            _timers.Add(timer);
        }

        _ = timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Advances monotonic time by one non-negative duration.</summary>
    /// <param name="value">The duration to add.</param>
    internal void Advance(TimeSpan value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
        var target = checked(_timestamp + value.Ticks);

        while (TryTakeDue(target, out var timer))
        {
            timer.Invoke();
        }

        _timestamp = target;
    }

    /// <summary>Changes one owned timer against the provider's current timestamp.</summary>
    internal bool Change(ManualTimer timer, TimeSpan dueTime, TimeSpan period)
    {
        Validate(dueTime, allowZero: true, nameof(dueTime));
        Validate(period, allowZero: false, nameof(period));

        lock (_gate)
        {
            if (timer.Disposed)
            {
                return false;
            }

            timer.DueTimestamp = dueTime == Timeout.InfiniteTimeSpan
                ? long.MaxValue
                : checked(_timestamp + dueTime.Ticks);
            timer.PeriodTicks = period == Timeout.InfiniteTimeSpan ? 0 : period.Ticks;
            return true;
        }
    }

    /// <summary>Removes one disposed timer from future scheduling.</summary>
    internal void Remove(ManualTimer timer)
    {
        lock (_gate)
        {
            _ = _timers.Remove(timer);
        }
    }

    private bool TryTakeDue(long target, out ManualTimer timer)
    {
        lock (_gate)
        {
            ManualTimer? selected = null;

            foreach (var candidate in _timers)
            {
                if (candidate.Disposed || candidate.DueTimestamp > target)
                {
                    continue;
                }

                if (selected is null ||
                    candidate.DueTimestamp < selected.DueTimestamp ||
                    (candidate.DueTimestamp == selected.DueTimestamp && candidate.Order < selected.Order))
                {
                    selected = candidate;
                }
            }

            if (selected is null)
            {
                timer = null!;
                return false;
            }

            _timestamp = selected.DueTimestamp;
            selected.DueTimestamp = selected.PeriodTicks == 0
                ? long.MaxValue
                : checked(selected.DueTimestamp + selected.PeriodTicks);
            timer = selected;
            return true;
        }
    }

    private static void Validate(TimeSpan value, bool allowZero, string name)
    {
        if (value == Timeout.InfiniteTimeSpan)
        {
            return;
        }

        var minimum = allowZero ? TimeSpan.Zero : TimeSpan.FromMilliseconds(1);
        var maximum = TimeSpan.FromMilliseconds(int.MaxValue);

        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(name, value, "The timer duration is outside the supported range.");
        }
    }
}
