// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using CellMetrics = Terminal.Geometry.Metrics;

/// <summary>Records inherited exact cell-metric context and measure observations.</summary>
internal sealed class CellMetricsProbe: ControlBase
{
    /// <summary>Gets the current inherited exact metrics.</summary>
    internal CellMetrics? InheritedMetrics => CellMetrics;

    /// <summary>Gets metric transitions published after context commit.</summary>
    internal List<CellMetrics?> Transitions { get; } = [];

    /// <summary>Gets the metric value visible during every measure callback.</summary>
    internal List<CellMetrics?> MeasureMetrics { get; } = [];

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        MeasureMetrics.Add(CellMetrics);
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void OnCellMetricsChanged(CellMetrics? previous, CellMetrics? current)
    {
        _ = previous;
        Transitions.Add(current);
    }
}
