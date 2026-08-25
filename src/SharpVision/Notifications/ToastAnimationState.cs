// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Notifications;

/// <summary>Measures one Toast entrance against a monotonic dispatcher-owned clock.</summary>
internal readonly struct ToastAnimationState
{
    private readonly TimeProvider _timeProvider;
    private readonly long _startedAt;
    private readonly TimeSpan _duration;

    /// <summary>Initializes one positive-duration animation interval.</summary>
    internal ToastAnimationState(TimeProvider timeProvider, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        _timeProvider = timeProvider;
        _startedAt = timeProvider.GetTimestamp();
        _duration = duration;
    }

    /// <summary>Gets current elapsed progress clamped from zero through one.</summary>
    internal double Progress
    {
        get
        {
            var elapsed = _timeProvider.GetElapsedTime(_startedAt, _timeProvider.GetTimestamp());
            return Math.Clamp(elapsed.Ticks / (double) _duration.Ticks, 0, 1);
        }
    }
}
