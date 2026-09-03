// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Displays ordered values as connected colored line series.</summary>
[PublicAPI]
public sealed class LineChart: CartesianChartControlBase
{
    /// <summary>Initializes an empty focusable line chart with automatic trend scaling.</summary>
    public LineChart() : base(new ChartScale(null, null, includeZero: false))
    {
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) =>
        LineChartRenderer.Render(this, canvas, ResolvedStyle);
}
