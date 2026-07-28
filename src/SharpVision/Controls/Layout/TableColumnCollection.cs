// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Owns validated mutable column definitions for one Table.</summary>
[PublicAPI]
public sealed class TableColumnCollection: IList<TableColumn>, IReadOnlyList<TableColumn>
{
    private readonly List<TableColumn> _items = [];
    private readonly Table _owner;

    /// <summary>Initializes a collection for one non-null owning table.</summary>
    /// <param name="owner">The owning table.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal TableColumnCollection(Table owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <inheritdoc/>
    public TableColumn this[int index]
    {
        get => _items[index];
        set
        {
            _owner.ValidateColumnCount(_items.Count);
            var sortedColumn = _owner.GetSortedColumn(out var sortedIndex);

            if (_items[index] == value)
            {
                return;
            }

            Table.ValidateColumn(value);
            _items[index] = value;
            _owner.ColumnsChanged(sortedColumn, sortedIndex);
        }
    }

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public void Add(TableColumn item)
    {
        Table.ValidateColumn(item);
        _owner.ValidateColumnCount(_items.Count + 1);
        var sortedColumn = _owner.GetSortedColumn(out var sortedIndex);
        _items.Add(item);
        _owner.ColumnsChanged(sortedColumn, sortedIndex);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }

        _owner.ValidateColumnCount(0);
        var sortedColumn = _owner.GetSortedColumn(out var sortedIndex);
        _items.Clear();
        _owner.ColumnsChanged(sortedColumn, sortedIndex);
    }

    /// <inheritdoc/>
    public bool Contains(TableColumn item) => _items.Contains(item);

    /// <inheritdoc/>
    public void CopyTo(TableColumn[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        _items.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public IEnumerator<TableColumn> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    public int IndexOf(TableColumn item) => _items.IndexOf(item);

    /// <inheritdoc/>
    public void Insert(int index, TableColumn item)
    {
        Table.ValidateColumn(item);
        _owner.ValidateColumnCount(_items.Count + 1);
        var sortedColumn = _owner.GetSortedColumn(out var sortedIndex);
        _items.Insert(index, item);
        _owner.ColumnsChanged(sortedColumn, sortedIndex);
    }

    /// <inheritdoc/>
    public bool Remove(TableColumn item)
    {
        var index = _items.IndexOf(item);

        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        return true;
    }

    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        _ = _items[index];
        _owner.ValidateColumnCount(_items.Count - 1);
        var sortedColumn = _owner.GetSortedColumn(out var sortedIndex);
        _items.RemoveAt(index);
        _owner.ColumnsChanged(sortedColumn, sortedIndex);
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
