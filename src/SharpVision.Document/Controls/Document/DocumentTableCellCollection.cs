// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Exposes one table row's owned ordered cells.</summary>
[PublicAPI]
public sealed class DocumentTableCellCollection: DocumentNodeCollection<DocumentTableCell>
{
    /// <summary>Initializes a cell collection.</summary>
    /// <param name="owner">The owning row.</param>
    internal DocumentTableCellCollection(DocumentTableRow owner) : base(owner)
    {
    }
}
