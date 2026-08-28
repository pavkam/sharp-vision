// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Displays labeled grouped values as horizontal bars around a numeric baseline.</summary>
[PublicAPI]
public sealed class HorizontalBarChart: ChartControlBase
{
    /// <summary>Initializes an empty passive horizontal bar chart with automatic scaling.</summary>
    public HorizontalBarChart() : base(ChartScale.Automatic, ChartLegendPlacement.Automatic, true, false)
    {
    }

    /// <summary>Gets or sets legend placement and automatic visibility.</summary>
    public ChartLegendPlacement LegendPlacement { get => LegendPlacementCore; set => LegendPlacementCore = value; }

    /// <summary>Gets or sets whether category labels consume plot width when they fit.</summary>
    public bool ShowCategoryLabels { get => ShowCategoryLabelsCore; set => ShowCategoryLabelsCore = value; }

    /// <summary>Gets or sets whether numeric values are drawn beside bars when they fit.</summary>
    public bool ShowValueLabels { get => ShowValueLabelsCore; set => ShowValueLabelsCore = value; }

    /// <inheritdoc/>
    protected override Size DefaultSize => new(30, 10);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) =>
        BarChartRenderer.Render(this, canvas, ResolvedStyle, Orientation.Horizontal);
}
