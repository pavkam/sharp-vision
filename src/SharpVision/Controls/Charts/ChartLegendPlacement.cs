// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Specifies where a chart legend consumes available cells.</summary>
[PublicAPI]
public enum ChartLegendPlacement
{
    /// <summary>Shows a bottom legend when multiple named series and sufficient cells exist.</summary>
    Automatic,
    /// <summary>Suppresses the legend.</summary>
    Hidden,
    /// <summary>Places the legend above the plot.</summary>
    Top,
    /// <summary>Places the legend below the plot.</summary>
    Bottom,
    /// <summary>Places the legend to the left of the plot.</summary>
    Left,
    /// <summary>Places the legend to the right of the plot.</summary>
    Right
}
