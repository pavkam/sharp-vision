// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

/// <summary>Returns a detached result after canceling an owned token source.</summary>
internal sealed class CancelingDocumentFormatReaderProbe: IDocumentFormatReader
{
    private readonly CancellationTokenSource _cancellation;

    /// <summary>Initializes a reader that cancels the non-null source during parsing.</summary>
    /// <param name="cancellation">The source canceled by <see cref="Read"/>.</param>
    internal CancelingDocumentFormatReaderProbe(CancellationTokenSource cancellation)
    {
        ArgumentNullException.ThrowIfNull(cancellation);
        _cancellation = cancellation;
    }

    /// <inheritdoc/>
    public DocumentReadResult Read(string source, DocumentReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        _cancellation.Cancel();
        return new DocumentReadResult([new DocumentParagraph(source)]);
    }
}
