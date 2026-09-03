// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

/// <summary>Displays one compact ordered series with sub-cell vertical resolution.</summary>
[PublicAPI]
public sealed class Sparkline: ChartControlBase
{
    /// <summary>Initializes an empty focusable sparkline with automatic trend scaling.</summary>
    public Sparkline() : base(new ChartScale(null, null, includeZero: false), ChartLegendPlacement.Hidden, false, false)
    {
    }

    /// <inheritdoc/>
    protected override ChartLegendPlacement ResolveLegendPlacement() => ChartLegendPlacement.Hidden;

    /// <inheritdoc/>
    protected override bool ResolveShowCategoryLabels() => false;

    /// <inheritdoc/>
    protected override bool ResolveShowValueLabels() => false;

    /// <inheritdoc/>
    protected override void ValidateSeriesCore(IReadOnlyList<ChartSeries> series)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (series.Count > 1)
        {
            throw new ArgumentException("A sparkline accepts at most one series.", nameof(series));
        }
    }

    /// <inheritdoc/>
    protected override Size DefaultSize => new(20, 1);

    /// <inheritdoc/>
    private protected override bool TryHitTestSelection(Point position, out ChartSelection selection) =>
        SparklineRenderer.TryHitTestSelection(this, position, out selection);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) =>
        SparklineRenderer.Render(this, canvas, ResolvedStyle);
}
