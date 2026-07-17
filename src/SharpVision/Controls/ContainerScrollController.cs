// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Owns the mutable scroll geometry and generated scrollbar parts for one container.</summary>
/// <remarks><see cref="Container"/> retains its public scrolling API and layout role. This collaborator
/// owns the committed extent, viewport, offsets, reservation flags, and the private generated bars so
/// no second control subsystem can independently mutate scroll state.</remarks>
internal sealed class ContainerScrollController
{
    /// <summary>Gets or sets the committed non-negative content extent.</summary>
    internal Size Extent;

    /// <summary>Gets or sets the committed non-negative viewport extent.</summary>
    internal Size Viewport;

    /// <summary>Gets or sets the committed horizontal content offset.</summary>
    internal int HorizontalOffset;

    /// <summary>Gets or sets the committed vertical content offset.</summary>
    internal int VerticalOffset;

    /// <summary>Gets or sets the committed physical viewport rectangle.</summary>
    internal Rect ViewportBounds;

    /// <summary>Gets or sets whether the horizontal scrollbar consumes one cell.</summary>
    internal bool ReserveHorizontal;

    /// <summary>Gets or sets whether the vertical scrollbar consumes one cell.</summary>
    internal bool ReserveVertical;

    /// <summary>Gets or sets the private generated scrollbar ownership slot.</summary>
    internal Children? Bars;

    /// <summary>Gets or sets the private horizontal scrollbar.</summary>
    internal ScrollBar? Horizontal;

    /// <summary>Gets or sets the private vertical scrollbar.</summary>
    internal ScrollBar? Vertical;

    /// <summary>Gets or sets whether generated bars are being synchronized from committed state.</summary>
    internal bool IsSynchronizing;
}
