// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Owns validated rows and transfers their cell controls into one Table.</summary>
[PublicAPI]
public sealed class TableRowCollection: IList<TableRow>, IReadOnlyList<TableRow>
{
    private readonly List<TableRow> _items = [];
    private readonly Table _owner;

    /// <summary>Initializes a collection for one non-null owning table.</summary>
    /// <param name="owner">The owning table.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal TableRowCollection(Table owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    public TableRow this[int index]
    {
        get => _items[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _owner.ReplaceRow(this, index, value);
        }
    }

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public void Add(TableRow item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.InsertRow(this, Count, item);
    }

    /// <inheritdoc/>
    public void Clear() => _owner.ClearRows(this);

    /// <inheritdoc/>
    public bool Contains(TableRow item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _items.Contains(item);
    }

    /// <inheritdoc/>
    public void CopyTo(TableRow[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        _items.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public IEnumerator<TableRow> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    public int IndexOf(TableRow item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _items.IndexOf(item);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public void Insert(int index, TableRow item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.InsertRow(this, index, item);
    }

    /// <inheritdoc/>
    public bool Remove(TableRow item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var index = _items.IndexOf(item);

        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        return true;
    }

    /// <inheritdoc/>
    public void RemoveAt(int index) => _owner.RemoveRow(this, index);

    /// <summary>Adds a row after the owner has validated and attached its cells.</summary>
    /// <param name="index">The validated insertion index.</param>
    /// <param name="row">The validated row.</param>
    internal void InsertAttached(int index, TableRow row)
    {
        Debug.Assert((uint) index <= (uint) _items.Count, "Attached row insertion requires a valid collection index.");
        Debug.Assert(row is not null, "Only a validated non-null row reaches the attachment boundary.");

        _items.Insert(index, row);
    }

    /// <summary>Removes a row after the owner has detached its cells.</summary>
    /// <param name="index">The valid row index.</param>
    internal void RemoveAttached(int index)
    {
        Debug.Assert((uint) index < (uint) _items.Count, "Attached row removal requires an existing collection index.");

        _items.RemoveAt(index);
    }

    /// <summary>Clears rows after the owner has detached their cells.</summary>
    internal void ClearAttached() => _items.Clear();

    /// <summary>Replaces one row after the owner has completed ownership transfer.</summary>
    /// <param name="index">The valid row index.</param>
    /// <param name="row">The validated new row.</param>
    internal void ReplaceAttached(int index, TableRow row)
    {
        Debug.Assert((uint) index < (uint) _items.Count,
            "Attached row replacement requires an existing collection index.");
        Debug.Assert(row is not null, "Only a validated non-null row reaches the replacement boundary.");

        _items[index] = row;
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
