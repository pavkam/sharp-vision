// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Traverses document nodes for retained embedded controls and validates unique ownership.</summary>
internal static class DocumentEmbeddedControlCollector
{
    /// <summary>Collects controls from a root block sequence in document order.</summary>
    /// <param name="blocks">The root blocks.</param>
    /// <param name="controls">The destination list.</param>
    internal static void Collect(DocumentBlockCollection blocks, List<ControlBase> controls)
    {
        foreach (var block in blocks)
        {
            Collect(block, controls);
        }
    }

    /// <summary>Validates a candidate subtree before its node ownership changes.</summary>
    /// <param name="candidate">The detached candidate.</param>
    /// <param name="ownerNode">The destination node, or null for a document root.</param>
    /// <param name="ownerDocument">The destination document, or null for a detached tree.</param>
    /// <exception cref="ArgumentException">A control is duplicated or belongs to another retained tree.</exception>
    internal static void ValidateInsertion(
        DocumentNode candidate,
        DocumentNode? ownerNode,
        Document? ownerDocument)
    {
        var existing = new HashSet<ControlBase>(ReferenceEqualityComparer.Instance);

        if (ownerDocument is not null)
        {
            var controls = new List<ControlBase>();
            Collect(ownerDocument.Blocks, controls);

            foreach (var control in controls)
            {
                _ = existing.Add(control);
            }
        }
        else if (ownerNode is not null)
        {
            var root = ownerNode;

            while (root.ParentNode is { } parent)
            {
                root = parent;
            }

            var controls = new List<ControlBase>();
            Collect(root, controls);

            foreach (var control in controls)
            {
                _ = existing.Add(control);
            }
        }

        var candidateControls = new List<ControlBase>();
        Collect(candidate, candidateControls);

        foreach (var control in candidateControls)
        {
            ObjectDisposedException.ThrowIf(control.IsDisposed, control);

            if (!existing.Add(control) || control.Parent is not null || control.Dispatcher is not null)
            {
                throw new ArgumentException(
                    "An embedded control must be detached and may appear only once in a document tree.",
                    nameof(candidate));
            }
        }
    }

    /// <summary>Collects controls from one candidate subtree in document order.</summary>
    /// <param name="node">The candidate root.</param>
    /// <param name="controls">The destination list.</param>
    internal static void Collect(DocumentNode node, List<ControlBase> controls)
    {
        switch (node)
        {
            case DocumentBlockControl blockControl:
                controls.Add(blockControl.Control);
                break;
            case DocumentParagraph paragraph:
                Collect(paragraph.Inlines, controls);
                break;
            case DocumentHeading heading:
                Collect(heading.Inlines, controls);
                break;
            case DocumentList list:
                foreach (var item in list.Items)
                {
                    Collect(item, controls);
                }

                break;
            case DocumentListItem item:
                foreach (var block in item.Blocks)
                {
                    Collect(block, controls);
                }

                break;
            case DocumentBlockQuote quote:
                foreach (var block in quote.Blocks)
                {
                    Collect(block, controls);
                }

                break;
            case DocumentCallout callout:
                foreach (var block in callout.Blocks)
                {
                    Collect(block, controls);
                }

                break;
            case DocumentTable table:
                foreach (var row in table.Rows)
                {
                    Collect(row, controls);
                }

                break;
            case DocumentTableRow row:
                foreach (var cell in row.Cells)
                {
                    Collect(cell, controls);
                }

                break;
            case DocumentTableCell cell:
                Collect(cell.Inlines, controls);
                break;
            case DocumentInlineControl inlineControl:
                controls.Add(inlineControl.Control);
                break;
            case DocumentInlineContainer container:
                Collect(container.Inlines, controls);
                break;
            case DocumentCodeBlock or DocumentSeparator or DocumentTextRun or DocumentCodeSpan or
                DocumentSoftBreak or DocumentLineBreak:
                break;
            default:
                throw new UnreachableException(
                    "DocumentNode's hierarchy is closed to this assembly, so every node kind is handled.");
        }
    }

    private static void Collect(DocumentInlineCollection inlines, List<ControlBase> controls)
    {
        foreach (var inline in inlines)
        {
            Collect(inline, controls);
        }
    }
}
