// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>Provides deterministic frame-timing telemetry for Renderer tests.</summary>
internal sealed class ManualTimeProvider: TimeProvider
{
    private long _timestamp;

    /// <summary>Gets or initializes deterministic clock movement after each timestamp read.</summary>
    internal TimeSpan AdvanceOnRead { get; set; }

    /// <inheritdoc/>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc/>
    public override long GetTimestamp()
    {
        var current = _timestamp;
        _timestamp += AdvanceOnRead.Ticks;
        return current;
    }
}
