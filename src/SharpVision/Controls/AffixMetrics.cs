// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Styling;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Reports the reserved cell columns an edge-pinned <see cref="Affix"/> pair occupies,
/// gap included, as measured by <see cref="ControlBase.MeasureAffixes"/>.</summary>
/// <remarks>
/// Public rather than the narrower accessibility a purely-internal helper would use: it is the
/// return type of the protected <see cref="ControlBase.MeasureAffixes"/> seam, so it must be at
/// least as accessible as every derived control - including a third-party control declared in
/// another assembly - that calls it. This is the same reason <see cref="StyleSlot{TStyle}"/> is
/// public rather than internal despite existing only to support protected style-initialization
/// members.
/// </remarks>
[PublicAPI]
public readonly record struct AffixMetrics
{
    /// <summary>Initializes validated reserved cell columns.</summary>
    /// <param name="startCells">The non-negative reserved leading columns.</param>
    /// <param name="endCells">The non-negative reserved trailing columns.</param>
    /// <exception cref="ArgumentOutOfRangeException">A column count is negative.</exception>
    public AffixMetrics([NonNegativeValue] int startCells, [NonNegativeValue] int endCells)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startCells);
        ArgumentOutOfRangeException.ThrowIfNegative(endCells);

        StartCells = startCells;
        EndCells = endCells;
    }

    /// <summary>Gets the reserved leading columns; zero when the start affix is unset.</summary>
    [NonNegativeValue]
    public int StartCells { get; }

    /// <summary>Gets the reserved trailing columns; zero when the end affix is unset.</summary>
    [NonNegativeValue]
    public int EndCells { get; }
}
