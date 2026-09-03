// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Displays ordered values as connected colored lines with baseline fill.</summary>
[PublicAPI]
public sealed class AreaChart: CartesianChartControlBase
{
    /// <summary>Initializes an empty focusable area chart with automatic trend scaling.</summary>
    public AreaChart() : base(new ChartScale(null, null, includeZero: false))
    {
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) =>
        AreaChartRenderer.Render(this, canvas, ResolvedStyle);
}
