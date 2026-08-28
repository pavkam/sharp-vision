// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Displays ordered values as connected colored line series.</summary>
[PublicAPI]
public sealed class LineChart: ChartControlBase
{
    /// <summary>Initializes an empty passive line chart with automatic trend scaling.</summary>
    public LineChart() : base(new ChartScale(null, null, includeZero: false), ChartLegendPlacement.Automatic, true, false)
    {
    }

    /// <summary>Gets or sets legend placement and automatic visibility.</summary>
    public ChartLegendPlacement LegendPlacement { get => LegendPlacementCore; set => LegendPlacementCore = value; }

    /// <summary>Gets or sets whether category labels consume plot cells when they fit.</summary>
    public bool ShowCategoryLabels { get => ShowCategoryLabelsCore; set => ShowCategoryLabelsCore = value; }

    /// <summary>Gets or sets whether numeric point values are drawn when they fit.</summary>
    public bool ShowValueLabels { get => ShowValueLabelsCore; set => ShowValueLabelsCore = value; }

    /// <inheritdoc/>
    protected override Size DefaultSize => new(30, 10);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) =>
        LineChartRenderer.Render(this, canvas, ResolvedStyle);
}
