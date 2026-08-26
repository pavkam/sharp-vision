// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Describes one unresolved definition, context, or keyword-list reference that
/// compilation safely omitted from a grammar.</summary>
[PublicAPI]
public sealed class SyntaxGrammarDiagnostic
{
    /// <summary>Initializes one immutable grammar diagnostic.</summary>
    /// <param name="kind">The missing target kind.</param>
    /// <param name="sourceDefinition">The non-empty definition containing the reference.</param>
    /// <param name="reference">The non-empty reference text as declared by the source.</param>
    internal SyntaxGrammarDiagnostic(
        SyntaxGrammarDiagnosticKind kind,
        string sourceDefinition,
        string reference)
    {
        Kind = kind;
        SourceDefinition = sourceDefinition;
        Reference = reference;
    }

    /// <summary>Gets the missing target kind.</summary>
    public SyntaxGrammarDiagnosticKind Kind { get; }

    /// <summary>Gets the definition containing the unresolved reference.</summary>
    public string SourceDefinition { get; }

    /// <summary>Gets the unresolved context or keyword-list reference text.</summary>
    public string Reference { get; }
}
