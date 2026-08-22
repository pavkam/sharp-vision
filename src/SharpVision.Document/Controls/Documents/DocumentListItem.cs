// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Represents one marked item of a <see cref="DocumentList"/>.</summary>
/// <remarks>
/// An item usually holds a single <see cref="DocumentParagraph"/>, but its
/// <see cref="Blocks"/> accepts any block sequence - most commonly a paragraph followed by a nested
/// <see cref="DocumentList"/>. An item belongs to a list and nowhere else: it is not a
/// <see cref="DocumentBlock"/>, so it cannot be placed directly in a document or a block quote where
/// it would render with no marker.
/// </remarks>
[PublicAPI]
public sealed class DocumentListItem: DocumentNode
{
    /// <summary>Initializes an empty item.</summary>
    public DocumentListItem() => Blocks = new DocumentBlockCollection(this);

    /// <summary>Initializes an item with one non-null detached block.</summary>
    /// <param name="block">The non-null detached block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="block"/> already belongs to a document tree.</exception>
    public DocumentListItem(DocumentBlock block) : this()
    {
        ArgumentNullException.ThrowIfNull(block);
        Blocks.Add(block);
    }

    /// <summary>Initializes an item with one non-null inline-markup text paragraph.</summary>
    /// <param name="text">The non-null markup string.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public DocumentListItem(string text) : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        Blocks.Add(new DocumentParagraph(text));
    }

    /// <summary>Initializes an item with one non-null inline-markup text paragraph followed by one
    /// non-null detached nested list.</summary>
    /// <param name="text">The non-null markup string.</param>
    /// <param name="nested">The non-null detached nested list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="nested"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="nested"/> already belongs to a document tree.</exception>
    public DocumentListItem(string text, DocumentList nested) : this(text)
    {
        ArgumentNullException.ThrowIfNull(nested);
        Blocks.Add(nested);
    }

    /// <summary>Gets the owned ordered block content.</summary>
    public DocumentBlockCollection Blocks { get; }
}
