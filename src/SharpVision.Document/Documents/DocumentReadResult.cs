// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Documents;

using SharpVision.Controls.Documents;

/// <summary>Contains one detached parsed tree and its diagnostics.</summary>
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

        if (collected.Any(static block => block is null))
        {
            throw new ArgumentNullException(nameof(blocks), "A result cannot contain a null block.");
        }

        if (collected.Distinct(ReferenceEqualityComparer.Instance).Count() != collected.Length)
        {
            throw new ArgumentException("A result cannot contain the same block more than once.", nameof(blocks));
        }

        var embeddedControls = new List<ControlBase>();

        foreach (var block in collected)
        {
            if (block.IsAttached)
            {
                throw new ArgumentException("A result can contain only detached blocks.", nameof(blocks));
            }

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
                "Every embedded result control must be detached and appear exactly once.",
                nameof(blocks));
        }

        var collectedDiagnostics = (diagnostics ?? []).ToArray();

        if (collectedDiagnostics.Any(static diagnostic => diagnostic is null))
        {
            throw new ArgumentNullException(nameof(diagnostics), "A result cannot contain a null diagnostic.");
        }

        Blocks = Array.AsReadOnly(collected);
        Diagnostics = Array.AsReadOnly(collectedDiagnostics);
    }

    /// <summary>Gets detached root blocks in source order.</summary>
    public IReadOnlyList<DocumentBlock> Blocks { get; }

    /// <summary>Gets diagnostics in source order.</summary>
    public IReadOnlyList<DocumentDiagnostic> Diagnostics { get; }
}
