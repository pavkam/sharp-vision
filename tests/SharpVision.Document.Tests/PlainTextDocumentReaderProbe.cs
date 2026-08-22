// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

/// <summary>Provides a second format implementation for abstraction tests.</summary>
internal sealed class PlainTextDocumentReaderProbe: IDocumentFormatReader
{
    /// <inheritdoc/>
    public DocumentReadResult Read(string source, DocumentReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new DocumentReadResult([new DocumentParagraph(source)]);
    }
}
