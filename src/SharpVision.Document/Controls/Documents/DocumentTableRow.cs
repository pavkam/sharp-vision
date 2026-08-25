// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Represents one row of semantic table cells.</summary>
[PublicAPI]
public sealed class DocumentTableRow: DocumentNode
{
    /// <summary>Initializes an empty body row.</summary>
    public DocumentTableRow() => Cells = new DocumentTableCellCollection(this);

    /// <summary>Gets or sets whether this row labels its columns.</summary>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The attached owner is disposed.</exception>
    public bool IsHeader
    {
        get;
        set
        {
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            InvalidateContent();
        }
    }

    /// <summary>Gets cells in column order.</summary>
    public DocumentTableCellCollection Cells { get; }
}
