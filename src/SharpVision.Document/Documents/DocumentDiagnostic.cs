// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Documents;

/// <summary>Describes one deterministic non-fatal format-reader diagnostic.</summary>
[PublicAPI]
public sealed class DocumentDiagnostic
{
    /// <summary>Initializes a diagnostic.</summary>
    /// <param name="message">The non-empty message.</param>
    /// <param name="span">The source range.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="message"/> is empty or all whitespace.</exception>
    public DocumentDiagnostic(string message, DocumentSourceSpan span)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Message = message;
        Span = span;
    }

    /// <summary>Gets the diagnostic message.</summary>
    public string Message { get; }

    /// <summary>Gets the implicated source range.</summary>
    public DocumentSourceSpan Span { get; }
}
