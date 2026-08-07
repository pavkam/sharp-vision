// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using System.Collections.ObjectModel;

/// <summary>Provides observable series membership for the line-chart binding specimen.</summary>
internal sealed class ChartShowcaseModel
{
    private readonly ObservableCollection<ChartSeries> _series = [];

    /// <summary>Gets the observable series exposed through the public binding path.</summary>
    public IReadOnlyList<ChartSeries>? Series => _series;

    /// <summary>Adds one series and publishes an observable collection change.</summary>
    internal void Add(ChartSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        _series.Add(series);
    }
}
