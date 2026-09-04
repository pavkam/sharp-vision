// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Documents;

/// <summary>Converts one serialized format into a detached document tree.</summary>
[PublicAPI]
public interface IDocumentFormatReader
{
    /// <summary>Reads a non-null source string without mutating a <see cref="Controls.Document.Document"/>.</summary>
    /// <param name="source">The non-null serialized source.</param>
    /// <param name="options">Optional general read limits.</param>
    /// <returns>The detached tree and deterministic diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The source exceeds an enabled limit.</exception>
    public DocumentReadResult Read(string source, DocumentReadOptions? options = null);
}
