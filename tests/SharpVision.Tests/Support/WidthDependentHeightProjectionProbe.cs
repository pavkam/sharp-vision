// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Provides a leaf whose measured height is an arbitrary caller-supplied function of its
/// last externally assigned width, for exercising <see cref="WidthDependentViewportCoordinator"/>
/// reconciliation against real <see cref="Container"/> scrollbar resolution.</summary>
/// <remarks>
/// Unlike <see cref="ProbeControl"/>'s fixed intrinsic size, this probe only changes its reported
/// height when <see cref="SetProjectedWidth"/> is called - the same seam a coordinator's
/// <c>reproject</c> callback uses - rather than reacting to whatever transient candidate width a
/// container's own automatic scrollbar probe measures it against mid-resolution. That isolates the
/// coordinator's explicit reprojection step from the container's internal probe, matching how a
/// real width-dependent projection (word wrap, a breakpoint grid) only re-lays-out when its owner
/// explicitly commits a new width, not on every scrollbar candidate the container tries.
/// </remarks>
internal sealed class WidthDependentHeightProjectionProbe: ControlBase
{
    private int? _projectedWidth;

    /// <summary>Initializes a probe reporting <paramref name="initialWidth"/> until the first
    /// <see cref="SetProjectedWidth"/> call.</summary>
    /// <param name="heightForWidth">The non-null function from an assigned width to the height this
    /// probe reports for it.</param>
    /// <param name="initialWidth">The width reported before any reprojection.</param>
    internal WidthDependentHeightProjectionProbe(Func<int, int> heightForWidth, int initialWidth)
    {
        ArgumentNullException.ThrowIfNull(heightForWidth);

        HeightForWidth = heightForWidth;
        InitialWidth = initialWidth;
    }

    /// <summary>Gets or sets the function from an assigned width to reported height, mutable so a
    /// test can simulate content changing between two independent layout passes.</summary>
    internal Func<int, int> HeightForWidth { get; set; }

    /// <summary>Gets the width this probe reports before any reprojection.</summary>
    internal int InitialWidth { get; }

    /// <summary>Gets the width most recently committed through <see cref="SetProjectedWidth"/>, or
    /// null before the first reprojection.</summary>
    internal int? ProjectedWidth => _projectedWidth;

    /// <summary>Commits the width this probe measures against, exercising a coordinator's
    /// <c>reproject</c> callback.</summary>
    /// <param name="width">The newly assigned width.</param>
    internal void SetProjectedWidth(int width) => _projectedWidth = width;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var width = _projectedWidth ?? InitialWidth;
        return new Size(width, HeightForWidth(width));
    }
}
