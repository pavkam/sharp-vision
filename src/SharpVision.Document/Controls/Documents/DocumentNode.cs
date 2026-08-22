// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Defines one node of a <see cref="Document"/>'s content tree.</summary>
/// <remarks>
/// <para>
/// A node is <em>pure data</em>: it carries content and structure, never layout surface, focus, or
/// rendering behavior of its own. Exactly one control - the owning <see cref="Document"/> - measures,
/// paints, hit-tests, and focuses the entire tree. A node therefore has no size, no bounds, no
/// margin, no padding, and no style; the document resolves all presentation from
/// <see cref="Document.ActualStyle"/> at paint time, so a live theme swap can never leave a node
/// stale.
/// </para>
/// <para>
/// The hierarchy is closed to this assembly. The <see langword="private protected"/> constructor
/// means the only nodes that can ever exist are the sealed types declared here, split between
/// <see cref="DocumentBlock"/> (vertically stacked content) and <see cref="DocumentInline"/>
/// (content flowing within a line). Every consumer can therefore exhaustively pattern-match a node
/// without an unreachable default case, and the document's layout engine is total over the tree.
/// </para>
/// <para>
/// A node has at most one owner. Adding an already-owned node to a second collection throws rather
/// than silently reparenting, so a tree can never become a graph and a mutation can never invalidate
/// two documents at once.
/// </para>
/// </remarks>
[PublicAPI]
public abstract class DocumentNode
{
    private protected DocumentNode()
    {
    }

    /// <summary>Gets the owning parent node, or null when this node is unowned or sits directly in a
    /// <see cref="Document"/>'s own block collection.</summary>
    internal DocumentNode? ParentNode { get; private set; }

    /// <summary>Gets the owning document when this node sits directly in <see cref="Document.Blocks"/>,
    /// or null when it is nested under <see cref="ParentNode"/> or unowned.</summary>
    /// <remarks>
    /// Only a top-level block records the document directly. A nested node reaches it by walking
    /// <see cref="ParentNode"/> to the root, which keeps attachment O(1) and removes the whole class of
    /// cascade bugs a pushed-down document reference would introduce on every subtree move.
    /// </remarks>
    internal Document? RootDocument { get; private set; }

    /// <summary>Gets the document this node currently belongs to, or null when it is detached.</summary>
    internal Document? OwnerDocument
    {
        get
        {
            var node = this;

            while (node.ParentNode is { } parent)
            {
                node = parent;
            }

            return node.RootDocument;
        }
    }

    /// <summary>Gets whether this node already belongs to a tree.</summary>
    internal bool IsAttached => ParentNode is not null || RootDocument is not null;

    /// <summary>Records this node's new owner after a collection has validated the insertion.</summary>
    /// <param name="parentNode">The owning node, or null when <paramref name="rootDocument"/> owns it.</param>
    /// <param name="rootDocument">The owning document, or null when <paramref name="parentNode"/> owns it.</param>
    internal void Attach(DocumentNode? parentNode, Document? rootDocument)
    {
        Debug.Assert(!IsAttached, "A node is validated as detached before it is attached.");
        Debug.Assert(
            parentNode is null ^ rootDocument is null,
            "A node is owned by exactly one of a parent node or a root document.");

        ParentNode = parentNode;
        RootDocument = rootDocument;
    }

    /// <summary>Clears this node's owner after a collection has removed it.</summary>
    internal void Detach()
    {
        ParentNode = null;
        RootDocument = null;
    }

    /// <summary>Verifies that the owning document, when any, may be mutated on this thread.</summary>
    internal void VerifyMutable() => OwnerDocument?.VerifyContentMutable();

    /// <summary>Requests that the owning document re-lay-out and repaint its content.</summary>
    /// <remarks>
    /// A detached node has nothing to invalidate, so building a tree before adding it to a document
    /// costs no layout passes at all.
    /// </remarks>
    private protected void InvalidateContent() => OwnerDocument?.InvalidateContent();
}
