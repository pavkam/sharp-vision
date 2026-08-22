// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Represents one indented block quotation, marked with a vertical bar down its left
/// edge.</summary>
/// <remarks>
/// A quote holds its own block sequence and nests freely: a quote inside a quote indents twice and
/// draws two bars.
/// </remarks>
[PublicAPI]
public sealed class DocumentBlockQuote: DocumentBlock
{
    /// <summary>Initializes an empty block quote.</summary>
    public DocumentBlockQuote() => Blocks = new DocumentBlockCollection(this);

    /// <summary>Initializes a block quote with one non-null detached block.</summary>
    /// <param name="block">The non-null detached block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="block"/> already belongs to a document tree.</exception>
    public DocumentBlockQuote(DocumentBlock block) : this()
    {
        ArgumentNullException.ThrowIfNull(block);
        Blocks.Add(block);
    }

    /// <summary>Initializes a block quote with one non-null inline-markup text paragraph.</summary>
    /// <param name="text">The non-null markup string.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public DocumentBlockQuote(string text) : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        Blocks.Add(new DocumentParagraph(text));
    }

    /// <summary>Gets the owned ordered block content.</summary>
    public DocumentBlockCollection Blocks { get; }
}
