// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Contains the resolved plot and optional legend rectangles for one chart frame.</summary>
[PublicAPI]
public readonly struct ChartPlotLayout
{
    /// <summary>Initializes resolved chart layout rectangles.</summary>
    /// <param name="plot">The resolved data plot rectangle.</param>
    /// <param name="legend">The resolved optional legend rectangle.</param>
    internal ChartPlotLayout(Rect plot, Rect legend)
    {
        Plot = plot;
        Legend = legend;
    }

    /// <summary>Gets the data plot rectangle.</summary>
    public Rect Plot { get; }

    /// <summary>Gets the optional legend rectangle. Empty when the resolved legend policy hides
    /// the legend or the bounds are too small to reserve one.</summary>
    public Rect Legend { get; }
}
