// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Represents one paragraph of flowing inline content.</summary>
/// <remarks>
/// A paragraph wraps its <see cref="Inlines"/> to the document's content width. An empty paragraph
/// still occupies one line, which makes it a deliberate way to add vertical space.
/// </remarks>
[PublicAPI]
public sealed class DocumentParagraph: DocumentBlock
{
    /// <summary>Initializes an empty paragraph.</summary>
    public DocumentParagraph() => Inlines = new DocumentInlineCollection(this);

    /// <summary>Initializes a paragraph with one non-null inline-markup text run.</summary>
    /// <param name="text">The non-null markup string.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public DocumentParagraph(string text) : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        Inlines.Add(new DocumentTextRun(text));
    }

    /// <summary>Gets the owned ordered inline content.</summary>
    public DocumentInlineCollection Inlines { get; }
}
