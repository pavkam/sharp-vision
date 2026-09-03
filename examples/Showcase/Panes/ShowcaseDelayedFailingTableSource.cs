// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Provides a deliberately slow table source whose first leading range fails once.</summary>
internal sealed class ShowcaseDelayedFailingTableSource: ITableDataSource<int>
{
    private readonly HashSet<int> _failedOnce = [];
    private readonly int _total;
    private int _fetchCount;

    /// <summary>Initializes a source with one non-negative logical row count.</summary>
    /// <param name="total">The logical row count.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="total"/> is negative.</exception>
    internal ShowcaseDelayedFailingTableSource(int total)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(total);
        _total = total;
    }

    /// <summary>Gets the number of load calls issued so far.</summary>
    internal int FetchCount => _fetchCount;

    /// <inheritdoc/>
    public int? Count => _total;

    /// <inheritdoc/>
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public object GetKey(int item) => item;

    /// <inheritdoc/>
    public async ValueTask<TableDataResult<int>> LoadAsync(
        TableDataRequest request,
        CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _fetchCount);
        await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);

        if (request.StartIndex == 0 && _failedOnce.Add(request.StartIndex))
        {
            throw new InvalidOperationException("Simulated transient failure for the showcase specimen.");
        }

        var count = Math.Min(request.Count, _total - request.StartIndex);
        var items = Enumerable.Range(request.StartIndex, count).ToArray();
        return new TableDataResult<int>
        {
            Items = items,
            IsEndOfData = request.StartIndex + count >= _total
        };
    }
}
