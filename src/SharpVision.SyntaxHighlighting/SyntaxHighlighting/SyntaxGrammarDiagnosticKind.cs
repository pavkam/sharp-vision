// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Identifies the unresolved grammar reference reported during compilation.</summary>
[PublicAPI]
public enum SyntaxGrammarDiagnosticKind
{
    /// <summary>The reference names a syntax definition unavailable to the compiler.</summary>
    MissingDefinition,

    /// <summary>The reference names a context absent from an available definition.</summary>
    MissingContext,

    /// <summary>The reference names a keyword list absent from an available definition.</summary>
    MissingKeywordList,
}
