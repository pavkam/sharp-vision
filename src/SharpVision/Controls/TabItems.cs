// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Exposes the mutable typed semantic pages owned by one TabControl.</summary>
public sealed class TabItems: IList<TabItem>, IReadOnlyList<TabItem>
{
    private readonly List<TabItem> _items = [];
    private readonly TabControl _owner;

    /// <summary>Initializes an empty collection for one non-null owning TabControl.</summary>
    /// <param name="owner">The owning control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal TabItems(TabControl owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The assigned item is null.</exception>
    public TabItem this[int index]
    {
        get => _items[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _owner.ReplaceTab(this, index, value);
        }
    }

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public void Add(TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.InsertTab(this, Count, item);
    }

    /// <inheritdoc/>
    public void Clear() => _owner.ClearTabs(this);

    /// <inheritdoc/>
    public bool Contains(TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _items.Contains(item);
    }

    /// <inheritdoc/>
    public void CopyTo(TabItem[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        _items.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public IEnumerator<TabItem> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    public int IndexOf(TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _items.IndexOf(item);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public void Insert(int index, TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.InsertTab(this, index, item);
    }

    /// <inheritdoc/>
    public bool Remove(TabItem item)
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
    public void RemoveAt(int index) => _owner.RemoveTab(this, index);

    /// <summary>Commits an owner-validated insertion to the semantic view.</summary>
    internal void InsertAttached(int index, TabItem item) => _items.Insert(index, item);

    /// <summary>Commits an owner-validated removal to the semantic view.</summary>
    internal void RemoveAttached(int index) => _items.RemoveAt(index);

    /// <summary>Commits an owner-validated replacement to the semantic view.</summary>
    internal void ReplaceAttached(int index, TabItem item) => _items[index] = item;

    /// <summary>Clears the semantic view after the owner detaches every page.</summary>
    internal void ClearAttached() => _items.Clear();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
