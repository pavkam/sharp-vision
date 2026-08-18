// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Supplies progressively, index-addressed data to one progressive <see cref="Table"/>.</summary>
/// <typeparam name="T">The item type loaded from this source.</typeparam>
/// <remarks>
/// A table bound through <see cref="Table.SetDataSource{T}"/> never assumes every item is loaded:
/// it requests bounded contiguous ranges as the viewport scrolls and caches only what
/// <see cref="LoadAsync"/> already returned. Every member here may be called from the table's
/// owning dispatcher thread only.
/// </remarks>
[PublicAPI]
public interface ITableDataSource<T>
{
    /// <summary>Gets the known total item count, or <see langword="null"/> while the total is not yet known.</summary>
    public int? Count { get; }

    /// <summary>Gets one stable identity for an item, independent of its current logical index.</summary>
    /// <param name="item">An item previously returned by <see cref="LoadAsync"/>.</param>
    /// <returns>The non-null stable key.</returns>
    public object GetKey(T item);

    /// <summary>Loads one contiguous range of items.</summary>
    /// <param name="request">The requested contiguous range.</param>
    /// <param name="cancellationToken">Cancels the request once its range is no longer needed.</param>
    /// <returns>The items loaded starting at exactly <see cref="TableDataRequest.StartIndex"/>.</returns>
    public ValueTask<TableDataResult<T>> LoadAsync(TableDataRequest request, CancellationToken cancellationToken);

    /// <summary>Raised when previously loaded data may be stale; a subscriber typically calls
    /// <see cref="Table.Reload"/> in response.</summary>
    public event EventHandler? Changed;
}
