// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Matches one word, scanned up to the next <see cref="SyntaxWordDelimiters"/> boundary, against a
/// resolved <see cref="SyntaxKeywordList"/> using its resolved case sensitivity.
/// </summary>
[PublicAPI]
public sealed class SyntaxKeywordMatcher
{
    private readonly HashSet<string> _words;
    private readonly SyntaxWordDelimiters _delimiters;

    /// <summary>Initializes a matcher for one keyword list at one resolved case sensitivity.</summary>
    /// <param name="words">The non-null literal words.</param>
    /// <param name="caseSensitive">Whether comparison is case sensitive.</param>
    /// <param name="delimiters">The fully resolved word-delimiter set for this rule instance.</param>
    internal SyntaxKeywordMatcher(IReadOnlyList<string> words, bool caseSensitive, SyntaxWordDelimiters delimiters)
    {
        _words = new HashSet<string>(
            words,
            caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        _delimiters = delimiters;
    }

    /// <summary>
    /// Attempts to match one keyword starting exactly at <paramref name="offset"/>.
    /// </summary>
    /// <param name="line">The complete line text.</param>
    /// <param name="offset">The zero-based offset to match at.</param>
    /// <returns>The matched word length, or zero when no keyword matches at this offset.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="offset"/> is negative or greater than <paramref name="line"/>'s length.
    /// </exception>
    [Pure]
    public int Match(ReadOnlySpan<char> line, int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, line.Length);

        var end = offset;

        while (end < line.Length && !_delimiters.Contains(line[end]))
        {
            end++;
        }

        return end == offset ? 0 : _words.Contains(line[offset..end].ToString()) ? end - offset : 0;
    }
}
