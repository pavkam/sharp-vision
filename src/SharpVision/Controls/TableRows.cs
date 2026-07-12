using System.Collections;

namespace SharpVision.Controls;

/// <summary>Owns validated rows and transfers their cell controls into one Table.</summary>
public sealed class TableRows: IList<TableRow>, IReadOnlyList<TableRow>
{
    private readonly List<TableRow> _items = [];
    private readonly Table _owner;

    /// <summary>Initializes a collection for one non-null owning table.</summary>
    /// <param name="owner">The owning table.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal TableRows(Table owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <inheritdoc/>
    public TableRow this[int index]
    {
        get => _items[index];
        set => _owner.ReplaceRow(this, index, value);
    }

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public void Add(TableRow item) => _owner.InsertRow(this, Count, item);

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
    public void Insert(int index, TableRow item) => _owner.InsertRow(this, index, item);

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
    internal void InsertAttached(int index, TableRow row) => _items.Insert(index, row);

    /// <summary>Removes a row after the owner has detached its cells.</summary>
    /// <param name="index">The valid row index.</param>
    internal void RemoveAttached(int index) => _items.RemoveAt(index);

    /// <summary>Clears rows after the owner has detached their cells.</summary>
    internal void ClearAttached() => _items.Clear();

    /// <summary>Replaces one row after the owner has completed ownership transfer.</summary>
    /// <param name="index">The valid row index.</param>
    /// <param name="row">The validated new row.</param>
    internal void ReplaceAttached(int index, TableRow row) => _items[index] = row;

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
