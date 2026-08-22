// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Exposes one <see cref="DocumentList"/>'s ordered items.</summary>
[PublicAPI]
public sealed class DocumentListItemCollection: DocumentNodeCollection<DocumentListItem>
{
    /// <summary>Initializes an item collection owned by one list.</summary>
    /// <param name="ownerList">The owning list.</param>
    internal DocumentListItemCollection(DocumentList ownerList) : base(ownerList)
    {
    }
}
