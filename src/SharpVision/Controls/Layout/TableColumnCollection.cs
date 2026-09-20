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
            _owner.ValidateProgressiveColumn(value);
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
        _owner.ValidateProgressiveColumn(item);
        _owner.ValidateColumnCount(_items.Count + 1);
        var sortedColumn = _owner.GetSortedColumn(out var sortedIndex);
        _items.Add(item);
        _owner.ColumnsChanged(sortedColumn, sortedIndex);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The owning table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owning table is disposed.</exception>
    public void Clear()
    {
        // The mutability guard must run before the empty-collection short circuit below, or
        // clearing an already-empty collection on a disposed or off-dispatcher table would
        // silently succeed instead of reporting the owner's terminal or thread state.
        _owner.VerifyMutable();

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
        _owner.ValidateProgressiveColumn(item);
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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current columns.</exception>
    /// <exception cref="InvalidOperationException">The owning table is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owning table is disposed.</exception>
    public void RemoveAt(int index)
    {
        // The mutability guard must run before the index is read, or an out-of-range index on a
        // disposed or off-dispatcher table would report ArgumentOutOfRangeException instead of the
        // owner's terminal or thread state. ValidateColumnCount is not used for the guard itself
        // here: it evaluates the hypothetical post-removal count against every existing row, which
        // is only meaningful once the index below is already known to be valid.
        _owner.VerifyMutable();
        _ = _items[index];
        _owner.ValidateColumnCount(_items.Count - 1);
        var sortedColumn = _owner.GetSortedColumn(out var sortedIndex);
        _items.RemoveAt(index);
        _owner.ColumnsChanged(sortedColumn, sortedIndex);
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
