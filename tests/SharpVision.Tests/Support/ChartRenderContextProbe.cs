// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Minimal chart derived directly from <see cref="ChartControlBase"/>, proving a
/// third-party chart can author its own rendering through the public
/// <see cref="ChartControlBase.CreateRenderContext(TerminalCanvas)"/> seam and
/// <see cref="ChartRenderContext"/> without going through any shipped renderer.</summary>
internal sealed class ChartRenderContextProbe: ChartControlBase
{
    /// <summary>Initializes an empty probe chart with an explicit zero-to-ten scale.</summary>
    internal ChartRenderContextProbe()
        : base(new ChartScale(0, 10, includeZero: false), ChartLegendPlacement.Hidden, showCategoryLabels: false, showValueLabels: false)
    {
    }

    /// <inheritdoc/>
    protected override Size DefaultSize => new(10, 5);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Series.Count == 0)
        {
            return;
        }

        var context = CreateRenderContext(canvas);
        var points = Series[0].Points;

        for (var index = 0; index < points.Count; index++)
        {
            var position = new Point(context.MapX(index, points.Count), context.MapY(points[index].Value));
            canvas.DrawRune(new Rune('*'), position, context.InheritedStyle);
        }
    }
}
