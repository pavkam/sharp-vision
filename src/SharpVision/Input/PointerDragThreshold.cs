// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Defines the shared cell-space movement boundary between a click and a pointer drag.</summary>
/// <remarks>
/// Terminal pointer coordinates are integral cells, so the first reported cell change crosses the
/// boundary. Consumers use this rule before taking capture from a click-oriented descendant.
/// </remarks>
[PublicAPI]
public static class PointerDragThreshold
{
    /// <summary>Gets the movement in either cell axis that begins a drag.</summary>
    public const int Cells = 1;

    /// <summary>Determines whether movement from an origin has crossed the shared drag boundary.</summary>
    /// <param name="origin">The screen-cell point where the primary press began.</param>
    /// <param name="current">The current screen-cell point.</param>
    /// <returns>
    /// True when either axis moved by at least <see cref="Cells"/>; otherwise false.
    /// </returns>
    [Pure]
    public static bool IsCrossed(Point origin, Point current) =>
        Math.Abs((long) current.X - origin.X) >= Cells ||
        Math.Abs((long) current.Y - origin.Y) >= Cells;
}
