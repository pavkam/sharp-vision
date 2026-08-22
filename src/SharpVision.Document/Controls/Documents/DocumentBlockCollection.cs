// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Exposes one owner's ordered block-level content.</summary>
/// <remarks>
/// Accepts only <see cref="DocumentBlock"/> nodes. Use <see cref="DocumentBlockControl"/> when one
/// block intentionally mounts a retained <see cref="ControlBase"/>.
/// </remarks>
[PublicAPI]
public sealed class DocumentBlockCollection: DocumentNodeCollection<DocumentBlock>
{
    /// <summary>Initializes a block collection owned by one node.</summary>
    /// <param name="ownerNode">The owning node.</param>
    internal DocumentBlockCollection(DocumentNode ownerNode) : base(ownerNode)
    {
    }

    /// <summary>Initializes a block collection owned by one document.</summary>
    /// <param name="ownerDocument">The owning document.</param>
    internal DocumentBlockCollection(Document ownerDocument) : base(ownerDocument)
    {
    }
}
