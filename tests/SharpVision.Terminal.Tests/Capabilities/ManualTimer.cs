// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

/// <summary>Provides one deterministic timer owned by a manual time provider.</summary>
internal sealed class ManualTimer: ITimer
{
    private readonly ManualTimeProvider _provider;
    private readonly TimerCallback _callback;
    private readonly object? _state;

    /// <summary>Initializes one unscheduled provider-owned timer.</summary>
    /// <param name="provider">The non-null owning time provider.</param>
    /// <param name="callback">The non-null callback.</param>
    /// <param name="state">The optional callback state.</param>
    internal ManualTimer(
        ManualTimeProvider provider,
        TimerCallback callback,
        object? state)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(callback);
        _provider = provider;
        _callback = callback;
        _state = state;
    }

    /// <summary>Gets whether this timer has been disposed.</summary>
    internal bool IsDisposed { get; private set; }

    /// <summary>Gets the next absolute due time.</summary>
    private DateTimeOffset Due { get; set; } = DateTimeOffset.MaxValue;

    /// <summary>Gets the repeat period or infinite duration.</summary>
    private TimeSpan Period { get; set; } = Timeout.InfiniteTimeSpan;

    /// <inheritdoc/>
    public bool Change(TimeSpan dueTime, TimeSpan period) =>
        _provider.Change(this, dueTime, period);

    /// <inheritdoc/>
    public void Dispose() => _provider.Dispose(this);

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Updates this timer while the provider gate is held.</summary>
    /// <param name="now">The provider's current time.</param>
    /// <param name="dueTime">The relative due time or infinite duration.</param>
    /// <param name="period">The repeat period or infinite duration.</param>
    internal void Schedule(DateTimeOffset now, TimeSpan dueTime, TimeSpan period)
    {
        Due = dueTime == Timeout.InfiniteTimeSpan
            ? DateTimeOffset.MaxValue
            : now + dueTime;
        Period = period;
    }

    /// <summary>Advances the next due time while the provider gate is held.</summary>
    /// <param name="now">The provider's advanced time.</param>
    /// <returns>Whether the callback is due once for this advance.</returns>
    internal bool Prepare(DateTimeOffset now)
    {
        if (IsDisposed || Due > now)
        {
            return false;
        }

        if (Period <= TimeSpan.Zero)
        {
            Due = DateTimeOffset.MaxValue;
        }
        else
        {
            do
            {
                Due += Period;
            }
            while (Due <= now);
        }

        return true;
    }

    /// <summary>Invokes the borrowed callback outside the provider gate.</summary>
    internal void Invoke() => _callback(_state);

    /// <summary>Marks this timer disposed while the provider gate is held.</summary>
    internal void MarkDisposed()
    {
        IsDisposed = true;
        Due = DateTimeOffset.MaxValue;
    }
}
