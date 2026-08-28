// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Displays labeled grouped values as vertical bars around a numeric baseline.</summary>
[PublicAPI]
public sealed class VerticalBarChart: ChartControlBase
{
    /// <summary>Initializes an empty passive vertical bar chart with automatic scaling.</summary>
    public VerticalBarChart() : base(ChartScale.Automatic, ChartLegendPlacement.Automatic, true, false)
    {
    }

    /// <summary>Gets or sets legend placement and automatic visibility.</summary>
    public ChartLegendPlacement LegendPlacement { get => LegendPlacementCore; set => LegendPlacementCore = value; }

    /// <summary>Gets or sets whether category labels consume plot height when they fit.</summary>
    public bool ShowCategoryLabels { get => ShowCategoryLabelsCore; set => ShowCategoryLabelsCore = value; }

    /// <summary>Gets or sets whether numeric values are drawn above bars when they fit.</summary>
    public bool ShowValueLabels { get => ShowValueLabelsCore; set => ShowValueLabelsCore = value; }

    /// <inheritdoc/>
    protected override Size DefaultSize => new(30, 10);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) =>
        BarChartRenderer.Render(this, canvas, ResolvedStyle, Orientation.Vertical);
}
