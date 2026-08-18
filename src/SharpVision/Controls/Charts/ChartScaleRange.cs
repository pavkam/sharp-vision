// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Contains one resolved non-empty finite chart range.</summary>
internal readonly struct ChartScaleRange
{
    /// <summary>Initializes a resolved chart range.</summary>
    /// <param name="minimum">The finite lower bound.</param>
    /// <param name="maximum">The finite upper bound above <paramref name="minimum"/>.</param>
    internal ChartScaleRange(double minimum, double maximum)
    {
        Debug.Assert(double.IsFinite(minimum));
        Debug.Assert(double.IsFinite(maximum));
        Debug.Assert(minimum < maximum);
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>Gets the resolved lower bound.</summary>
    internal double Minimum { get; }

    /// <summary>Gets the resolved upper bound.</summary>
    internal double Maximum { get; }
}
