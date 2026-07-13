// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Collections;

/// <summary>Owns validated mutable column definitions for one Table.</summary>
public sealed class TableColumns: IList<TableColumn>, IReadOnlyList<TableColumn>
{
    private readonly List<TableColumn> _items = [];
    private readonly Table _owner;

    /// <summary>Initializes a collection for one non-null owning table.</summary>
    /// <param name="owner">The owning table.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal TableColumns(Table owner)
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

            if (_items[index] == value)
            {
                return;
            }

            _items[index] = value;
            _owner.ColumnsChanged();
        }
    }

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public void Add(TableColumn item)
    {
        _owner.ValidateColumnCount(_items.Count + 1);
        _items.Add(item);
        _owner.ColumnsChanged();
    }

    /// <inheritdoc/>
    public void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }

        _owner.ValidateColumnCount(0);
        _items.Clear();
        _owner.ColumnsChanged();
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
        _owner.ValidateColumnCount(_items.Count + 1);
        _items.Insert(index, item);
        _owner.ColumnsChanged();
    }

    /// <inheritdoc/>
    public bool Remove(TableColumn item)
    {
        int index = _items.IndexOf(item);

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
        _items.RemoveAt(index);
        _owner.ColumnsChanged();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
