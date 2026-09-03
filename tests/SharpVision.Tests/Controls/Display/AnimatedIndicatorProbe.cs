// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Provides a minimal concrete animated indicator for proving the shared playback
/// contract without borrowing Spinner or ChaseIndicator behavior.</summary>
internal sealed class AnimatedIndicatorProbe: AnimatedIndicatorBase
{
    /// <summary>Gets or sets whether the probe attempts to paint both adjacent padding cells.</summary>
    internal bool DrawOutsideContentBounds { get; set; }

    /// <summary>Gets the number of shared-timer callbacks accepted by this probe.</summary>
    internal int TickCount { get; private set; }

    /// <summary>Gets the interval observed by the most recent derived synchronization callback.</summary>
    internal TimeSpan SynchronizedInterval { get; private set; }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderFrame(TerminalCanvas canvas, Rect bounds)
    {
        if (DrawOutsideContentBounds)
        {
            canvas.DrawRune(
                new Rune('X'),
                new Point(bounds.X - 1, bounds.Y),
                ResolvedStyle,
                BackgroundMode.Transparent);
            canvas.DrawRune(
                new Rune('X'),
                new Point(bounds.Right, bounds.Y),
                ResolvedStyle,
                BackgroundMode.Transparent);
        }

        canvas.DrawRune(
            new Rune('0' + TickCount),
            new Point(bounds.X, bounds.Y),
            ResolvedStyle,
            BackgroundMode.Transparent);
    }

    /// <inheritdoc/>
    protected override void OnAnimationTick()
    {
        TickCount++;
        Invalidate(InvalidationImpact.Render);
    }

    /// <inheritdoc/>
    protected override void OnIntervalChanged()
    {
        SynchronizedInterval = Interval;
        base.OnIntervalChanged();
    }
}
