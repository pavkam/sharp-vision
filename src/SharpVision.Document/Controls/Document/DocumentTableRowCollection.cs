// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Exposes one table's owned ordered rows.</summary>
[PublicAPI]
public sealed class DocumentTableRowCollection: DocumentNodeCollection<DocumentTableRow>
{
    /// <summary>Initializes a row collection.</summary>
    /// <param name="owner">The owning table.</param>
    internal DocumentTableRowCollection(DocumentTable owner) : base(owner)
    {
    }
}
