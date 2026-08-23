// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Identifies one context to push, by its compiled grammar and index within it.</summary>
[PublicAPI]
public readonly record struct SyntaxContextTargetEntry
{
    /// <summary>Initializes a resolved push target.</summary>
    /// <param name="grammar">The non-null grammar the target context belongs to.</param>
    /// <param name="contextIndex">The non-negative index into <paramref name="grammar"/>'s contexts.</param>
    internal SyntaxContextTargetEntry(SyntaxGrammar grammar, int contextIndex)
    {
        Grammar = grammar;
        ContextIndex = contextIndex;
    }

    /// <summary>Gets the grammar the target context belongs to.</summary>
    public SyntaxGrammar Grammar { get; }

    /// <summary>Gets the target context's index within <see cref="Grammar"/>'s contexts.</summary>
    public int ContextIndex { get; }
}
