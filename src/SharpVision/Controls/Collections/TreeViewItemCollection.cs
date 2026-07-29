// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Exposes a typed tree view item collection backed by the owning tree view or parent item.</summary>
[PublicAPI]
public sealed class TreeViewItemCollection: IReadOnlyList<TreeViewItem>
{
    private readonly List<TreeViewItem> _items = [];
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

    /// <inheritdoc/>
    public TreeViewItem this[int index] => _items[index];

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
    public void Add(TreeViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Owner?.VerifyTreeMutable();

        if (_items.Contains(item))
        {
            throw new ArgumentException("The item is already in this collection.", nameof(item));
        }

        if (item.ParentCollection is not null)
        {
            throw new InvalidOperationException("The item already belongs to a tree item collection.");
        }

        // The item is detached, so it can only be an ancestor of this insertion point by
        // containing it, which the descendant walk below already detects from the other end. Only
        // self-insertion needs its own test. The previous upward walk cost O(depth) on every add,
        // which made building a chain-shaped tree quadratic before any node was even owned.
        if (ReferenceEquals(ParentItem, item))
        {
            throw new InvalidOperationException("A tree item cannot contain itself or one of its ancestors.");
        }

        if (ParentItem is not null && ContainsDescendant(item, ParentItem))
        {
            throw new InvalidOperationException("A tree item cannot contain one of its descendants.");
        }

        _items.Add(item);
        item.ParentCollection = this;
        item.Children.Owner = Owner;
        Owner?.NotifyStructureChanged();
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

        if (!_items.Remove(item))
        {
            return false;
        }

        item.ParentCollection = null;
        item.Children.Owner = null;
        Owner?.NotifyStructureChanged();
        return true;
    }

    /// <summary>Removes every owned tree view item.</summary>
    /// <exception cref="InvalidOperationException">An attached owner is mutated off its dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">An attached owner is disposed.</exception>
    public void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }

        Owner?.VerifyTreeMutable();

        foreach (var item in _items)
        {
            item.ParentCollection = null;
            item.Children.Owner = null;
        }

        _items.Clear();
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
