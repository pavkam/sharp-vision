// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Contains resolved immutable inputs shared by one chart render pass.</summary>
internal readonly struct ChartRenderContext
{
    /// <summary>Initializes one chart render context.</summary>
    internal ChartRenderContext(
        IChartControl chart,
        ChartPlotLayout layout,
        ChartScaleRange range,
        TerminalStyle inheritedStyle)
    {
        Chart = chart;
        Layout = layout;
        Range = range;
        InheritedStyle = inheritedStyle;
    }

    /// <summary>Gets the chart contract.</summary>
    internal IChartControl Chart { get; }

    /// <summary>Gets the resolved plot layout.</summary>
    internal ChartPlotLayout Layout { get; }

    /// <summary>Gets the resolved numeric range.</summary>
    internal ChartScaleRange Range { get; }

    /// <summary>Gets the inherited terminal cell style.</summary>
    internal TerminalStyle InheritedStyle { get; }
}
