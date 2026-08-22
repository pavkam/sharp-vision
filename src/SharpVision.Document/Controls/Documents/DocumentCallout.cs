// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Represents a typed emphasized aside with a title and nested blocks.</summary>
[PublicAPI]
public sealed class DocumentCallout: DocumentBlock
{
    /// <summary>Initializes a NOTE callout.</summary>
    public DocumentCallout()
    {
        Kind = "NOTE";
        Title = string.Empty;
        Blocks = new DocumentBlockCollection(this);
    }

    /// <summary>Gets or sets the non-empty ordinal callout kind.</summary>
    /// <exception cref="ArgumentException">The value is empty.</exception>
    public string Kind
    {
        get;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            VerifyMutable();

            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            InvalidateContent();
        }
    }

    /// <summary>Gets or sets the non-null display title.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public string Title
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();

            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            InvalidateContent();
        }
    }

    /// <summary>Gets the owned callout body.</summary>
    public DocumentBlockCollection Blocks { get; }
}
