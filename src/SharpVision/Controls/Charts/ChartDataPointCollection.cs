// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

using System.Collections.ObjectModel;

/// <summary>Owns one series' observable, duplicate-free chart data points.</summary>
[PublicAPI]
public sealed class ChartDataPointCollection: ObservableCollection<ChartDataPoint>
{
    /// <summary>Initializes an empty point collection.</summary>
    public ChartDataPointCollection()
    {
    }

    /// <summary>Initializes a point collection from validated non-null unique references.</summary>
    /// <param name="points">The points to copy in enumeration order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="points"/> or one point is null.</exception>
    /// <exception cref="ArgumentException">One point reference is repeated.</exception>
    public ChartDataPointCollection(IEnumerable<ChartDataPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        foreach (var point in points)
        {
            Add(point);
        }
    }

    /// <inheritdoc/>
    protected override void InsertItem(int index, ChartDataPoint item)
    {
        ArgumentNullException.ThrowIfNull(item);
        RejectDuplicate(item, ignoredIndex: -1);
        base.InsertItem(index, item);
    }

    /// <inheritdoc/>
    protected override void SetItem(int index, ChartDataPoint item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (ReferenceEquals(this[index], item))
        {
            return;
        }

        RejectDuplicate(item, index);
        base.SetItem(index, item);
    }

    private void RejectDuplicate(ChartDataPoint item, int ignoredIndex)
    {
        for (var index = 0; index < Count; index++)
        {
            if (index != ignoredIndex && ReferenceEquals(this[index], item))
            {
                throw new ArgumentException("A chart series cannot contain the same point reference twice.", nameof(item));
            }
        }
    }
}
