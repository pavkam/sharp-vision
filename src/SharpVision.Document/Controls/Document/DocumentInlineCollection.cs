// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Exposes one <see cref="DocumentParagraph"/> or <see cref="DocumentHeading"/>'s ordered
/// flowing inline content.</summary>
[PublicAPI]
public sealed class DocumentInlineCollection: DocumentNodeCollection<DocumentInline>
{
    /// <summary>Initializes an inline collection owned by one node.</summary>
    /// <param name="ownerNode">The owning node.</param>
    internal DocumentInlineCollection(DocumentNode ownerNode) : base(ownerNode)
    {
    }
}
