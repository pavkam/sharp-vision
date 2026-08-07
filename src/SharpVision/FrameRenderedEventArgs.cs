// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

using Terminal.Rendering;

/// <summary>Provides metrics for one flushed and committed terminal frame.</summary>
[PublicAPI]
public sealed class FrameRenderedEventArgs: EventArgs
{
    /// <summary>Initializes one completed frame event.</summary>
    /// <param name="metrics">The validated completed renderer metrics.</param>
    public FrameRenderedEventArgs(RenderMetrics metrics) => RenderMetrics = metrics;

    /// <summary>Gets completed renderer metrics.</summary>
    public RenderMetrics RenderMetrics { get; }
}
