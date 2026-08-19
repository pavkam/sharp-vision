// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using InstantHandle = JetBrains.Annotations.InstantHandleAttribute;

/// <summary>Parses a .NET custom date/time format pattern into an ordered sequence of literal and
/// editable-token runs, and formats a value against that same pattern one segment at a time.</summary>
/// <remarks>
/// The same walk works for any pattern accepted by <see cref="DateOnly.ToString(string?, IFormatProvider?)"/>,
/// <see cref="TimeOnly.ToString(string?, IFormatProvider?)"/>, or <see cref="DateTime.ToString(string, IFormatProvider)"/>,
/// so <see cref="Controls.Input.DateInput"/>, <see cref="Controls.Input.TimeInput"/>, and
/// <see cref="Controls.Input.DateTimeInput"/> all derive their segment order, widths, separators,
/// and designator text from a single culture-driven pattern string instead of each hardcoding a
/// fixed field order.
/// </remarks>
internal static class TemporalPatternSegmenter
{
    /// <summary>Parses a pattern into literal and editable-token runs.</summary>
    /// <param name="pattern">The custom format pattern, such as a culture's <c>ShortDatePattern</c> or <c>ShortTimePattern</c>.</param>
    /// <param name="tokenKinds">Maps each recognized pattern letter (for example 'M', 'd', 'y', 'H', 'h', 'm', 's', 't') to the segment kind it produces.</param>
    /// <param name="culture">Supplies <see cref="DateTimeFormatInfo.DateSeparator"/> and <see cref="DateTimeFormatInfo.TimeSeparator"/> for unquoted '/' and ':' literals.</param>
    /// <returns>The pattern's literal and editable runs, in left-to-right order.</returns>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    [Pure]
    public static IReadOnlyList<PatternSegment> ParseTokens(
        string pattern,
        IReadOnlyDictionary<char, TemporalSegmentKind> tokenKinds,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(tokenKinds);
        ArgumentNullException.ThrowIfNull(culture);

        var segments = new List<PatternSegment>();
        var index = 0;

        while (index < pattern.Length)
        {
            var ch = pattern[index];

            if (ch is '\'' or '"')
            {
                var quote = ch;
                var literal = new StringBuilder();
                index++;

                while (index < pattern.Length && pattern[index] != quote)
                {
                    if (pattern[index] == '\\' && index + 1 < pattern.Length)
                    {
                        index++;
                    }

                    _ = literal.Append(pattern[index]);
                    index++;
                }

                if (index < pattern.Length)
                {
                    index++;
                }

                AppendLiteral(segments, literal.ToString());
                continue;
            }

            if (ch == '\\' && index + 1 < pattern.Length)
            {
                AppendLiteral(segments, pattern[index + 1].ToString());
                index += 2;
                continue;
            }

            if (ch == '%')
            {
                index++;

                if (index >= pattern.Length)
                {
                    break;
                }

                ch = pattern[index];
            }

            if (tokenKinds.TryGetValue(ch, out var kind))
            {
                var count = 0;

                while (index < pattern.Length && pattern[index] == ch)
                {
                    count++;
                    index++;
                }

                segments.Add(new PatternSegment(kind, count));
                continue;
            }

            AppendLiteral(
                segments,
                ch switch
                {
                    '/' => culture.DateTimeFormat.DateSeparator,
                    ':' => culture.DateTimeFormat.TimeSeparator,
                    _ => ch.ToString()
                });
            index++;
        }

        return segments;
    }

    /// <summary>Formats a value against a parsed pattern one segment at a time, so each editable
    /// run's rendered text can be highlighted and hit-tested independently.</summary>
    /// <param name="pattern">The same pattern previously passed to <see cref="ParseTokens"/>.</param>
    /// <param name="tokens">The result of parsing <paramref name="pattern"/> with <see cref="ParseTokens"/>.</param>
    /// <param name="tokenKinds">The same map of recognized pattern letters previously passed to <see cref="ParseTokens"/>.</param>
    /// <param name="formatter">Formats the value against an arbitrary pattern string, typically <c>value.ToString(format, culture)</c>.</param>
    /// <returns>The rendered text for each entry in <paramref name="tokens"/>, in the same order.</returns>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    /// <exception cref="InvalidOperationException">The formatted value exhausts all available marker characters.</exception>
    [Pure]
    public static IReadOnlyList<string> FormatSegments(
        string pattern,
        IReadOnlyList<PatternSegment> tokens,
        IReadOnlyDictionary<char, TemporalSegmentKind> tokenKinds,
        [InstantHandle] Func<string, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(tokenKinds);
        ArgumentNullException.ThrowIfNull(formatter);

        var unmarked = formatter(pattern);
        var startMarker = FindAvailableMarker(unmarked, excluded: default);
        var endMarker = FindAvailableMarker(unmarked, startMarker);
        var markedPattern = BuildMarkedPattern(pattern, tokenKinds, startMarker, endMarker);
        var formatted = formatter(markedPattern);
        var results = new List<string>(tokens.Count);
        var cursor = 0;

        foreach (var token in tokens)
        {
            if (token.Kind is null)
            {
                results.Add(token.LiteralText);
                continue;
            }

            var start = formatted.IndexOf(startMarker, cursor);
            Debug.Assert(start >= 0, "A marked pattern must contain a start marker for every editable run.");
            var contentStart = start + 1;
            var end = formatted.IndexOf(endMarker, contentStart);
            Debug.Assert(end >= 0, "A marked pattern must contain a matching end marker for every editable run.");
            results.Add(formatted[contentStart..end]);
            cursor = end + 1;
        }

        return results;
    }

    /// <summary>Re-walks the pattern exactly as <see cref="ParseTokens"/> does, wrapping each
    /// editable run in a pair of quoted marker literals so <see cref="FormatSegments"/> can locate
    /// its formatted text after the whole pattern is formatted in one pass.</summary>
    private static string BuildMarkedPattern(
        string pattern,
        IReadOnlyDictionary<char, TemporalSegmentKind> tokenKinds,
        char startMarker,
        char endMarker)
    {
        var marked = new StringBuilder(pattern.Length + 16);
        var index = 0;

        while (index < pattern.Length)
        {
            var ch = pattern[index];

            if (ch is '\'' or '"')
            {
                var quote = ch;
                _ = marked.Append(ch);
                index++;

                while (index < pattern.Length)
                {
                    ch = pattern[index];
                    _ = marked.Append(ch);
                    index++;

                    if (ch == '\\' && index < pattern.Length)
                    {
                        _ = marked.Append(pattern[index]);
                        index++;
                    }
                    else if (ch == quote)
                    {
                        break;
                    }
                }

                continue;
            }

            if (ch == '\\' && index + 1 < pattern.Length)
            {
                _ = marked.Append(ch).Append(pattern[index + 1]);
                index += 2;
                continue;
            }

            var percentPrefixed = ch == '%' && index + 1 < pattern.Length;
            var tokenStart = percentPrefixed ? index + 1 : index;
            var token = pattern[tokenStart];

            if (tokenKinds.ContainsKey(token))
            {
                _ = marked.Append('\'').Append(startMarker).Append('\'');

                if (percentPrefixed)
                {
                    _ = marked.Append('%');
                }

                do
                {
                    _ = marked.Append(pattern[tokenStart]);
                    tokenStart++;
                }
                while (!percentPrefixed && tokenStart < pattern.Length && pattern[tokenStart] == token);

                _ = marked.Append('\'').Append(endMarker).Append('\'');
                index = tokenStart;
                continue;
            }

            _ = marked.Append(ch);
            index++;
        }

        return marked.ToString();
    }

    private static void AppendLiteral(List<PatternSegment> segments, string literal)
    {
        if (literal.Length == 0)
        {
            return;
        }

        if (segments.Count > 0 && segments[^1].Kind is null)
        {
            var previous = segments[^1];
            segments[^1] = new PatternSegment(previous.LiteralText + literal);
            return;
        }

        segments.Add(new PatternSegment(literal));
    }

    private static char FindAvailableMarker(string formatted, char excluded)
    {
        for (var value = 1; value < char.MaxValue; value++)
        {
            var candidate = (char) value;

            if (candidate != excluded &&
                candidate is not ('\'' or '"' or '\\') &&
                !char.IsSurrogate(candidate) &&
                !formatted.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("The formatted value exhausts all available segment markers.");
    }
}
