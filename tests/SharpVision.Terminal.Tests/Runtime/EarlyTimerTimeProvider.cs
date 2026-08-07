// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using Capabilities;



/// <summary>Provides a deterministic clock whose first timer can fire before UTC reaches its due time.</summary>
internal sealed class EarlyTimerTimeProvider: TimeProvider
{
    private readonly Lock _gate = new();
    private readonly ManualTimeProvider _inner = new();
    private TimerCallback? _firstCallback;
    private object? _firstState;
    private int _timerCount;

    /// <summary>Gets completion when the provider creates a second timer.</summary>
    internal TaskCompletionSource SecondTimerCreated { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _inner.GetUtcNow();

    /// <inheritdoc/>
    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = _inner.CreateTimer(callback, state, dueTime, period);

        lock (_gate)
        {
            _timerCount++;

            if (_timerCount == 1)
            {
                _firstCallback = callback;
                _firstState = state;
            }
            else if (_timerCount == 2)
            {
                _ = SecondTimerCreated.TrySetResult();
            }
        }

        return timer;
    }

    /// <summary>Fires the first timer once without advancing the provider's UTC clock.</summary>
    /// <exception cref="InvalidOperationException">No first timer exists, or it has already fired early.</exception>
    internal void FireFirstTimerEarly()
    {
        TimerCallback callback;
        object? state;

        lock (_gate)
        {
            callback = _firstCallback ?? throw new InvalidOperationException(
                "The first timer is unavailable for an early callback.");
            state = _firstState;
            _firstCallback = null;
            _firstState = null;
        }

        callback(state);
    }

    /// <summary>Advances UTC and invokes timers whose true deadlines have elapsed.</summary>
    /// <param name="duration">The non-negative duration to advance.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is negative.</exception>
    internal void Advance(TimeSpan duration) => _inner.Advance(duration);
}
