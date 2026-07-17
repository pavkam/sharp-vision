// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Exposes one tab control's constrained page collection.</summary>
public sealed class TabItems: IReadOnlyList<TabItem>
{
    private readonly TabControl _owner;

    internal TabItems(TabControl owner) { ArgumentNullException.ThrowIfNull(owner); _owner = owner; }

    /// <inheritdoc/>
    public TabItem this[int index] => _owner.ItemAt(index);

    /// <inheritdoc/>
    public int Count => _owner.ItemCount;

    /// <summary>Adds one detached non-null tab item.</summary>
    public void Add(TabItem item) { ArgumentNullException.ThrowIfNull(item); _owner.AddItem(item); }

    /// <summary>Removes one owned tab item.</summary>
    public bool Remove(TabItem item) { ArgumentNullException.ThrowIfNull(item); return _owner.RemoveItem(item); }

    /// <summary>Removes every owned tab item.</summary>
    public void Clear() => _owner.ClearItems();

    /// <inheritdoc/>
    public IEnumerator<TabItem> GetEnumerator() { for (var i = 0; i < Count; i++) { yield return this[i]; } }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
