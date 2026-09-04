// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Bounds semantic-tree depth before recursive layout receives a subtree.</summary>
internal static class DocumentTreeDepthValidator
{
    /// <summary>Gets the maximum number of semantic nodes on any root-to-leaf path.</summary>
    internal const int MaximumDepth = 256;

    /// <summary>Rejects an insertion whose resulting path would exceed <see cref="MaximumDepth"/>.</summary>
    /// <param name="candidate">The detached subtree root.</param>
    /// <param name="owner">The destination owner node, or null for a document root.</param>
    /// <exception cref="ArgumentException">The resulting tree would exceed the supported depth.</exception>
    internal static void ValidateInsertion(DocumentNode candidate, DocumentNode? owner)
    {
        var ancestorDepth = 0;

        for (var ancestor = owner; ancestor is not null; ancestor = ancestor.ParentNode)
        {
            ancestorDepth++;
        }

        var stack = new Stack<(DocumentNode Node, int Depth)>();
        stack.Push((candidate, ancestorDepth + 1));

        while (stack.TryPop(out var entry))
        {
            if (entry.Depth > MaximumDepth)
            {
                throw new ArgumentException(
                    $"A document tree cannot exceed {MaximumDepth} semantic levels.",
                    nameof(candidate));
            }

            PushChildren(entry.Node, entry.Depth + 1, stack);
        }
    }

    /// <summary>Gets whether a subtree contains a semantic link.</summary>
    /// <param name="candidate">The subtree root.</param>
    /// <returns>True when the root or a descendant is a <see cref="DocumentLink"/>.</returns>
    internal static bool ContainsLink(DocumentNode candidate)
    {
        var stack = new Stack<(DocumentNode Node, int Depth)>();
        stack.Push((candidate, 0));

        while (stack.TryPop(out var entry))
        {
            if (entry.Node is DocumentLink)
            {
                return true;
            }

            PushChildren(entry.Node, depth: 0, stack);
        }

        return false;
    }

    private static void PushChildren(
        DocumentNode node,
        int depth,
        Stack<(DocumentNode Node, int Depth)> stack)
    {
        switch (node)
        {
            case DocumentParagraph paragraph:
                Push(paragraph.Inlines, depth, stack);
                break;
            case DocumentHeading heading:
                Push(heading.Inlines, depth, stack);
                break;
            case DocumentList list:
                Push(list.Items, depth, stack);
                break;
            case DocumentListItem item:
                Push(item.Blocks, depth, stack);
                break;
            case DocumentBlockQuote quote:
                Push(quote.Blocks, depth, stack);
                break;
            case DocumentCallout callout:
                Push(callout.Blocks, depth, stack);
                break;
            case DocumentTable table:
                Push(table.Rows, depth, stack);
                break;
            case DocumentTableRow row:
                Push(row.Cells, depth, stack);
                break;
            case DocumentTableCell cell:
                Push(cell.Inlines, depth, stack);
                break;
            case DocumentInlineContainer container:
                Push(container.Inlines, depth, stack);
                break;
            case DocumentCodeBlock:
            case DocumentSeparator:
            case DocumentBlockControl:
            case DocumentTextRun:
            case DocumentCodeSpan:
            case DocumentSoftBreak:
            case DocumentLineBreak:
            case DocumentInlineControl:
                break;
            default:
                throw new UnreachableException(
                    "DocumentNode's hierarchy is closed to this assembly, so every node kind is handled.");
        }
    }

    private static void Push<TNode>(
        DocumentNodeCollection<TNode> nodes,
        int depth,
        Stack<(DocumentNode Node, int Depth)> stack)
        where TNode : DocumentNode
    {
        for (var index = nodes.Count - 1; index >= 0; index--)
        {
            stack.Push((nodes[index], depth));
        }
    }
}
