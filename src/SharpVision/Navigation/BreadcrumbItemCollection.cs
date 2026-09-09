// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using CollectionAccess = JetBrains.Annotations.CollectionAccessAttribute;
using CollectionAccessType = JetBrains.Annotations.CollectionAccessType;

/// <summary>Exposes one breadcrumb's constrained retained path collection.</summary>
[PublicAPI]
public sealed class BreadcrumbItemCollection: ItemCollection<BreadcrumbItem>
{
    private readonly Breadcrumb _owner;

    /// <summary>Initializes a collection facade for an exact owner.</summary>
    /// <param name="owner">The non-null breadcrumb owner.</param>
    internal BreadcrumbItemCollection(Breadcrumb owner)
        : base(owner) =>
        _owner = owner;

    /// <summary>Gets or replaces one retained item while preserving its position.</summary>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the path.</exception>
    /// <exception cref="ArgumentException">The assigned item cannot be owned by this breadcrumb.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher or during an ownership transaction.</exception>
    /// <exception cref="ObjectDisposedException">The owner or assigned item is disposed.</exception>
    [CollectionAccess(CollectionAccessType.Read | CollectionAccessType.ModifyExistingContent)]
    public override BreadcrumbItem this[int index]
    {
        get => base[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _owner.ReplaceItem(index, value);
        }
    }

    /// <summary>Inserts one detached item at a path position.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    /// <exception cref="ArgumentException">The item cannot be owned by this breadcrumb.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher or during an ownership transaction.</exception>
    /// <exception cref="ObjectDisposedException">The owner or item is disposed.</exception>
    [CollectionAccess(CollectionAccessType.UpdatedContent)]
    public override void Insert(int index, BreadcrumbItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.InsertItem(index, item);
    }

    /// <summary>Removes an identical owned item without disposing it.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    [CollectionAccess(CollectionAccessType.ModifyExistingContent)]
    public override bool Remove(BreadcrumbItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _owner.RemoveItem(item);
    }

    /// <summary>Removes the item at a path position without disposing it.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the path.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    [CollectionAccess(CollectionAccessType.ModifyExistingContent)]
    public override void RemoveAt(int index) => _owner.RemoveItemAt(index);

    /// <summary>Moves an owned item while preserving identity and semantic current state.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An index is outside the path.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    [CollectionAccess(CollectionAccessType.ModifyExistingContent)]
    public override void Move(int oldIndex, int newIndex) => _owner.MoveItem(oldIndex, newIndex);

    /// <summary>Detaches every retained item without disposing it.</summary>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    [CollectionAccess(CollectionAccessType.ModifyExistingContent)]
    public override void Clear() => _owner.ClearItems();
}
