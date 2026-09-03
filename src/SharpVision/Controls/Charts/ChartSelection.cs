// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Identifies one chart point by its zero-based series and point indices.</summary>
[PublicAPI]
public readonly record struct ChartSelection
{
    /// <summary>Initializes one validated chart point identity.</summary>
    /// <param name="seriesIndex">The zero-based series index.</param>
    /// <param name="pointIndex">The zero-based point index within the series.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="seriesIndex"/> or <paramref name="pointIndex"/> is negative.
    /// </exception>
    public ChartSelection(int seriesIndex, int pointIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seriesIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(pointIndex);
        SeriesIndex = seriesIndex;
        PointIndex = pointIndex;
    }

    /// <summary>Gets the zero-based series index.</summary>
    [NonNegativeValue]
    public int SeriesIndex { get; }

    /// <summary>Gets the zero-based point index within the series.</summary>
    [NonNegativeValue]
    public int PointIndex { get; }
}
