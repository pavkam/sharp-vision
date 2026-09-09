// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Provides the shared base for a typed semantic collection an <see cref="ItemsControl"/>
/// owner publishes over its own realized item controls.</summary>
/// <typeparam name="TItem">The concrete control type this collection exposes.</typeparam>
/// <remarks>
/// <para>
/// A derived owner constructs and publishes one instance of a sealed collection type deriving from
/// this class, typically as a single get-only property assigned immediately after
/// <see cref="ItemsControl.InitializeItemsHost"/> in its own constructor. The default
/// <see cref="this[int]"/>, <see cref="Count"/>, <see cref="Add"/>, <see cref="Insert"/>,
/// <see cref="Remove(TItem)"/>, <see cref="RemoveAt"/>, <see cref="Move"/>, <see cref="IndexOf"/>,
/// <see cref="Contains"/>, and <see cref="Clear"/> implementations read and mutate realized item
/// controls directly through the owner's <c>protected internal</c> item-control accessors -
/// <see cref="ItemsControl.GetItemControl"/> and its siblings. Those accessors are
/// <c>protected internal</c> rather than plain <c>protected</c> specifically so this class, a
/// sibling of <see cref="ItemsControl"/> rather than a subclass of it, can reach them on the owner
/// instance it wraps.
/// </para>
/// <para>
/// An owner whose collection needs more than that raw structural mutation - reindexing a current or
/// selected position, notifying a dependent property, or maintaining a private subscription - keeps
/// that logic exactly where it already lives, on the owner itself, in the owner's own internal
/// method. Its derived collection then overrides the corresponding virtual member here to call that
/// existing owner method instead of running the default accessor-only path; it does not move or
/// duplicate the owner's logic into the collection. The seven protected <c>On*</c> hooks
/// (<see cref="OnInserting"/>, <see cref="OnInserted"/>, <see cref="OnRemoving"/>,
/// <see cref="OnRemoved"/>, <see cref="OnMoved"/>, <see cref="OnReplaced"/>, <see cref="OnCleared"/>)
/// exist for the simpler case: a derived collection that only needs to react before or after a
/// default-path mutation commits, without replacing the mutation itself.
/// </para>
/// </remarks>
[PublicAPI]
public abstract class ItemCollection<TItem>: IReadOnlyList<TItem>
    where TItem : ControlBase
{
    private readonly ItemsControl _owner;

    /// <summary>Initializes a typed collection view over one non-null owner.</summary>
    /// <param name="owner">The owning item control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    protected ItemCollection(ItemsControl owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Gets or replaces one owned item, preserving its position.</summary>
    /// <param name="index">The valid zero-based item position.</param>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current items.</exception>
    /// <exception cref="ArgumentException">The assigned item cannot be owned by this collection's owner.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner or the assigned item is disposed.</exception>
    public virtual TItem this[int index]
    {
        get => (TItem) _owner.GetItemControl(index);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _owner.VerifyMutable();
            var previous = (TItem) _owner.GetItemControl(index);
            _owner.ReplaceItemControl(index, value);
            OnReplaced(index, previous, value);
        }
    }

    /// <summary>Gets the number of currently owned items.</summary>
    public virtual int Count => _owner.ItemControlCount;

    /// <summary>Adds one detached item at the end of the collection.</summary>
    /// <param name="item">The non-null detached item.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item cannot be owned by this collection's owner.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner or item is disposed.</exception>
    public virtual void Add(TItem item) => Insert(Count, item);

    /// <summary>Inserts one detached item at a validated position.</summary>
    /// <param name="index">The insertion position from zero through <see cref="Count"/>.</param>
    /// <param name="item">The non-null detached item.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    /// <exception cref="ArgumentException">The item cannot be owned by this collection's owner.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner or item is disposed.</exception>
    public virtual void Insert(int index, TItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.VerifyMutable();
        OnInserting(index, item);
        _owner.InsertItemControl(index, item);
        OnInserted(index, item);
    }

    /// <summary>Removes one identical owned item without disposing it.</summary>
    /// <param name="item">The non-null candidate.</param>
    /// <returns>True when the item was removed; false when it was not owned by this collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public virtual bool Remove(TItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.VerifyMutable();
        var index = _owner.IndexOfItemControl(item);

        if (index < 0)
        {
            return false;
        }

        OnRemoving(index, item);
        _ = _owner.RemoveItemControl(item);
        OnRemoved(index, item);
        return true;
    }

    /// <summary>Removes the owned item at a position without disposing it.</summary>
    /// <param name="index">The valid zero-based item position.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current items.</exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public virtual void RemoveAt(int index)
    {
        _owner.VerifyMutable();
        var item = (TItem) _owner.GetItemControl(index);
        OnRemoving(index, item);
        _owner.RemoveItemControlAt(index);
        OnRemoved(index, item);
    }

    /// <summary>Moves one owned item to a different position, preserving its identity.</summary>
    /// <param name="oldIndex">The current zero-based item position.</param>
    /// <param name="newIndex">The destination zero-based item position.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="oldIndex"/> or <paramref name="newIndex"/> is outside the current items.
    /// </exception>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public virtual void Move(int oldIndex, int newIndex)
    {
        _owner.VerifyMutable();
        _owner.MoveItemControl(oldIndex, newIndex);
        OnMoved(oldIndex, newIndex);
    }

    /// <summary>Gets the position of one item, or -1 when it is not owned by this collection.</summary>
    /// <param name="item">The non-null candidate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public virtual int IndexOf(TItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _owner.IndexOfItemControl(item);
    }

    /// <summary>Gets whether one item is currently owned by this collection.</summary>
    /// <param name="item">The non-null candidate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public virtual bool Contains(TItem item) => IndexOf(item) >= 0;

    /// <summary>Removes every owned item without disposing the detached instances.</summary>
    /// <exception cref="InvalidOperationException">The owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public virtual void Clear()
    {
        _owner.VerifyMutable();
        _owner.ClearItemControls();
        OnCleared();
    }

    /// <inheritdoc/>
    public IEnumerator<TItem> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Runs immediately before the default <see cref="Insert"/> path commits one insertion.</summary>
    /// <param name="index">The validated insertion position.</param>
    /// <param name="item">The non-null item about to be inserted.</param>
    /// <remarks>The default implementation does nothing.</remarks>
    protected virtual void OnInserting(int index, TItem item)
    {
    }

    /// <summary>Runs immediately after the default <see cref="Insert"/> path commits one insertion.</summary>
    /// <param name="index">The committed insertion position.</param>
    /// <param name="item">The inserted item.</param>
    /// <remarks>The default implementation does nothing.</remarks>
    protected virtual void OnInserted(int index, TItem item)
    {
    }

    /// <summary>Runs immediately before the default <see cref="Remove(TItem)"/> or
    /// <see cref="RemoveAt"/> path commits one removal.</summary>
    /// <param name="index">The item's position before removal.</param>
    /// <param name="item">The item about to be removed.</param>
    /// <remarks>The default implementation does nothing.</remarks>
    protected virtual void OnRemoving(int index, TItem item)
    {
    }

    /// <summary>Runs immediately after the default <see cref="Remove(TItem)"/> or
    /// <see cref="RemoveAt"/> path commits one removal.</summary>
    /// <param name="index">The removed item's former position.</param>
    /// <param name="item">The removed item.</param>
    /// <remarks>The default implementation does nothing.</remarks>
    protected virtual void OnRemoved(int index, TItem item)
    {
    }

    /// <summary>Runs immediately after the default <see cref="Move"/> path commits one reorder.</summary>
    /// <param name="oldIndex">The item's former position.</param>
    /// <param name="newIndex">The item's committed position.</param>
    /// <remarks>The default implementation does nothing.</remarks>
    protected virtual void OnMoved(int oldIndex, int newIndex)
    {
    }

    /// <summary>Runs immediately after the default indexer setter commits one replacement.</summary>
    /// <param name="index">The replaced position.</param>
    /// <param name="previous">The outgoing item.</param>
    /// <param name="item">The incoming item.</param>
    /// <remarks>The default implementation does nothing.</remarks>
    protected virtual void OnReplaced(int index, TItem previous, TItem item)
    {
    }

    /// <summary>Runs immediately after the default <see cref="Clear"/> path commits.</summary>
    /// <remarks>The default implementation does nothing.</remarks>
    protected virtual void OnCleared()
    {
    }
}
