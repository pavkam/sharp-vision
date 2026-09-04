// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Documents;

using SharpVision.Controls.Document;

/// <summary>Contains one parsed tree and its diagnostics.</summary>
/// <remarks>
/// A reader creates the result with detached roots. <see cref="Document.Load"/>
/// consumes those exact roots by attaching them to its document, so a result is single-use for
/// loading even though its diagnostics and applied roots remain inspectable afterward.
/// </remarks>
[PublicAPI]
public sealed class DocumentReadResult
{
    /// <summary>Initializes a result from detached unique blocks.</summary>
    /// <param name="blocks">The non-null detached block sequence.</param>
    /// <param name="diagnostics">The non-null diagnostic sequence.</param>
    /// <exception cref="ArgumentNullException">An argument or entry is null.</exception>
    /// <exception cref="ArgumentException">A block is attached or duplicated.</exception>
    /// <exception cref="ObjectDisposedException">An embedded control is disposed.</exception>
    public DocumentReadResult(
        IEnumerable<DocumentBlock> blocks,
        IEnumerable<DocumentDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        var collected = blocks.ToArray();

        ValidateDetachedTree(collected, nameof(blocks));

        var collectedDiagnostics = (diagnostics ?? []).ToArray();

        if (collectedDiagnostics.Any(static diagnostic => diagnostic is null))
        {
            throw new ArgumentNullException(nameof(diagnostics), "A result cannot contain a null diagnostic.");
        }

        Blocks = Array.AsReadOnly(collected);
        Diagnostics = Array.AsReadOnly(collectedDiagnostics);
    }

    /// <summary>Gets root blocks in source order.</summary>
    /// <remarks>
    /// The roots are detached until a successful <see cref="Document.Load"/>
    /// consumes them; afterward the same objects belong to the destination document.
    /// </remarks>
    public IReadOnlyList<DocumentBlock> Blocks { get; }

    /// <summary>Gets diagnostics in source order.</summary>
    public IReadOnlyList<DocumentDiagnostic> Diagnostics { get; }

    /// <summary>Revalidates the complete mutable result tree before its roots transfer to a
    /// document.</summary>
    /// <param name="parameterName">The public parameter that supplied this result.</param>
    /// <exception cref="ArgumentNullException">A root is null.</exception>
    /// <exception cref="ArgumentException">A root is attached or duplicated, the tree is too deep,
    /// or an embedded control is attached or duplicated across any roots.</exception>
    /// <exception cref="ObjectDisposedException">An embedded control is disposed.</exception>
    internal void ValidateForConsumption(string parameterName) => ValidateDetachedTree(Blocks, parameterName);

    private static void ValidateDetachedTree(IReadOnlyList<DocumentBlock> blocks, string parameterName)
    {
        if (blocks.Any(static block => block is null))
        {
            throw new ArgumentNullException(parameterName, "A result cannot contain a null block.");
        }

        if (blocks.Distinct(ReferenceEqualityComparer.Instance).Count() != blocks.Count)
        {
            throw new ArgumentException("A result cannot contain the same block more than once.", parameterName);
        }

        var embeddedControls = new List<ControlBase>();

        foreach (var block in blocks)
        {
            if (block.IsAttached)
            {
                throw new ArgumentException("A result can contain only detached blocks.", parameterName);
            }

            DocumentTreeDepthValidator.ValidateInsertion(block, owner: null);
            DocumentEmbeddedControlCollector.Collect(block, embeddedControls);
        }

        foreach (var control in embeddedControls)
        {
            ObjectDisposedException.ThrowIf(control.IsDisposed, control);
        }

        if (embeddedControls.Distinct(ReferenceEqualityComparer.Instance).Count() != embeddedControls.Count ||
            embeddedControls.Any(static control => control.Parent is not null || control.Dispatcher is not null))
        {
            throw new ArgumentException(
                "Every embedded result control must be detached and appear exactly once across the complete tree.",
                parameterName);
        }
    }
}
