// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using CollectionAccess = JetBrains.Annotations.CollectionAccessAttribute;
using CollectionAccessType = JetBrains.Annotations.CollectionAccessType;

/// <summary>Exposes one tab control's constrained page collection.</summary>
[PublicAPI]
public sealed class TabItemCollection: ItemCollection<TabItem>
{
    private readonly TabControl _owner;

    internal TabItemCollection(TabControl owner)
        : base(owner) =>
        _owner = owner;

    /// <summary>Gets or replaces one owned tab item, preserving its position.</summary>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current items.</exception>
    /// <exception cref="ArgumentException">The assigned item is already owned or is attached elsewhere.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner or the assigned item is disposed.</exception>
    [CollectionAccess(CollectionAccessType.Read | CollectionAccessType.ModifyExistingContent)]
    public override TabItem this[int index]
    {
        get => base[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _owner.ReplaceItem(index, value);
        }
    }

    /// <summary>Inserts one detached non-null tab item at a position.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    /// <exception cref="ArgumentException">The item is already owned or is attached elsewhere.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner or item is disposed.</exception>
    [CollectionAccess(CollectionAccessType.UpdatedContent)]
    public override void Insert(int index, TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.InsertItem(index, item);
    }

    /// <summary>Removes one owned tab item.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    [CollectionAccess(CollectionAccessType.ModifyExistingContent)]
    public override bool Remove(TabItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _owner.RemoveItem(item);
    }

    /// <summary>Removes the owned tab item at a position.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current items.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    [CollectionAccess(CollectionAccessType.ModifyExistingContent)]
    public override void RemoveAt(int index) => _owner.RemoveItemAt(index);

    /// <summary>Moves one owned tab item to a different position, preserving its identity and selection.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="oldIndex"/> or <paramref name="newIndex"/> is outside the current items.
    /// </exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    [CollectionAccess(CollectionAccessType.ModifyExistingContent)]
    public override void Move(int oldIndex, int newIndex) => _owner.MoveItem(oldIndex, newIndex);

    /// <summary>Removes every owned tab item.</summary>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    [CollectionAccess(CollectionAccessType.ModifyExistingContent)]
    public override void Clear() => _owner.ClearItems();
}
