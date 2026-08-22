// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Represents a block table whose rows share measured column widths.</summary>
[PublicAPI]
public sealed class DocumentTable: DocumentBlock
{
    /// <summary>Initializes an empty table.</summary>
    public DocumentTable() => Rows = new DocumentTableRowCollection(this);

    /// <summary>Gets rows in display order.</summary>
    public DocumentTableRowCollection Rows { get; }
}
