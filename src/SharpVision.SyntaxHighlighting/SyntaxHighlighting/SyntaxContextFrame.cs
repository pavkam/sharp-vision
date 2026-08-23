// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Represents one entry on <see cref="SyntaxTokenizer"/>'s context stack.</summary>
internal readonly struct SyntaxContextFrame
{
    /// <summary>Initializes a stack frame.</summary>
    /// <param name="grammar">The grammar the active context belongs to.</param>
    /// <param name="contextIndex">The active context's index within <paramref name="grammar"/>.</param>
    /// <param name="captures">The dynamic arguments bound when this frame was pushed.</param>
    public SyntaxContextFrame(SyntaxGrammar grammar, int contextIndex, IReadOnlyList<string> captures)
    {
        Grammar = grammar;
        ContextIndex = contextIndex;
        Captures = captures;
    }

    /// <summary>Gets the grammar the active context belongs to.</summary>
    public SyntaxGrammar Grammar { get; }

    /// <summary>Gets the active context's index within <see cref="Grammar"/>.</summary>
    public int ContextIndex { get; }

    /// <summary>Gets the dynamic arguments bound when this frame was pushed.</summary>
    public IReadOnlyList<string> Captures { get; }

    /// <summary>Gets the active context.</summary>
    public SyntaxGrammarContext Context => Grammar.Contexts[ContextIndex];
}
