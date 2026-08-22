// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

/// <summary>Returns one fixed result so tests can exercise repeated format-reader consumption.</summary>
internal sealed class StaticDocumentFormatReaderProbe: IDocumentFormatReader
{
    private readonly DocumentReadResult _result;

    /// <summary>Initializes the probe with the result returned by every read.</summary>
    /// <param name="result">The non-null fixed result.</param>
    internal StaticDocumentFormatReaderProbe(DocumentReadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _result = result;
    }

    /// <summary>Gets how many reads were requested, proving validation ordering.</summary>
    internal int ReadCalls { get; private set; }

    /// <inheritdoc/>
    public DocumentReadResult Read(string source, DocumentReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ReadCalls++;
        return _result;
    }
}
