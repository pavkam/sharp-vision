// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Displays labeled grouped values as vertical bars around a numeric baseline.</summary>
[PublicAPI]
public sealed class VerticalBarChart: CartesianChartControlBase
{
    /// <summary>Initializes an empty focusable vertical bar chart with automatic scaling.</summary>
    public VerticalBarChart() : base(ChartScale.Automatic)
    {
    }

    /// <inheritdoc/>
    private protected override bool TryHitTestSelection(Point position, out ChartSelection selection) =>
        BarChartRenderer.TryHitTestSelection(this, position, Orientation.Vertical, out selection);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) =>
        BarChartRenderer.Render(this, canvas, ResolvedStyle, Orientation.Vertical);
}
