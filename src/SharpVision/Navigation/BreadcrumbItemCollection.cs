// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using CollectionAccess = JetBrains.Annotations.CollectionAccessAttribute;
using CollectionAccessType = JetBrains.Annotations.CollectionAccessType;

/// <summary>Exposes one breadcrumb's constrained retained path collection.</summary>
[PublicAPI]
public sealed class BreadcrumbItemCollection: IReadOnlyList<BreadcrumbItem>
{
    private readonly Breadcrumb _owner;

    /// <summary>Initializes a collection facade for an exact owner.</summary>
    /// <param name="owner">The non-null breadcrumb owner.</param>
    internal BreadcrumbItemCollection(Breadcrumb owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <inheritdoc/>
    [CollectionAccess(CollectionAccessType.Read)]
    public int Count => _owner.ItemCount;

    /// <summary>Gets or replaces one retained item while preserving its position.</summary>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the path.</exception>
    /// <exception cref="ArgumentException">The assigned item cannot be owned by this breadcrumb.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher or during an ownership transaction.</exception>
    /// <exception cref="ObjectDisposedException">The owner or assigned item is disposed.</exception>
    public BreadcrumbItem this[int index]
    {
        get => _owner.ItemAt(index);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _owner.ReplaceItem(index, value);
        }
    }

    /// <summary>Adds one detached item to the end of the path.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item cannot be owned by this breadcrumb.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher or during an ownership transaction.</exception>
    /// <exception cref="ObjectDisposedException">The owner or item is disposed.</exception>
    public void Add(BreadcrumbItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.AddItem(item);
    }

    /// <summary>Inserts one detached item at a path position.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    /// <exception cref="ArgumentException">The item cannot be owned by this breadcrumb.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher or during an ownership transaction.</exception>
    /// <exception cref="ObjectDisposedException">The owner or item is disposed.</exception>
    public void Insert(int index, BreadcrumbItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.InsertItem(index, item);
    }

    /// <summary>Removes an identical owned item without disposing it.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public bool Remove(BreadcrumbItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _owner.RemoveItem(item);
    }

    /// <summary>Removes the item at a path position without disposing it.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the path.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public void RemoveAt(int index) => _owner.RemoveItemAt(index);

    /// <summary>Moves an owned item while preserving identity and semantic current state.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An index is outside the path.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public void Move(int oldIndex, int newIndex) => _owner.MoveItem(oldIndex, newIndex);

    /// <summary>Gets an item's identity position, or -1 when it is not owned here.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    [CollectionAccess(CollectionAccessType.Read)]
    public int IndexOf(BreadcrumbItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _owner.IndexOfItem(item);
    }

    /// <summary>Detaches every retained item without disposing it.</summary>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public void Clear() => _owner.ClearItems();

    /// <inheritdoc/>
    public IEnumerator<BreadcrumbItem> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
