// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Provides deterministic monotonic time for routed gesture tests.</summary>
internal sealed class ManualTimeProvider: TimeProvider
{
    private long _timestamp;

    /// <inheritdoc/>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc/>
    public override long GetTimestamp() => _timestamp;

    /// <summary>Advances monotonic time by one non-negative duration.</summary>
    /// <param name="value">The duration to add.</param>
    internal void Advance(TimeSpan value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
        _timestamp = checked(_timestamp + value.Ticks);
    }
}
