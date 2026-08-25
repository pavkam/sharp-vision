// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

/// <summary>Provides a deterministic format-reader failure after stream decoding completes.</summary>
internal sealed class ThrowingDocumentFormatReaderProbe: IDocumentFormatReader
{
    /// <inheritdoc/>
    public DocumentReadResult Read(string source, DocumentReadOptions? options = null) =>
        throw new InvalidDataException("The format reader rejected the decoded source.");
}
