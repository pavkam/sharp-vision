// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Surfaces;

/// <summary>Measures one floating-surface fade from a captured progress to its target.</summary>
internal readonly struct FloatingSurfaceTransition
{
    private readonly TimeProvider _timeProvider;
    private readonly long _startedAt;
    private readonly TimeSpan _duration;
    private readonly double _start;
    private readonly double _target;

    /// <summary>Initializes one positive-duration transition against a monotonic clock.</summary>
    /// <param name="timeProvider">The non-null dispatcher-owned clock.</param>
    /// <param name="duration">The positive transition duration.</param>
    /// <param name="start">The inclusive zero-through-one starting progress.</param>
    /// <param name="target">The inclusive zero-through-one target progress.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A duration or progress is invalid.</exception>
    internal FloatingSurfaceTransition(
        TimeProvider timeProvider,
        TimeSpan duration,
        double start,
        double target)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);

        if (!double.IsFinite(start) || start is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "Transition progress must be finite and between zero and one.");
        }

        if (!double.IsFinite(target) || target is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "Transition progress must be finite and between zero and one.");
        }

        _timeProvider = timeProvider;
        _startedAt = timeProvider.GetTimestamp();
        _duration = duration;
        _start = start;
        _target = target;
    }

    /// <summary>Gets elapsed progress clamped between the captured start and target.</summary>
    internal double Progress
    {
        get
        {
            var ratio = Math.Clamp(Elapsed.Ticks / (double) _duration.Ticks, 0, 1);
            return _start + ((_target - _start) * ratio);
        }
    }

    /// <summary>Gets the non-negative wall-clock time remaining.</summary>
    internal TimeSpan Remaining
    {
        get
        {
            var value = _duration - Elapsed;
            return value > TimeSpan.Zero ? value : TimeSpan.Zero;
        }
    }

    private TimeSpan Elapsed => _timeProvider.GetElapsedTime(_startedAt, _timeProvider.GetTimestamp());
}
