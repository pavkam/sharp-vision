// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Contains the resolved plot and optional legend rectangles for one chart frame.</summary>
internal readonly struct ChartPlotLayout
{
    /// <summary>Initializes resolved chart layout rectangles.</summary>
    internal ChartPlotLayout(Rect plot, Rect legend)
    {
        Plot = plot;
        Legend = legend;
    }

    /// <summary>Gets the data plot rectangle.</summary>
    internal Rect Plot { get; }

    /// <summary>Gets the optional legend rectangle.</summary>
    internal Rect Legend { get; }
}
