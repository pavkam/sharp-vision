// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Exposes a typed tree view item collection backed by the owning tree view or parent item.</summary>
[PublicAPI]
public sealed class TreeViewItemCollection: IReadOnlyList<TreeViewItem>
{
    private readonly List<TreeViewItem> _items = [];
    // Membership testing alongside the ordered list. Add() must reject a duplicate on every
    // call, and a List<T>.Contains scan made populating an n-item collection O(n^2).
    private readonly HashSet<TreeViewItem> _itemSet = new(ReferenceEqualityComparer.Instance);
#pragma warning disable IDE0032 // Cross-instance propagation assigns this field directly.
    private TreeView? _owner;
#pragma warning restore IDE0032

    /// <summary>Gets or sets the owner that receives structural change notifications.</summary>
    internal TreeView? Owner
    {
        get => _owner;
        set
        {
            if (ReferenceEquals(_owner, value))
            {
                return;
            }

            // Iterative: a caller may attach a prebuilt detached chain of arbitrary depth to a
            // root, and ownership has to reach every descendant. Recursion here turned a valid
            // deep tree into an unrecoverable StackOverflowException on a single Add. A subtree
            // that already carries the new owner cannot contain one that does not, so pruning
            // there keeps repeated attach and detach cycles linear.
            List<TreeViewItemCollection> pending = [this];

            while (pending.Count > 0)
            {
                var current = pending[^1];
                pending.RemoveAt(pending.Count - 1);

                if (ReferenceEquals(current._owner, value))
                {
                    continue;
                }

                current._owner = value;

                foreach (var item in current._items)
                {
                    pending.Add(item.Children);
                }
            }
        }
    }

    /// <summary>Gets the parent item when this is a child collection.</summary>
    internal TreeViewItem? ParentItem { get; }

    internal TreeViewItemCollection(TreeViewItem? parentItem = null) => ParentItem = parentItem;

    /// <summary>
    /// Gets or sets whether this collection's content is governed by the owning item's async child
    /// loader. While true, every caller mutation path throws; only the loader's own commit engine,
    /// through <see cref="LoaderReplace"/>, may change the content.
    /// </summary>
    internal bool IsLoaderOwned { get; set; }

    private void VerifyNotLoaderOwned()
    {
        if (IsLoaderOwned)
        {
            throw new InvalidOperationException(
                "This collection is governed by an async child source. Mutate it through the " +
                "owning item's ChildSource and ReloadChildrenAsync instead of directly.");
        }
    }

    /// <summary>Gets or replaces one owned tree view item, preserving its position.</summary>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current items.</exception>
    /// <exception cref="ArgumentException">The assigned item already belongs to this collection.</exception>
    /// <exception cref="InvalidOperationException">
    /// The assigned item already belongs to another collection, would create a cycle, or an
    /// attached owner is mutated off its dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">An attached owner is disposed.</exception>
    public TreeViewItem this[int index]
    {
        get => _items[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Owner?.VerifyTreeMutable();
            VerifyNotLoaderOwned();

            if ((uint) index >= (uint) _items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    "The replacement index is outside the collection.");
            }

            var old = _items[index];

            if (ReferenceEquals(old, value))
            {
                return;
            }

            ValidateCandidate(value);

            old.ParentCollection = null;
            old.Children.Owner = null;
            old.CancelPendingChildLoadSubtree();

            _items[index] = value;
            _ = _itemSet.Remove(old);
            _ = _itemSet.Add(value);
            value.ParentCollection = this;
            value.Children.Owner = Owner;
            Owner?.NotifyStructureChanged();
        }
    }

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <summary>Adds one detached tree view item.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">The item already belongs to this collection.</exception>
    /// <exception cref="InvalidOperationException">
    /// The item already belongs to another collection, would create a cycle, or an attached owner
    /// is mutated off its dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">An attached owner is disposed.</exception>
    public void Add(TreeViewItem item) => Insert(_items.Count, item);

    /// <summary>Inserts one detached tree view item at a position.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    /// <exception cref="ArgumentException">The item already belongs to this collection.</exception>
    /// <exception cref="InvalidOperationException">
    /// The item already belongs to another collection, would create a cycle, or an attached owner
    /// is mutated off its dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">An attached owner is disposed.</exception>
    public void Insert(int index, TreeViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Owner?.VerifyTreeMutable();
        VerifyNotLoaderOwned();

        if ((uint) index > (uint) _items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index,
                "The insertion index is outside the collection.");
        }

        ValidateCandidate(item);

        _items.Insert(index, item);
        _ = _itemSet.Add(item);
        item.ParentCollection = this;
        item.Children.Owner = Owner;
        Owner?.NotifyStructureChanged();
        ParentItem?.OnChildCollectionStructureChanged();
    }

    // Shared by Add/Insert/the indexer setter: the item is always detached at this
    // point, so it can only be an ancestor of this insertion point by containing it,
    // which the descendant walk below already detects from the other end. Only
    // self-insertion needs its own test. An upward walk from the candidate costs
    // O(depth) on every mutation, which made building a chain-shaped tree quadratic
    // before any node was even owned.
    private void ValidateCandidate(TreeViewItem item)
    {
        if (_itemSet.Contains(item))
        {
            throw new ArgumentException("The item is already in this collection.", nameof(item));
        }

        if (item.ParentCollection is not null)
        {
            throw new InvalidOperationException("The item already belongs to a tree item collection.");
        }

        if (ReferenceEquals(ParentItem, item))
        {
            throw new InvalidOperationException("A tree item cannot contain itself or one of its ancestors.");
        }

        if (ParentItem is not null && ContainsDescendant(item, ParentItem))
        {
            throw new InvalidOperationException("A tree item cannot contain one of its descendants.");
        }
    }

    private static bool ContainsDescendant(TreeViewItem root, TreeViewItem candidate)
    {
        // Iterative: cycle detection runs before every add, over a caller-controlled hierarchy.
        List<TreeViewItem> pending = [root];

        while (pending.Count > 0)
        {
            var current = pending[^1];
            pending.RemoveAt(pending.Count - 1);

            foreach (var child in current.Children)
            {
                if (ReferenceEquals(child, candidate))
                {
                    return true;
                }

                pending.Add(child);
            }
        }

        return false;
    }

    /// <summary>Removes one owned tree view item.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">An attached owner is mutated off its dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">An attached owner is disposed.</exception>
    public bool Remove(TreeViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Owner?.VerifyTreeMutable();
        VerifyNotLoaderOwned();

        if (!_items.Remove(item))
        {
            return false;
        }

        _ = _itemSet.Remove(item);
        item.ParentCollection = null;
        item.Children.Owner = null;
        item.CancelPendingChildLoadSubtree();
        Owner?.NotifyStructureChanged();
        ParentItem?.OnChildCollectionStructureChanged();
        return true;
    }

    /// <summary>Removes the owned tree view item at a position.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current items.</exception>
    /// <exception cref="InvalidOperationException">An attached owner is mutated off its dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">An attached owner is disposed.</exception>
    public void RemoveAt(int index)
    {
        Owner?.VerifyTreeMutable();
        VerifyNotLoaderOwned();

        if ((uint) index >= (uint) _items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index,
                "The removal index is outside the collection.");
        }

        var item = _items[index];
        _items.RemoveAt(index);
        _ = _itemSet.Remove(item);
        item.ParentCollection = null;
        item.Children.Owner = null;
        item.CancelPendingChildLoadSubtree();
        Owner?.NotifyStructureChanged();
        ParentItem?.OnChildCollectionStructureChanged();
    }

    /// <summary>Moves one owned tree view item to a different position, preserving its identity.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="oldIndex"/> or <paramref name="newIndex"/> is outside the current items.
    /// </exception>
    /// <exception cref="InvalidOperationException">An attached owner is mutated off its dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">An attached owner is disposed.</exception>
    public void Move(int oldIndex, int newIndex)
    {
        Owner?.VerifyTreeMutable();
        VerifyNotLoaderOwned();

        if ((uint) oldIndex >= (uint) _items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(oldIndex), oldIndex,
                "The source index is outside the collection.");
        }

        if ((uint) newIndex >= (uint) _items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(newIndex), newIndex,
                "The destination index is outside the collection.");
        }

        if (oldIndex == newIndex)
        {
            return;
        }

        var item = _items[oldIndex];
        _items.RemoveAt(oldIndex);
        _items.Insert(newIndex, item);
        Owner?.NotifyStructureChanged();
    }

    /// <summary>Gets the position of one item, or -1 when it is not owned by this collection.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public int IndexOf(TreeViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _items.IndexOf(item);
    }

    /// <summary>Removes every owned tree view item.</summary>
    /// <exception cref="InvalidOperationException">An attached owner is mutated off its dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">An attached owner is disposed.</exception>
    public void Clear()
    {
        Owner?.VerifyTreeMutable();
        VerifyNotLoaderOwned();

        if (_items.Count == 0)
        {
            return;
        }

        foreach (var item in _items)
        {
            item.ParentCollection = null;
            item.Children.Owner = null;
            item.CancelPendingChildLoadSubtree();
        }

        _items.Clear();
        _itemSet.Clear();
        Owner?.NotifyStructureChanged();
        ParentItem?.OnChildCollectionStructureChanged();
    }

    /// <summary>
    /// Replaces this collection's content with an ordered snapshot on behalf of the owning item's
    /// async child-loading commit engine, bypassing the <see cref="IsLoaderOwned"/> caller-mutation
    /// guard. Every supplied item - reused or freshly materialized - is (re)attached in the given
    /// order; any previously owned item absent from <paramref name="items"/> is detached, not
    /// disposed, because eviction disposal is the commit engine's own responsibility.
    /// </summary>
    /// <param name="items">The non-null final ordered content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    /// <exception cref="InvalidOperationException">An attached owner is mutated off its dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">An attached owner is disposed.</exception>
    internal void LoaderReplace(IReadOnlyList<TreeViewItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Owner?.VerifyTreeMutable();

        foreach (var item in _items)
        {
            item.ParentCollection = null;
            item.Children.Owner = null;
        }

        _items.Clear();
        _itemSet.Clear();

        foreach (var item in items)
        {
            _items.Add(item);
            _ = _itemSet.Add(item);
            item.ParentCollection = this;
            item.Children.Owner = Owner;
        }

        Owner?.NotifyStructureChanged();
    }

    /// <inheritdoc/>
    public IEnumerator<TreeViewItem> GetEnumerator()
    {
        for (var index = 0; index < _items.Count; index++)
        {
            yield return _items[index];
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
