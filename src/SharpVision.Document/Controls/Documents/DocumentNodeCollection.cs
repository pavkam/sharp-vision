// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Implements the shared ordered, single-owner node collection behind
/// <see cref="DocumentBlockCollection"/>, <see cref="DocumentInlineCollection"/>, and
/// <see cref="DocumentListItemCollection"/>.</summary>
/// <typeparam name="TNode">The node kind this collection accepts.</typeparam>
/// <remarks>
/// <para>
/// The collection owns its entries. Adding a node records this collection's owner on it; removing a
/// node clears that owner and hands the caller back a detached node it may reuse elsewhere. A node
/// that already belongs to a tree is rejected rather than silently reparented, which is what keeps a
/// document tree a tree.
/// </para>
/// <para>
/// Every successful mutation invalidates the owning document's layout exactly once. A collection
/// belonging to a detached subtree invalidates nothing, so composing a whole document before adding
/// it costs a single layout pass rather than one per node.
/// </para>
/// </remarks>
[PublicAPI]
public abstract class DocumentNodeCollection<TNode>: IReadOnlyList<TNode>
    where TNode : DocumentNode
{
    private readonly List<TNode> _items = [];
    private readonly Document? _ownerDocument;
    private readonly DocumentNode? _ownerNode;

    /// <summary>Initializes a collection owned by one node.</summary>
    /// <param name="ownerNode">The owning node.</param>
    private protected DocumentNodeCollection(DocumentNode ownerNode)
    {
        Debug.Assert(ownerNode is not null, "A node collection requires its owning node.");
        _ownerNode = ownerNode;
    }

    /// <summary>Initializes a collection owned by one document.</summary>
    /// <param name="ownerDocument">The owning document.</param>
    private protected DocumentNodeCollection(Document ownerDocument)
    {
        Debug.Assert(ownerDocument is not null, "A node collection requires its owning document.");
        _ownerDocument = ownerDocument;
    }

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <summary>Gets the node at a position.</summary>
    /// <param name="index">The valid zero-based position.</param>
    /// <returns>The node at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current entries.</exception>
    public TNode this[int index] => _items[index];

    /// <summary>Adds one detached node at the end of the sequence.</summary>
    /// <param name="node">The non-null detached node.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> already belongs to a document tree,
    /// creates a cycle, nests a link, duplicates a retained control, or would exceed the supported tree depth.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The attached owner is disposed.</exception>
    public void Add(TNode node) => Insert(_items.Count, node);

    /// <summary>Inserts one detached node at a position.</summary>
    /// <param name="index">The insertion position from zero through <see cref="Count"/>.</param>
    /// <param name="node">The non-null detached node.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> already belongs to a document tree,
    /// creates a cycle, nests a link, duplicates a retained control, or would exceed the supported tree depth.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The attached owner is disposed.</exception>
    public void Insert(int index, TNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _items.Count);
        VerifyMutable();

        if (node.IsAttached)
        {
            throw new ArgumentException(
                "The node already belongs to a document tree. Remove it from its current owner first.",
                nameof(node));
        }

        if (_ownerNode is not null && WouldCreateCycle(node, _ownerNode))
        {
            throw new ArgumentException(
                "The node cannot be inserted below itself or one of its descendants.",
                nameof(node));
        }

        if (HasLinkAncestor(_ownerNode) && DocumentTreeDepthValidator.ContainsLink(node))
        {
            throw new ArgumentException(
                "A link cannot contain another link.",
                nameof(node));
        }

        DocumentTreeDepthValidator.ValidateInsertion(node, _ownerNode);

        DocumentEmbeddedControlCollector.ValidateInsertion(
            node,
            _ownerNode,
            _ownerDocument ?? _ownerNode?.OwnerDocument);

        // Ownership is recorded before the sequence changes so a rejected insertion leaves both the
        // node and this collection exactly as the caller found them.
        node.Attach(_ownerNode, _ownerDocument);
        _items.Insert(index, node);
        Invalidate();
    }

    /// <summary>Removes the first occurrence of one node by reference, leaving it detached.</summary>
    /// <param name="node">The non-null candidate.</param>
    /// <returns>True when the node was found and removed; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The attached owner is disposed.</exception>
    public bool Remove(TNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        VerifyMutable();
        var index = _items.IndexOf(node);

        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        return true;
    }

    /// <summary>Removes the node at a position, leaving it detached.</summary>
    /// <param name="index">The valid zero-based position.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the current entries.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The attached owner is disposed.</exception>
    public void RemoveAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _items.Count);
        VerifyMutable();

        _items[index].Detach();
        _items.RemoveAt(index);
        Invalidate();
    }

    /// <summary>Removes every node, leaving each one detached.</summary>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The attached owner is disposed.</exception>
    public void Clear()
    {
        VerifyMutable();

        if (_items.Count == 0)
        {
            return;
        }

        foreach (var item in _items)
        {
            item.Detach();
        }

        _items.Clear();
        Invalidate();
    }

    /// <summary>Gets the allocation-free value enumerator used by direct iteration.</summary>
    /// <returns>The value enumerator.</returns>
    public List<TNode>.Enumerator GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator<TNode> IEnumerable<TNode>.GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    private void Invalidate() => (_ownerDocument ?? _ownerNode?.OwnerDocument)?.InvalidateContent();

    private void VerifyMutable() => (_ownerDocument ?? _ownerNode?.OwnerDocument)?.VerifyContentMutable();

    private static bool WouldCreateCycle(DocumentNode candidate, DocumentNode owner)
    {
        for (var ancestor = owner; ancestor is not null; ancestor = ancestor.ParentNode)
        {
            if (ReferenceEquals(ancestor, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLinkAncestor(DocumentNode? owner)
    {
        for (var ancestor = owner; ancestor is not null; ancestor = ancestor.ParentNode)
        {
            if (ancestor is DocumentLink)
            {
                return true;
            }
        }

        return false;
    }
}
