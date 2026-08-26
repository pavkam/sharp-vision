// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Wraps one parsed <see cref="SyntaxRule"/> with its grammar-resolved style, context target, and
/// compiled matching data, and performs the actual per-position match KDE's <c>Rule::doMatch</c>
/// family of subclasses implement.
/// </summary>
/// <remarks>
/// Every matcher below is a direct, line-oriented C# port of the corresponding upstream
/// KSyntaxHighlighting <c>rule.cpp</c> matcher, verified against that source rather than against
/// memory or the higher-level XML format documentation, since several rules (dynamic
/// <see cref="SyntaxRuleKind.Character"/> capture-index encoding, <see cref="SyntaxRuleKind.WordMatch"/>'s
/// boundary check on both the character before and the first matched character, and the exact
/// escape-sequence grammar <see cref="SyntaxRuleKind.EscapedCharacter"/> and
/// <see cref="SyntaxRuleKind.QuotedCharacter"/> share) are not fully specified by the public XML
/// format documentation alone.
/// </remarks>
[PublicAPI]
public sealed class SyntaxCompiledRule
{
    /// <summary>The maximum number of capture-substituted patterns retained by one dynamic rule.</summary>
    private const int _dynamicRegexCacheCapacity = 64;

    private readonly SyntaxKeywordMatcher? _keywordMatcher;
    private readonly SyntaxWordDelimiters _delimiters;
    private readonly PcreRegex? _staticRegex;
    private readonly SyntaxRegularExpressionCache? _dynamicRegexCache;
    private readonly bool _regularExpressionIsValid = true;
    private readonly bool _capturesRequired;

    /// <summary>Initializes a compiled rule.</summary>
    /// <param name="source">The non-null parsed rule this instance compiles.</param>
    /// <param name="resolvedStyle">
    /// The style role this rule's own matched text is painted with, or null when this rule
    /// declared no attribute of its own, in which case a match is styled with whichever context
    /// is active on top of the stack at match time (not necessarily the context this rule was
    /// originally declared in, since <c>IncludeRules</c> can splice it into another context).
    /// </param>
    /// <param name="resolvedTarget">The resolved context change to apply after a match.</param>
    /// <param name="delimiters">The fully resolved word-delimiter set for this rule instance.</param>
    /// <param name="keywordMatcher">
    /// The resolved keyword matcher for a <see cref="SyntaxRuleKind.Keyword"/> rule, otherwise null.
    /// </param>
    /// <param name="capturesRequired">Whether the resolved target consumes regular-expression captures.</param>
    internal SyntaxCompiledRule(
        SyntaxRule source,
        SyntaxDefaultStyle? resolvedStyle,
        SyntaxContextTarget resolvedTarget,
        SyntaxWordDelimiters delimiters,
        SyntaxKeywordMatcher? keywordMatcher,
        bool capturesRequired)
    {
        Source = source;
        ResolvedStyle = resolvedStyle;
        ResolvedTarget = resolvedTarget;
        _delimiters = delimiters;
        _keywordMatcher = keywordMatcher;
        _capturesRequired = capturesRequired;

        if (source.Kind == SyntaxRuleKind.RegularExpression)
        {
            if (source.Dynamic)
            {
                _dynamicRegexCache = new SyntaxRegularExpressionCache(_dynamicRegexCacheCapacity);
                var initial = CompileRegex(source.Text ?? string.Empty, source, out _regularExpressionIsValid);
                _ = _dynamicRegexCache.GetOrAdd(source.Text ?? string.Empty, _ => initial);
            }
            else
            {
                _staticRegex = CompileRegex(source.Text ?? string.Empty, source, out _regularExpressionIsValid);
            }
        }
    }

    /// <summary>Gets the parsed rule this instance compiles.</summary>
    public SyntaxRule Source { get; }

    /// <summary>
    /// Gets whether a failed <see cref="TryMatch"/> reports a meaningful <see
    /// cref="SyntaxRuleMatch.SkipOffset"/> - true only for <see cref="SyntaxRuleKind.Keyword"/>
    /// and <see cref="SyntaxRuleKind.RegularExpression"/>, the two kinds whose own matching
    /// process already discovers, as a side effect of a single failed attempt, exactly how far
    /// forward a retry would need to move to have any chance of succeeding. <see
    /// cref="SyntaxTokenizer"/> uses this to cache that bound per line and skip re-invoking these
    /// two rule kinds at intermediate offsets they cannot possibly match at, the same way upstream
    /// KSyntaxHighlighting's <c>Rule::hasSkipOffset</c> gates its own per-line skip-offset cache.
    /// </summary>
    internal bool HasSkipOffset => Source.Kind is SyntaxRuleKind.Keyword or SyntaxRuleKind.RegularExpression;

    /// <summary>Gets the number of compiled regular expressions retained by this rule so tests can
    /// prove the dynamic-pattern lifetime bound without reflecting into its cache implementation.</summary>
    internal int CachedRegularExpressionCount =>
        _dynamicRegexCache?.Count ?? (_staticRegex is null ? 0 : 1);

    /// <summary>Gets whether this rule's authored regular-expression pattern compiled under the
    /// KDE-compatible engine, so corpus tests can reject silently disabled shipped rules.</summary>
    internal bool RegularExpressionIsValid => _regularExpressionIsValid;

    /// <summary>
    /// Gets the style role this rule's own matched text is painted with, or null to inherit
    /// whichever context is active on top of the stack at match time.
    /// </summary>
    public SyntaxDefaultStyle? ResolvedStyle { get; }

    /// <summary>Gets the resolved context change to apply after a successful, non-lookahead match.</summary>
    public SyntaxContextTarget ResolvedTarget { get; }

    /// <summary>Attempts to match this rule starting exactly at <paramref name="offset"/>.</summary>
    /// <param name="line">The complete line text, excluding the line terminator.</param>
    /// <param name="offset">The zero-based offset to match at.</param>
    /// <param name="captures">
    /// The owning context's currently bound dynamic arguments, consulted only when
    /// <see cref="SyntaxRule.Dynamic"/> is set.
    /// </param>
    /// <returns>The match outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="line"/> or <paramref name="captures"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="offset"/> is negative or greater than <paramref name="line"/>'s length.
    /// </exception>
    [Pure]
    public SyntaxRuleMatch TryMatch(string line, int offset, IReadOnlyList<string> captures)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(captures);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, line.Length);

        var span = line.AsSpan();

        return Source.Kind switch
        {
            SyntaxRuleKind.Keyword => MatchKeyword(span, offset),
            SyntaxRuleKind.Float => MatchFloat(span, offset),
            SyntaxRuleKind.Octal => MatchOctal(span, offset),
            SyntaxRuleKind.Hex => MatchHex(span, offset),
            SyntaxRuleKind.Integer => MatchInteger(span, offset),
            SyntaxRuleKind.Character => MatchCharacter(span, offset, captures),
            SyntaxRuleKind.TwoCharacter => MatchTwoCharacters(span, offset),
            SyntaxRuleKind.AnyCharacter => MatchAnyCharacter(span, offset),
            SyntaxRuleKind.StringMatch => MatchStringLiteral(span, offset, captures),
            SyntaxRuleKind.WordMatch => MatchWord(span, offset),
            SyntaxRuleKind.RegularExpression => MatchRegularExpression(line, offset, captures),
            SyntaxRuleKind.LineContinuation => MatchLineContinuation(span, offset),
            SyntaxRuleKind.EscapedCharacter => MatchEscapedCharacterRule(span, offset),
            SyntaxRuleKind.Range => MatchRange(span, offset),
            SyntaxRuleKind.QuotedCharacter => MatchQuotedCharacter(span, offset),
            SyntaxRuleKind.DetectSpaces => MatchSpaces(span, offset),
            SyntaxRuleKind.DetectIdentifier => MatchIdentifier(span, offset),

            // IncludeRules never survives grammar compilation as a standalone rule: its target's
            // rules are spliced in its place. Reaching this defensively means the target could not
            // be resolved, in which case it correctly contributes no match.
            SyntaxRuleKind.IncludeRules => SyntaxRuleMatch.None,

            _ => SyntaxRuleMatch.None,
        };
    }

    #region Numeric literals

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    private static bool IsAsciiOctalDigit(char c) => c is >= '0' and <= '7';

    private static bool IsAsciiHexDigit(char c) => IsAsciiDigit(c) || c is (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

    private bool PrecededByDelimiter(ReadOnlySpan<char> line, int offset) =>
        offset == 0 || _delimiters.Contains(line[offset - 1]);

    private SyntaxRuleMatch MatchInteger(ReadOnlySpan<char> line, int offset)
    {
        if (!PrecededByDelimiter(line, offset))
        {
            return SyntaxRuleMatch.None;
        }

        var end = offset;

        while (end < line.Length && IsAsciiDigit(line[end]))
        {
            end++;
        }

        return end == offset ? SyntaxRuleMatch.None : new SyntaxRuleMatch(end - offset, []);
    }

    private SyntaxRuleMatch MatchOctal(ReadOnlySpan<char> line, int offset)
    {
        if (!PrecededByDelimiter(line, offset) || line.Length < offset + 2 || line[offset] != '0' || !IsAsciiOctalDigit(line[offset + 1]))
        {
            return SyntaxRuleMatch.None;
        }

        var end = offset + 2;

        while (end < line.Length && IsAsciiOctalDigit(line[end]))
        {
            end++;
        }

        return new SyntaxRuleMatch(end - offset, []);
    }

    private SyntaxRuleMatch MatchHex(ReadOnlySpan<char> line, int offset)
    {
        if (!PrecededByDelimiter(line, offset) ||
            line.Length < offset + 3 ||
            line[offset] != '0' ||
            (line[offset + 1] != 'x' && line[offset + 1] != 'X') ||
            !IsAsciiHexDigit(line[offset + 2]))
        {
            return SyntaxRuleMatch.None;
        }

        var end = offset + 3;

        while (end < line.Length && IsAsciiHexDigit(line[end]))
        {
            end++;
        }

        return new SyntaxRuleMatch(end - offset, []);
    }

    private SyntaxRuleMatch MatchFloat(ReadOnlySpan<char> line, int offset)
    {
        if (!PrecededByDelimiter(line, offset))
        {
            return SyntaxRuleMatch.None;
        }

        var end = offset;

        while (end < line.Length && IsAsciiDigit(line[end]))
        {
            end++;
        }

        if (end >= line.Length || line[end] != '.')
        {
            return SyntaxRuleMatch.None;
        }

        end++;

        while (end < line.Length && IsAsciiDigit(line[end]))
        {
            end++;
        }

        if (end == offset + 1)
        {
            // Only a decimal point was found, with no digit on either side.
            return SyntaxRuleMatch.None;
        }

        var exponentEnd = end;

        if (exponentEnd >= line.Length || (line[exponentEnd] != 'e' && line[exponentEnd] != 'E'))
        {
            return new SyntaxRuleMatch(end - offset, []);
        }

        exponentEnd++;

        if (exponentEnd < line.Length && (line[exponentEnd] == '+' || line[exponentEnd] == '-'))
        {
            exponentEnd++;
        }

        var foundExponentDigit = false;

        while (exponentEnd < line.Length && IsAsciiDigit(line[exponentEnd]))
        {
            exponentEnd++;
            foundExponentDigit = true;
        }

        return new SyntaxRuleMatch((foundExponentDigit ? exponentEnd : end) - offset, []);
    }

    #endregion

    #region Character and string literals

    private SyntaxRuleMatch MatchCharacter(ReadOnlySpan<char> line, int offset, IReadOnlyList<string> captures)
    {
        if (offset >= line.Length)
        {
            return SyntaxRuleMatch.None;
        }

        if (!Source.Dynamic)
        {
            return line[offset] == Source.Char1 ? new SyntaxRuleMatch(1, []) : SyntaxRuleMatch.None;
        }

        var captureIndex = Source.Char1 - '0' - 1;

        return captureIndex < 0 || captureIndex >= captures.Count || captures[captureIndex].Length == 0
            ? SyntaxRuleMatch.None
            : line[offset] == captures[captureIndex][0] ? new SyntaxRuleMatch(1, []) : SyntaxRuleMatch.None;
    }

    private SyntaxRuleMatch MatchTwoCharacters(ReadOnlySpan<char> line, int offset) =>
        offset + 2 <= line.Length && line[offset] == Source.Char1 && line[offset + 1] == Source.Char2
            ? new SyntaxRuleMatch(2, [])
            : SyntaxRuleMatch.None;

    private SyntaxRuleMatch MatchAnyCharacter(ReadOnlySpan<char> line, int offset)
    {
        Debug.Assert(Source.Text is not null, "The reader requires an AnyChar rule's String attribute.");
        return offset < line.Length && (Source.Text ?? string.Empty).AsSpan().Contains(line[offset])
            ? new SyntaxRuleMatch(1, [])
            : SyntaxRuleMatch.None;
    }

    private SyntaxRuleMatch MatchStringLiteral(ReadOnlySpan<char> line, int offset, IReadOnlyList<string> captures)
    {
        Debug.Assert(Source.Text is not null, "The reader requires a StringDetect rule's String attribute.");
        var pattern = Source.Dynamic ? ReplaceCaptures(Source.Text ?? string.Empty, captures, escapeForRegex: false) : Source.Text ?? string.Empty;
        var comparison = Source.Insensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        return offset + pattern.Length <= line.Length && line.Slice(offset, pattern.Length).Equals(pattern, comparison)
            ? new SyntaxRuleMatch(pattern.Length, [])
            : SyntaxRuleMatch.None;
    }

    private SyntaxRuleMatch MatchWord(ReadOnlySpan<char> line, int offset)
    {
        Debug.Assert(Source.Text is not null, "The reader requires a WordDetect rule's String attribute.");
        var word = Source.Text ?? string.Empty;

        if (word.Length == 0 || line.Length - offset < word.Length)
        {
            return SyntaxRuleMatch.None;
        }

        if (offset > 0 && !_delimiters.Contains(line[offset - 1]) && !_delimiters.Contains(line[offset]))
        {
            return SyntaxRuleMatch.None;
        }

        var comparison = Source.Insensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (!line.Slice(offset, word.Length).Equals(word, comparison))
        {
            return SyntaxRuleMatch.None;
        }

        var afterEnd = offset + word.Length;

        return afterEnd == line.Length || _delimiters.Contains(line[afterEnd]) || _delimiters.Contains(line[afterEnd - 1])
            ? new SyntaxRuleMatch(word.Length, [])
            : SyntaxRuleMatch.None;
    }

    private SyntaxRuleMatch MatchRange(ReadOnlySpan<char> line, int offset)
    {
        if (line.Length - offset < 2 || line[offset] != Source.Char1)
        {
            return SyntaxRuleMatch.None;
        }

        for (var i = offset + 1; i < line.Length; i++)
        {
            if (line[i] == Source.Char2)
            {
                return new SyntaxRuleMatch(i + 1 - offset, []);
            }
        }

        return SyntaxRuleMatch.None;
    }

    private SyntaxRuleMatch MatchLineContinuation(ReadOnlySpan<char> line, int offset) =>
        line.Length > 0 && offset == line.Length - 1 && line[offset] == Source.Char1
            ? new SyntaxRuleMatch(1, [])
            : SyntaxRuleMatch.None;

    private static SyntaxRuleMatch MatchQuotedCharacter(ReadOnlySpan<char> line, int offset)
    {
        if (line.Length < offset + 3 || line[offset] != '\'' || line[offset + 1] == '\'')
        {
            return SyntaxRuleMatch.None;
        }

        var end = MatchEscapedChar(line, offset + 1);

        if (end == offset + 1)
        {
            if (line[end] == '\\')
            {
                return SyntaxRuleMatch.None;
            }

            end++;
        }

        return end < line.Length && line[end] == '\''
            ? new SyntaxRuleMatch(end + 1 - offset, [])
            : SyntaxRuleMatch.None;
    }

    private static SyntaxRuleMatch MatchEscapedCharacterRule(ReadOnlySpan<char> line, int offset)
    {
        var end = MatchEscapedChar(line, offset);
        return end == offset ? SyntaxRuleMatch.None : new SyntaxRuleMatch(end - offset, []);
    }

    /// <summary>
    /// Matches one C-style backslash escape sequence starting at <paramref name="offset"/>: a
    /// single-character escape (<c>\n</c>, <c>\"</c>, ...), a one-to-two digit hexadecimal escape
    /// (<c>\x1F</c>), or a one-to-three digit octal escape (<c>\017</c>).
    /// </summary>
    private static int MatchEscapedChar(ReadOnlySpan<char> line, int offset)
    {
        if (offset >= line.Length || line[offset] != '\\' || line.Length < offset + 2)
        {
            return offset;
        }

        switch (line[offset + 1])
        {
            case 'a' or 'b' or 'e' or 'f' or 'n' or 'r' or 't' or 'v' or '"' or '\'' or '?' or '\\':
                return offset + 2;

            case 'x':
                if (offset + 2 < line.Length && IsAsciiHexDigit(line[offset + 2]))
                {
                    return offset + 3 < line.Length && IsAsciiHexDigit(line[offset + 3]) ? offset + 4 : offset + 3;
                }

                return offset;

            case >= '0' and <= '7':
                if (offset + 2 < line.Length && IsAsciiOctalDigit(line[offset + 2]))
                {
                    return offset + 3 < line.Length && IsAsciiOctalDigit(line[offset + 3]) ? offset + 4 : offset + 3;
                }

                return offset + 2;

            default:
                return offset;
        }
    }

    #endregion

    #region Whitespace and identifiers

    private static SyntaxRuleMatch MatchSpaces(ReadOnlySpan<char> line, int offset)
    {
        var end = offset;

        while (end < line.Length && char.IsWhiteSpace(line[end]))
        {
            end++;
        }

        return end == offset ? SyntaxRuleMatch.None : new SyntaxRuleMatch(end - offset, []);
    }

    private static SyntaxRuleMatch MatchIdentifier(ReadOnlySpan<char> line, int offset)
    {
        if (offset >= line.Length)
        {
            return SyntaxRuleMatch.None;
        }

        if (line[offset] == '_')
        {
            return MatchIdentifierContinuation(line, offset, 1);
        }

        _ = Rune.DecodeFromUtf16(line[offset..], out var first, out var firstLength);

        return Rune.IsLetter(first)
            ? MatchIdentifierContinuation(line, offset, firstLength)
            : SyntaxRuleMatch.None;
    }

    private static SyntaxRuleMatch MatchIdentifierContinuation(ReadOnlySpan<char> line, int offset, int firstLength)
    {
        var end = offset + firstLength;

        while (end < line.Length)
        {
            if (line[end] == '_')
            {
                end++;
                continue;
            }

            _ = Rune.DecodeFromUtf16(line[end..], out var rune, out var runeLength);
            var category = Rune.GetUnicodeCategory(rune);

            if (!Rune.IsLetter(rune) &&
                category is not UnicodeCategory.DecimalDigitNumber and
                not UnicodeCategory.LetterNumber and
                not UnicodeCategory.OtherNumber)
            {
                break;
            }

            end += runeLength;
        }

        return new SyntaxRuleMatch(end - offset, []);
    }

    #endregion

    #region Keywords and regular expressions

    private SyntaxRuleMatch MatchKeyword(ReadOnlySpan<char> line, int offset)
    {
        Debug.Assert(_keywordMatcher is not null, "A Keyword rule must carry a compiled matcher.");

        var (length, skipOffset) = _keywordMatcher.MatchWithSkip(line, offset);
        return length == 0 ? new SyntaxRuleMatch(skipOffset) : new SyntaxRuleMatch(length, []);
    }

    private SyntaxRuleMatch MatchRegularExpression(string line, int offset, IReadOnlyList<string> captures)
    {
        Debug.Assert(Source.Text is not null, "The reader requires a RegExpr rule's String attribute.");
        Debug.Assert(
            Source.Dynamic ? _dynamicRegexCache is not null : _staticRegex is not null,
            "The constructor allocates exactly one of the two caches, matching Source.Dynamic.");

        var regex = Source.Dynamic
            ? _dynamicRegexCache!.GetOrAdd(
                ReplaceCaptures(Source.Text ?? string.Empty, captures, escapeForRegex: true),
                pattern => CompileRegex(pattern, Source))
            : _staticRegex!;

        PcreMatch match;

        try
        {
            match = SyntaxRegularExpression.Match(regex, line, offset);
        }
        catch (PcreMatchException error) when (SyntaxRegularExpression.IsBudgetExceeded(error))
        {
            // The negative skip offset suppresses this effective rule for the rest of the line.
            // Retrying the same exhausted search at every following UTF-16 offset would multiply
            // the bounded PCRE2 work into an unbounded line-level delay.
            return new SyntaxRuleMatch(skipOffset: -1);
        }

        if (!match.Success)
        {
            // .NET's non-anchored Match(line, offset) already searched every remaining position
            // in the line for this pattern and found none, so this rule cannot possibly match
            // again anywhere on the current line either - report the strongest possible skip hint.
            return new SyntaxRuleMatch(skipOffset: -1);
        }

        if (match.Index != offset)
        {
            // The pattern's next possible match starts at match.Index, not here - the same
            // non-anchored search already tells us there is no point retrying this rule at any
            // offset before that.
            return new SyntaxRuleMatch(skipOffset: match.Index);
        }

        if (!_capturesRequired || match.Groups.Count <= 1)
        {
            return new SyntaxRuleMatch(match.Length, []);
        }

        var captured = new string[Math.Min(9, match.Groups.Count - 1)];

        for (var i = 0; i < captured.Length; i++)
        {
            var group = match.Groups[i + 1];
            captured[i] = group.Success ? group.Value : string.Empty;
        }

        return new SyntaxRuleMatch(match.Length, captured);
    }

    private static PcreRegex CompileRegex(string pattern, SyntaxRule source) =>
        SyntaxRegularExpression.Compile(pattern, source.Insensitive, source.Minimal);

    private static PcreRegex CompileRegex(string pattern, SyntaxRule source, out bool isValid) =>
        SyntaxRegularExpression.Compile(pattern, source.Insensitive, source.Minimal, out isValid);

    private static string ReplaceCaptures(string pattern, IReadOnlyList<string> captures, bool escapeForRegex)
    {
        if (captures.Count == 0 || !pattern.Contains('%'))
        {
            return pattern;
        }

        var result = pattern;

        for (var i = Math.Min(9, captures.Count); i >= 1; i--)
        {
            var replacement = escapeForRegex ? Regex.Escape(captures[i - 1]) : captures[i - 1];
            result = result.Replace($"%{i}", replacement, StringComparison.Ordinal);
        }

        return result;
    }

    #endregion
}
