// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Matches one word, scanned up to the next <see cref="SyntaxWordDelimiters"/> boundary, against a
/// resolved <see cref="SyntaxKeywordList"/> using its resolved case sensitivity.
/// </summary>
internal sealed class SyntaxKeywordMatcher
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
    internal int Match(ReadOnlySpan<char> line, int offset) => MatchWithSkip(line, offset).Length;

    /// <summary>
    /// Attempts to match one keyword starting exactly at <paramref name="offset"/>, additionally
    /// reporting how far forward a caller may skip re-invoking this method within the same
    /// undelimited run without missing a potential match: scanning from any later offset that is
    /// still short of the reported boundary always re-derives the identical delimiter boundary,
    /// so a caller retrying position by position gains nothing by calling this method again before
    /// reaching it.
    /// </summary>
    /// <param name="line">The complete line text.</param>
    /// <param name="offset">The zero-based offset to match at.</param>
    /// <returns>
    /// The matched word length (zero when no keyword matches), and the exclusive offset before
    /// which this method's own boundary scan is guaranteed to reach the same result.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="offset"/> is negative or greater than <paramref name="line"/>'s length.
    /// </exception>
    [Pure]
    internal (int Length, int SkipOffset) MatchWithSkip(ReadOnlySpan<char> line, int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, line.Length);

        var end = offset;

        while (end < line.Length && !_delimiters.Contains(line[end]))
        {
            end++;
        }

        if (end == offset)
        {
            return (0, offset);
        }

        return _words.GetAlternateLookup<ReadOnlySpan<char>>().Contains(line[offset..end])
            ? (end - offset, end)
            : (0, end);
    }
}
