// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Describes one regular expression that indentation-based folding treats as an empty line, such
/// as a comment-only line in a language whose folding is otherwise indentation sensitive.
/// </summary>
[PublicAPI]
public sealed class SyntaxEmptyLineRule
{
    /// <summary>Initializes a fully specified, internally validated empty-line rule.</summary>
    /// <param name="pattern">The non-null regular expression matched against a whole line.</param>
    /// <param name="caseSensitive">Whether <paramref name="pattern"/> matches case sensitively.</param>
    internal SyntaxEmptyLineRule(string pattern, bool caseSensitive)
    {
        Pattern = pattern;
        CaseSensitive = caseSensitive;
    }

    /// <summary>Gets the regular expression matched against a whole line.</summary>
    public string Pattern { get; }

    /// <summary>Gets whether <see cref="Pattern"/> matches case sensitively.</summary>
    public bool CaseSensitive { get; }
}
