// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Exposes a typed tree view item collection backed by the owning tree view or parent item.</summary>
[PublicAPI]
public sealed class TreeViewItemCollection: IReadOnlyList<TreeViewItem>
{
    private readonly List<TreeViewItem> _items = [];

    /// <summary>Gets or sets the owner that receives structural change notifications.</summary>
    internal TreeView? Owner
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;

            foreach (var item in _items)
            {
                item.Children.Owner = value;
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
    public void Add(TreeViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_items.Contains(item))
        {
            throw new ArgumentException("The item is already in this collection.", nameof(item));
        }

        if (item.ParentCollection is not null)
        {
            throw new InvalidOperationException("The item already belongs to a tree item collection.");
        }

        for (var ancestor = ParentItem; ancestor is not null; ancestor = ancestor.ParentCollection?.ParentItem)
        {
            if (ReferenceEquals(ancestor, item))
            {
                throw new InvalidOperationException("A tree item cannot contain itself or one of its ancestors.");
            }
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
        foreach (var child in root.Children)
        {
            if (ReferenceEquals(child, candidate) || ContainsDescendant(child, candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Removes one owned tree view item.</summary>
    public bool Remove(TreeViewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!_items.Remove(item))
        {
            return false;
        }

        Owner?.NotifyStructureChanged();
        item.ParentCollection = null;
        item.Children.Owner = null;
        return true;
    }

    /// <summary>Removes every owned tree view item.</summary>
    public void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }

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
