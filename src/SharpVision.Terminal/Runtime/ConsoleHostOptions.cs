// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Configures how the console host prepares the interactive console.</summary>
[PublicAPI]
public sealed record ConsoleHostOptions
{
    /// <summary>Gets the positive finite poll interval for the cell-only resize fallback.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive and finite.</exception>
    public TimeSpan ResizeInterval
    {
        get;
        init
        {
            if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The resize interval must be positive and finite.");
            }

            field = value;
        }
    } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets whether control keys such as Ctrl+C are delivered as input rather than
    /// raising the host signal. Default is <see langword="false"/>.
    /// </summary>
    public bool CaptureControlKeys { get; init; }

    /// <summary>
    /// Gets whether the platform console mode should report mouse events, because mouse
    /// tracking is negotiated for this run. Default is <see langword="false"/>.
    /// </summary>
    public bool EnableMouseInput { get; init; }
}
