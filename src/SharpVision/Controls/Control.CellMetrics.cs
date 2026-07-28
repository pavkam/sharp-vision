// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

using CellMetrics = Metrics;

/// <summary>Provides inherited protocol-neutral terminal cell geometry.</summary>
public abstract partial class Control
{
    /// <summary>Gets exact terminal cell-pixel geometry inherited from the application, when available.</summary>
    protected CellMetrics? CellMetrics { get; private set; }

    /// <summary>Gets inherited exact metrics for framework-owned descendants.</summary>
    internal CellMetrics? CellMetricsContext => CellMetrics;

    /// <summary>Publishes exact cell geometry across this complete subtree.</summary>
    /// <param name="value">Exact measured geometry, or null when unavailable.</param>
    /// <exception cref="InvalidOperationException">The attached tree is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">This control or an owned descendant is disposed.</exception>
    internal void SetCellMetrics(CellMetrics? value)
    {
        VerifyMutable();
        var changed = new List<(Control Control, CellMetrics? Previous)>();
        CommitCellMetrics(value, changed);
        ExceptionDispatchInfo? failure = null;

        foreach (var entry in changed)
        {
            ExceptionAggregation.Capture(
                () => entry.Control.OnCellMetricsChanged(entry.Previous, value),
                ref failure);
        }

        failure?.Throw();
    }

    /// <summary>Responds after inherited exact cell geometry commits.</summary>
    /// <param name="previous">The previous exact geometry, or null.</param>
    /// <param name="current">The committed exact geometry, or null.</param>
    protected virtual void OnCellMetricsChanged(CellMetrics? previous, CellMetrics? current)
    {
        _ = previous;
        _ = current;
    }

    private void CommitCellMetrics(
        CellMetrics? value,
        List<(Control Control, CellMetrics? Previous)> changed)
    {
        ThrowIfDisposed();
        var previous = CellMetrics;

        if (previous != value)
        {
            CellMetrics = value;
            changed.Add((this, previous));
        }

        VisitChildren(child => child.CommitCellMetrics(value, changed));
    }
}
