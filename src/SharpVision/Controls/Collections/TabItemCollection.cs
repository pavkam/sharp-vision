// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using CollectionAccess = JetBrains.Annotations.CollectionAccessAttribute;
using CollectionAccessType = JetBrains.Annotations.CollectionAccessType;

/// <summary>Exposes one tab control's constrained page collection.</summary>
[PublicAPI]
public sealed class TabItemCollection: IReadOnlyList<TabItem>
{
    private readonly TabControl _owner;

    internal TabItemCollection(TabControl owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <inheritdoc/>
    [CollectionAccess(CollectionAccessType.Read)]
    public int Count => _owner.ItemCount;

    /// <summary>Gets or replaces one owned tab item, preserving its position.</summary>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current items.</exception>
    /// <exception cref="ArgumentException">The assigned item is already owned or is attached elsewhere.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner or the assigned item is disposed.</exception>
    [CollectionAccess(CollectionAccessType.Read | CollectionAccessType.ModifyExistingContent)]
    public TabItem this[int index]
    {
        get => _owner.ItemAt(index);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _owner.ReplaceItem(index, value);
        }
    }

    /// <summary>Adds one detached non-null tab item.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item is already owned or is attached elsewhere.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner or item is disposed.</exception>
    [CollectionAccess(CollectionAccessType.UpdatedContent)]
    public void Add(TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.AddItem(item);
    }

    /// <summary>Inserts one detached non-null tab item at a position.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    /// <exception cref="ArgumentException">The item is already owned or is attached elsewhere.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner or item is disposed.</exception>
    [CollectionAccess(CollectionAccessType.UpdatedContent)]
    public void Insert(int index, TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.InsertItem(index, item);
    }

    /// <summary>Removes one owned tab item.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    [CollectionAccess(CollectionAccessType.ModifyExistingContent)]
    public bool Remove(TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _owner.RemoveItem(item);
    }

    /// <summary>Removes the owned tab item at a position.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current items.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    [CollectionAccess(CollectionAccessType.ModifyExistingContent)]
    public void RemoveAt(int index) => _owner.RemoveItemAt(index);

    /// <summary>Moves one owned tab item to a different position, preserving its identity and selection.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="oldIndex"/> or <paramref name="newIndex"/> is outside the current items.
    /// </exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    [CollectionAccess(CollectionAccessType.ModifyExistingContent)]
    public void Move(int oldIndex, int newIndex) => _owner.MoveItem(oldIndex, newIndex);

    /// <summary>Gets the position of one tab item, or -1 when it is not owned by this control.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    [CollectionAccess(CollectionAccessType.Read)]
    public int IndexOf(TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _owner.IndexOfItem(item);
    }

    /// <summary>Removes every owned tab item.</summary>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    [CollectionAccess(CollectionAccessType.ModifyExistingContent)]
    public void Clear() => _owner.ClearItems();

    /// <inheritdoc/>
    [CollectionAccess(CollectionAccessType.Read)]
    public IEnumerator<TabItem> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
