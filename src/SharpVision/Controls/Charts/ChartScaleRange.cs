// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Contains one resolved non-empty finite chart range.</summary>
internal readonly struct ChartScaleRange
{
    /// <summary>Initializes a resolved chart range.</summary>
    /// <param name="minimum">The finite lower bound.</param>
    /// <param name="maximum">The finite upper bound above <paramref name="minimum"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either bound is not finite.</exception>
    /// <exception cref="ArgumentException"><paramref name="minimum"/> is not below
    /// <paramref name="maximum"/>.</exception>
    internal ChartScaleRange(double minimum, double maximum)
    {
        if (!double.IsFinite(minimum))
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), minimum, "The minimum must be finite.");
        }

        if (!double.IsFinite(maximum))
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "The maximum must be finite.");
        }

        if (minimum >= maximum)
        {
            throw new ArgumentException("The minimum must be below the maximum.", nameof(minimum));
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>Gets the resolved lower bound.</summary>
    internal double Minimum { get; }

    /// <summary>Gets the resolved upper bound.</summary>
    internal double Maximum { get; }
}
