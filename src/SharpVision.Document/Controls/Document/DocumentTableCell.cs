// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Represents one table cell containing semantic inline flow.</summary>
[PublicAPI]
public sealed class DocumentTableCell: DocumentNode
{
    /// <summary>Initializes an empty leading-aligned cell.</summary>
    public DocumentTableCell() => Inlines = new DocumentInlineCollection(this);

    /// <summary>Initializes a cell with one non-null text run.</summary>
    /// <param name="text">The non-null text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public DocumentTableCell(string text) : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        Inlines.Add(new DocumentTextRun(text));
    }

    /// <summary>Gets or sets horizontal content alignment.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The attached owner is disposed.</exception>
    public DocumentTableCellAlignment Alignment
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The table-cell alignment is unknown.");
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            InvalidateContent();
        }
    }

    /// <summary>Gets semantic inline content.</summary>
    public DocumentInlineCollection Inlines { get; }
}
