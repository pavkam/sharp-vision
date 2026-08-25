// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Documents.Markdown;

using SharpVision.Controls.Documents;
using SharpVision.Controls.Input;
using SharpVision.Text;

/// <summary>Parses CommonMark-shaped Markdown plus explicitly enabled GFM and Obsidian extensions.</summary>
/// <remarks>The reader produces detached nodes and never mutates a control. Parsing is deterministic,
/// culture-independent, and bounded by <see cref="DocumentReadOptions.MaximumCharacters"/>.</remarks>
[PublicAPI]
public sealed class MarkdownDocumentReader: IDocumentFormatReader
{
    private const int _maximumBlockQuoteDepth = 64;
    private readonly MarkdownExtension _extensions;

    /// <summary>Initializes a baseline CommonMark reader.</summary>
    public MarkdownDocumentReader()
    {
    }

    /// <summary>Initializes a reader from non-null options copied at construction.</summary>
    /// <param name="options">The non-null options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public MarkdownDocumentReader(MarkdownOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _extensions = options.Extensions;
    }

    /// <inheritdoc/>
    public DocumentReadResult Read(string source, DocumentReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new DocumentReadOptions();

        if (source.Length > options.MaximumCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(source),
                source.Length,
                "The document exceeds the configured maximum character count.");
        }

        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var radioGroupOrdinal = 0;
        var blocks = ParseBlocks(lines, ref radioGroupOrdinal, quoteDepth: 0);
        return new DocumentReadResult(blocks);
    }

    private List<DocumentBlock> ParseBlocks(string[] lines, ref int radioGroupOrdinal, int quoteDepth)
    {
        var blocks = new List<DocumentBlock>();
        var index = 0;

        while (index < lines.Length)
        {
            var line = lines[index];

            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            if (index + 1 < lines.Length && TrySetextUnderline(lines[index + 1], out var setextLevel) &&
                !IsBlockStart(line))
            {
                var setextHeading = new DocumentHeading(setextLevel);
                ParseInlines(line.Trim(), setextHeading.Inlines);
                blocks.Add(setextHeading);
                index += 2;
                continue;
            }

            if (TryFence(lines, ref index, out var code))
            {
                blocks.Add(code);
                continue;
            }

            if (TryHeading(line, out var heading))
            {
                blocks.Add(heading);
                index++;
                continue;
            }

            if (IsRule(line))
            {
                blocks.Add(new DocumentSeparator());
                index++;
                continue;
            }

            if (line.TrimStart().StartsWith('>'))
            {
                blocks.Add(ParseQuote(lines, ref index, ref radioGroupOrdinal, quoteDepth));
                continue;
            }

            if (Has(MarkdownExtension.Tables) && index + 1 < lines.Length &&
                lines[index].Contains('|') && lines[index + 1].Contains('|') &&
                TryTableAlignments(lines[index + 1], out var alignments) &&
                SplitTableRow(lines[index]).Count == alignments.Count)
            {
                blocks.Add(ParseTable(lines, ref index, alignments));
                continue;
            }

            if (TryListMarker(line, out _))
            {
                blocks.Add(ParseList(lines, ref index, ref radioGroupOrdinal, quoteDepth));
                continue;
            }

            var paragraphLines = new List<string>();

            while (index < lines.Length && !string.IsNullOrWhiteSpace(lines[index]))
            {
                if (paragraphLines.Count > 0 && IsBlockStart(lines[index]))
                {
                    break;
                }

                paragraphLines.Add(lines[index]);
                index++;
            }

            blocks.Add(CreateParagraph(paragraphLines));
        }

        return blocks;
    }

    private static bool TryFence(string[] lines, ref int index, [NotNullWhen(true)] out DocumentCodeBlock? code)
    {
        if (!TryFenceOpener(lines[index], out var fence))
        {
            code = null;
            return false;
        }

        index++;
        var body = new StringBuilder();

        while (index < lines.Length && !IsFenceCloser(lines[index], fence))
        {
            if (body.Length > 0)
            {
                _ = body.Append('\n');
            }

            var line = lines[index];
            var removableIndent = Math.Min(fence.Indent, CountLeadingSpaces(line));
            _ = body.Append(line.AsSpan(removableIndent));
            index++;
        }

        if (index < lines.Length)
        {
            index++;
        }

        code = new DocumentCodeBlock(body.ToString()) { Language = fence.Info };
        return true;
    }

    [Pure]
    private static bool TryFenceOpener(string source, out MarkdownFence fence)
    {
        var indent = CountLeadingSpaces(source);

        if (indent > 3 || indent >= source.Length || source[indent] is not ('`' or '~'))
        {
            fence = default;
            return false;
        }

        var marker = source[indent];
        var length = CountRun(source, indent, marker);

        if (length < 3)
        {
            fence = default;
            return false;
        }

        var info = source[(indent + length)..].Trim();

        if (marker == '`' && info.Contains('`'))
        {
            fence = default;
            return false;
        }

        fence = new MarkdownFence(marker, length, indent, info);
        return true;
    }

    [Pure]
    private static bool IsFenceCloser(string source, MarkdownFence fence)
    {
        var indent = CountLeadingSpaces(source);

        if (indent > 3 || indent >= source.Length || source[indent] != fence.Marker)
        {
            return false;
        }

        var length = CountRun(source, indent, fence.Marker);
        return length >= fence.Length && source.AsSpan(indent + length).Trim().IsEmpty;
    }

    private bool TryHeading(string line, [NotNullWhen(true)] out DocumentHeading? heading)
    {
        var indent = CountLeadingSpaces(line);

        if (indent > 3)
        {
            heading = null;
            return false;
        }

        var trimmed = line[indent..];
        var level = 0;

        while (level < trimmed.Length && level < 6 && trimmed[level] == '#')
        {
            level++;
        }

        if (level == 0 || (level < trimmed.Length && trimmed[level] is not (' ' or '\t')))
        {
            heading = null;
            return false;
        }

        var content = level == trimmed.Length ? string.Empty : trimmed[(level + 1)..];
        var end = content.Length;

        while (end > 0 && content[end - 1] is ' ' or '\t')
        {
            end--;
        }

        var hashesStart = end;

        while (hashesStart > 0 && content[hashesStart - 1] == '#')
        {
            hashesStart--;
        }

        if (hashesStart < end && hashesStart > 0 && content[hashesStart - 1] is ' ' or '\t')
        {
            end = hashesStart - 1;

            while (end > 0 && content[end - 1] is ' ' or '\t')
            {
                end--;
            }
        }

        heading = new DocumentHeading(level);
        ParseInlines(content[..end], heading.Inlines);
        return true;
    }

    private DocumentBlock ParseQuote(
        string[] lines,
        ref int index,
        ref int radioGroupOrdinal,
        int quoteDepth)
    {
        var quoted = new List<string>();

        while (index < lines.Length && lines[index].TrimStart().StartsWith('>'))
        {
            var value = lines[index].TrimStart()[1..];
            quoted.Add(value.StartsWith(' ') ? value[1..] : value);
            index++;
        }

        if (quoteDepth + 1 < _maximumBlockQuoteDepth && Has(MarkdownExtension.Callouts) &&
            TryCalloutHeader(quoted[0], out var kind, out var title))
        {
            quoted.RemoveAt(0);
            var callout = new DocumentCallout { Kind = kind, Title = title };

            foreach (var block in ParseBlocks([.. quoted], ref radioGroupOrdinal, quoteDepth + 1))
            {
                callout.Blocks.Add(block);
            }

            return callout;
        }

        var quote = new DocumentBlockQuote();

        if (quoteDepth + 1 >= _maximumBlockQuoteDepth)
        {
            quote.Blocks.Add(CreateParagraph(quoted));
        }
        else
        {
            foreach (var block in ParseBlocks([.. quoted], ref radioGroupOrdinal, quoteDepth + 1))
            {
                quote.Blocks.Add(block);
            }
        }

        return quote;
    }

    private DocumentList ParseList(
        string[] lines,
        ref int index,
        ref int radioGroupOrdinal,
        int quoteDepth)
    {
        _ = TryListMarker(lines[index], out var firstMarker);
        var list = new DocumentList(firstMarker.IsOrdered ? DocumentListKind.Numbered : DocumentListKind.Bulleted)
        {
            Start = firstMarker.Start
        };
        var radioGroup = Has(MarkdownExtension.RadioLists)
            ? FormattableString.Invariant($"markdown-radio-{radioGroupOrdinal++}")
            : null;
        RadioButton? selectedRadio = null;

        while (index < lines.Length && TryListMarker(lines[index], out var marker) &&
               marker.Indent == firstMarker.Indent && marker.IsOrdered == firstMarker.IsOrdered)
        {
            var item = new DocumentListItem();
            var continuation = new List<string>();
            var endListAfterItem = false;
            index++;

            while (index < lines.Length)
            {
                if (TryListMarker(lines[index], out var next) && next.Indent == firstMarker.Indent)
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(lines[index]) && CountLeadingSpaces(lines[index]) <= firstMarker.Indent)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(lines[index]))
                {
                    if (index + 1 < lines.Length && TryListMarker(lines[index + 1], out var afterBlank) &&
                        afterBlank.Indent == firstMarker.Indent)
                    {
                        index++;

                        if (ContinuesSameSemanticList(marker.Content, afterBlank.Content))
                        {
                            list.IsLoose = true;
                        }
                        else
                        {
                            endListAfterItem = true;
                        }

                        break;
                    }

                    list.IsLoose = true;
                    continuation.Add(string.Empty);
                }
                else
                {
                    var remove = Math.Min(firstMarker.Indent + firstMarker.MarkerWidth, CountLeadingSpaces(lines[index]));
                    continuation.Add(lines[index][remove..]);
                }

                index++;
            }

            if (Has(MarkdownExtension.TaskLists) && TryTask(marker.Content, out var isChecked, out var taskText))
            {
                item.Blocks.Add(new DocumentBlockControl(new CheckBox(taskText) { IsChecked = isChecked }));
            }
            else if (radioGroup is not null && TryRadio(marker.Content, out isChecked, out var radioText))
            {
                if (isChecked && selectedRadio is not null)
                {
                    selectedRadio.IsChecked = false;
                }

                var radio = new RadioButton(radioText)
                {
                    GroupName = radioGroup,
                    IsChecked = isChecked
                };
                MarkdownRadioRegistry.Register(radio);
                item.Blocks.Add(new DocumentBlockControl(radio));

                if (isChecked)
                {
                    selectedRadio = radio;
                }
            }
            else
            {
                continuation.Insert(0, marker.Content);
            }

            foreach (var block in ParseBlocks([.. continuation], ref radioGroupOrdinal, quoteDepth))
            {
                item.Blocks.Add(block);
            }

            list.Items.Add(item);

            if (endListAfterItem)
            {
                break;
            }

        }

        return list;
    }

    private bool ContinuesSameSemanticList(string current, string next)
    {
        var currentTask = Has(MarkdownExtension.TaskLists) && TryTask(current, out _, out _);
        var nextTask = Has(MarkdownExtension.TaskLists) && TryTask(next, out _, out _);
        var currentRadio = Has(MarkdownExtension.RadioLists) && TryRadio(current, out _, out _);
        var nextRadio = Has(MarkdownExtension.RadioLists) && TryRadio(next, out _, out _);
        return currentTask == nextTask && currentRadio == nextRadio;
    }

    private DocumentTable ParseTable(
        string[] lines,
        ref int index,
        IReadOnlyList<DocumentTableCellAlignment> alignments)
    {
        var table = new DocumentTable();
        var header = CreateTableRow(SplitTableRow(lines[index]), alignments, isHeader: true);
        table.Rows.Add(header);
        index += 2;

        while (index < lines.Length && !string.IsNullOrWhiteSpace(lines[index]) && lines[index].Contains('|'))
        {
            table.Rows.Add(CreateTableRow(SplitTableRow(lines[index]), alignments, isHeader: false));
            index++;
        }

        return table;
    }

    private DocumentTableRow CreateTableRow(
        List<string> values,
        IReadOnlyList<DocumentTableCellAlignment> alignments,
        bool isHeader)
    {
        var row = new DocumentTableRow { IsHeader = isHeader };

        for (var column = 0; column < alignments.Count; column++)
        {
            var cell = new DocumentTableCell { Alignment = alignments[column] };

            if (column < values.Count)
            {
                ParseInlines(values[column], cell.Inlines);
            }

            row.Cells.Add(cell);
        }

        return row;
    }

    private DocumentParagraph CreateParagraph(List<string> lines)
    {
        var paragraph = new DocumentParagraph();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var hasFollowingLine = index + 1 < lines.Count;
            var spaceBreak = hasFollowingLine && line.EndsWith("  ", StringComparison.Ordinal);
            var slashBreak = hasFollowingLine && EndsWithUnescapedBackslash(line);

            if (spaceBreak)
            {
                line = line.TrimEnd(' ');
            }
            else if (slashBreak)
            {
                line = line[..^1];
            }

            ParseInlines(line, paragraph.Inlines);

            if (hasFollowingLine)
            {
                paragraph.Inlines.Add(spaceBreak || slashBreak
                    ? new DocumentLineBreak()
                    : new DocumentSoftBreak());
            }
        }

        return paragraph;
    }

    private void ParseInlines(string source, DocumentInlineCollection destination, bool insideLink = false)
    {
        var index = 0;
        var plain = new StringBuilder();
        var linkCloserUnavailable = false;
        var wikiCloserUnavailable = false;

        void Flush()
        {
            if (plain.Length > 0)
            {
                destination.Add(new DocumentTextRun(TextMarkup.Escape(plain.ToString())));
                _ = plain.Clear();
            }
        }

        while (index < source.Length)
        {
            if (source[index] == '\\' && index + 1 < source.Length && IsEscapablePunctuation(source[index + 1]))
            {
                _ = plain.Append(source[index + 1]);
                index += 2;
                continue;
            }

            if (!insideLink && source[index] == '<' && TryAngleAutolink(source, index, out var angleEnd, out var angleTarget))
            {
                Flush();
                destination.Add(new DocumentLink(angleTarget, angleTarget));
                index = angleEnd;
                continue;
            }

            if (!insideLink && Has(MarkdownExtension.Autolinks) && TryExtendedAutolink(source, index, out var urlEnd, out var url))
            {
                Flush();
                destination.Add(new DocumentLink(url, url));
                index = urlEnd;
                continue;
            }

            if (!insideLink && !wikiCloserUnavailable && Has(MarkdownExtension.WikiLinks) &&
                TryWikiLink(source, index, out var wikiEnd, out var wikiTarget, out var wikiLabel))
            {
                Flush();
                destination.Add(new DocumentLink(wikiLabel, wikiTarget));
                index = wikiEnd;
                continue;
            }

            if (!wikiCloserUnavailable && Has(MarkdownExtension.WikiLinks) &&
                source.AsSpan(index).StartsWith("[[", StringComparison.Ordinal) &&
                source.IndexOf("]]", index + 2, StringComparison.Ordinal) < 0)
            {
                wikiCloserUnavailable = true;
            }

            if (TryDelimited(source, index, "***", out var combinedEnd))
            {
                Flush();
                var strong = new DocumentStrong();
                var emphasis = new DocumentEmphasis();
                ParseInlines(source[(index + 3)..combinedEnd], emphasis.Inlines, insideLink);
                strong.Inlines.Add(emphasis);
                destination.Add(strong);
                index = combinedEnd + 3;
                continue;
            }

            if (TryDelimited(source, index, "**", out var strongEnd))
            {
                Flush();
                var strong = new DocumentStrong();
                ParseInlines(source[(index + 2)..strongEnd], strong.Inlines, insideLink);
                destination.Add(strong);
                index = strongEnd + 2;
                continue;
            }

            if (TryDelimited(source, index, "___", out var combinedUnderscoreEnd))
            {
                Flush();
                var strongUnderscore = new DocumentStrong();
                var emphasisUnderscore = new DocumentEmphasis();
                ParseInlines(source[(index + 3)..combinedUnderscoreEnd], emphasisUnderscore.Inlines, insideLink);
                strongUnderscore.Inlines.Add(emphasisUnderscore);
                destination.Add(strongUnderscore);
                index = combinedUnderscoreEnd + 3;
                continue;
            }

            if (TryDelimited(source, index, "__", out var strongUnderscoreEnd))
            {
                Flush();
                var strongUnderscoreOnly = new DocumentStrong();
                ParseInlines(source[(index + 2)..strongUnderscoreEnd], strongUnderscoreOnly.Inlines, insideLink);
                destination.Add(strongUnderscoreOnly);
                index = strongUnderscoreEnd + 2;
                continue;
            }

            if (Has(MarkdownExtension.Strikethrough) && TryDelimited(source, index, "~~", out var strikeEnd))
            {
                Flush();
                var strike = new DocumentStrikethrough();
                ParseInlines(source[(index + 2)..strikeEnd], strike.Inlines, insideLink);
                destination.Add(strike);
                index = strikeEnd + 2;
                continue;
            }

            if (source[index] is '*' or '_' && CanOpenEmphasis(source, index) &&
                FindUnescaped(source, source[index].ToString(), index + 1) is var emphasisEnd &&
                emphasisEnd > index + 1)
            {
                Flush();
                var emphasis = new DocumentEmphasis();
                ParseInlines(source[(index + 1)..emphasisEnd], emphasis.Inlines, insideLink);
                destination.Add(emphasis);
                index = emphasisEnd + 1;
                continue;
            }

            if (source[index] == '`')
            {
                var delimiterLength = CountRun(source, index, '`');

                if (FindCodeSpanEnd(source, index + delimiterLength, delimiterLength) is var codeEnd && codeEnd >= 0)
                {
                    Flush();
                    destination.Add(new DocumentCodeSpan(
                        NormalizeCodeSpan(source[(index + delimiterLength)..codeEnd])));
                    index = codeEnd + delimiterLength;
                    continue;
                }
            }

            if (!linkCloserUnavailable && source[index] == '[' &&
                source.IndexOf("](", index + 1, StringComparison.Ordinal) < 0)
            {
                linkCloserUnavailable = true;
            }

            if (!insideLink && !linkCloserUnavailable && source[index] == '[' &&
                TryLink(source, index, out var linkEnd, out var label, out var target))
            {
                Flush();
                var link = new DocumentLink { Target = target.Length == 0 ? null : target };

                // CommonMark forbids a link from containing another link at any nesting depth: a
                // label whose own content would otherwise resolve to a link (a literal reference
                // marker is fine and stays plain text either way, but a genuinely link-shaped
                // sequence such as another "[x](y)" or an autolink is not) instead leaves that
                // content as ordinary literal text. insideLink propagates through every recursive
                // call this label's own content can reach - emphasis, strong, strikethrough - so a
                // link-shaped sequence nested arbitrarily deep inside the label still degrades to
                // text instead of throwing when the model's own "no nested links" rule rejects it.
                ParseInlines(label, link.Inlines, insideLink: true);
                destination.Add(link);
                index = linkEnd;
                continue;
            }

            _ = plain.Append(source[index]);
            index++;
        }

        Flush();
    }

    [Pure]
    private bool Has(MarkdownExtension extension) => (_extensions & extension) != 0;

    [Pure]
    private static bool TryDelimited(string source, int index, string delimiter, out int end)
    {
        if (!source.AsSpan(index).StartsWith(delimiter, StringComparison.Ordinal))
        {
            end = -1;
            return false;
        }

        end = FindUnescaped(source, delimiter, index + delimiter.Length);
        return end > index + delimiter.Length;
    }

    [Pure]
    private static bool TryLink(string source, int index, out int end, out string label, out string target)
    {
        var closeLabel = FindLabelClose(source, index);

        if (closeLabel < 0 || closeLabel + 1 >= source.Length || source[closeLabel + 1] != '(')
        {
            end = -1;
            label = string.Empty;
            target = string.Empty;
            return false;
        }

        var depth = 1;
        var cursor = closeLabel + 2;

        while (cursor < source.Length && depth > 0)
        {
            if (source[cursor] == '\\' && cursor + 1 < source.Length)
            {
                cursor += 2;
                continue;
            }

            if (source[cursor] == '(')
            {
                depth++;
            }
            else if (source[cursor] == ')')
            {
                depth--;
            }

            cursor++;
        }

        if (depth != 0)
        {
            end = -1;
            label = string.Empty;
            target = string.Empty;
            return false;
        }

        label = source[(index + 1)..closeLabel];
        target = UnescapePunctuation(source[(closeLabel + 2)..(cursor - 1)]);
        end = cursor;
        return true;
    }

    /// <summary>
    /// Finds the label-closing <c>]</c> for a link opened at <paramref name="index"/>, tracking
    /// bracket nesting depth rather than accepting the first unescaped <c>]</c> regardless of
    /// context. A label may legitimately contain its own balanced <c>[...]</c> - a literal
    /// reference marker ("[See [1]](url)") or a nested image ("[![alt](img.png)](url)", the common
    /// "linked image" pattern - and the label's true end is the <c>]</c> that returns nesting back
    /// to zero, not whichever <c>]</c> happens to appear first.
    /// </summary>
    /// <param name="source">The complete inline source.</param>
    /// <param name="index">The zero-based offset of the opening <c>[</c>.</param>
    /// <returns>The zero-based offset of the matching <c>]</c>, or -1 when the brackets never balance.</returns>
    [Pure]
    private static int FindLabelClose(string source, int index)
    {
        var depth = 1;
        var cursor = index + 1;

        while (cursor < source.Length)
        {
            if (source[cursor] == '\\' && cursor + 1 < source.Length)
            {
                cursor += 2;
                continue;
            }

            if (source[cursor] == '[')
            {
                depth++;
            }
            else if (source[cursor] == ']')
            {
                depth--;

                if (depth == 0)
                {
                    return cursor;
                }
            }

            cursor++;
        }

        return -1;
    }

    [Pure]
    private static bool TryWikiLink(
        string source,
        int index,
        out int end,
        out string target,
        out string label)
    {
        if (!source.AsSpan(index).StartsWith("[[", StringComparison.Ordinal))
        {
            end = -1;
            target = string.Empty;
            label = string.Empty;
            return false;
        }

        var close = source.IndexOf("]]", index + 2, StringComparison.Ordinal);

        if (close < 0)
        {
            end = -1;
            target = string.Empty;
            label = string.Empty;
            return false;
        }

        var payload = source[(index + 2)..close];
        var separator = FindUnescaped(payload, "|", 0);
        target = UnescapePunctuation(separator < 0 ? payload : payload[..separator]);
        label = UnescapePunctuation(separator < 0 ? payload : payload[(separator + 1)..]);
        end = close + 2;
        return target.Length > 0;
    }

    [Pure]
    private static int FindUnescaped(string source, string value, int start)
    {
        var index = start;

        while (index <= source.Length - value.Length)
        {
            var found = source.IndexOf(value, index, StringComparison.Ordinal);

            if (found < 0)
            {
                return -1;
            }

            var slashes = 0;

            for (var cursor = found - 1; cursor >= 0 && source[cursor] == '\\'; cursor--)
            {
                slashes++;
            }

            if ((slashes & 1) == 0)
            {
                return found;
            }

            index = found + value.Length;
        }

        return -1;
    }

    [Pure]
    private static int FindCodeSpanEnd(string source, int start, int delimiterLength)
    {
        var index = start;

        while (index < source.Length)
        {
            if (source[index] != '`')
            {
                index++;
                continue;
            }

            var length = CountRun(source, index, '`');

            if (length == delimiterLength)
            {
                return index;
            }

            index += length;
        }

        return -1;
    }

    [Pure]
    private static string NormalizeCodeSpan(string source)
    {
        var normalized = source.Replace('\n', ' ').Replace('\r', ' ');

        return normalized.Length >= 2 && normalized[0] == ' ' && normalized[^1] == ' ' &&
            normalized.Any(static character => character != ' ')
            ? normalized[1..^1]
            : normalized;
    }

    [Pure]
    private static bool CanOpenEmphasis(string source, int index)
    {
        return index + 1 < source.Length && !char.IsWhiteSpace(source[index + 1]) &&
            (source[index] != '_' || index == 0 ||
             !char.IsLetterOrDigit(source[index - 1]) || !char.IsLetterOrDigit(source[index + 1]));
    }

    [Pure]
    private static bool IsEscapablePunctuation(char value) =>
        value is '!' or '"' or '#' or '$' or '%' or '&' or '\'' or '(' or ')' or '*' or '+' or ',' or '-' or '.' or
            '/' or ':' or ';' or '<' or '=' or '>' or '?' or '@' or '[' or '\\' or ']' or '^' or '_' or '`' or '{' or
            '|' or '}' or '~';

    [Pure]
    private static string UnescapePunctuation(string source)
    {
        var result = new StringBuilder(source.Length);

        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == '\\' && index + 1 < source.Length && IsEscapablePunctuation(source[index + 1]))
            {
                index++;
            }

            _ = result.Append(source[index]);
        }

        return result.ToString();
    }

    [Pure]
    private static bool TryAngleAutolink(string source, int index, out int end, out string target)
    {
        var close = source.IndexOf('>', index + 1);

        if (close > index + 1)
        {
            var candidate = source[(index + 1)..close];

            if ((candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                 candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 candidate.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) &&
                !candidate.Any(char.IsWhiteSpace))
            {
                target = candidate;
                end = close + 1;
                return true;
            }
        }

        target = string.Empty;
        end = -1;
        return false;
    }

    [Pure]
    private static bool TryExtendedAutolink(string source, int index, out int end, out string target)
    {
        if (index > 0 && !char.IsWhiteSpace(source[index - 1]) && source[index - 1] != '(')
        {
            end = -1;
            target = string.Empty;
            return false;
        }

        var remainder = source.AsSpan(index);

        if (!remainder.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !remainder.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            end = -1;
            target = string.Empty;
            return false;
        }

        var length = 0;

        while (length < remainder.Length && !char.IsWhiteSpace(remainder[length]) && remainder[length] is not ('<' or '>'))
        {
            length++;
        }

        while (length > 0 && remainder[length - 1] is '.' or ',' or ';' or ':' or '!' or '?')
        {
            length--;
        }

        while (length > 0 && remainder[length - 1] == ')' &&
               CountCharacter(remainder[..length], ')') > CountCharacter(remainder[..length], '('))
        {
            length--;
        }

        while (length > 0 && remainder[length - 1] == ']' &&
               CountCharacter(remainder[..length], ']') > CountCharacter(remainder[..length], '['))
        {
            length--;
        }

        target = remainder[..length].ToString();
        end = index + length;
        return length > 0;
    }

    [Pure]
    private static bool TryListMarker(string line, out MarkdownListMarker marker)
    {
        var indent = CountLeadingSpaces(line);

        if (indent > 3)
        {
            marker = default;
            return false;
        }

        var trimmed = line[indent..];

        if (trimmed.Length >= 2 && trimmed[0] is '-' or '+' or '*' && trimmed[1] == ' ')
        {
            marker = new MarkdownListMarker(indent, isOrdered: false, start: 1, markerWidth: 2, trimmed[2..]);
            return true;
        }

        var digits = 0;

        while (digits < trimmed.Length && char.IsAsciiDigit(trimmed[digits]))
        {
            digits++;
        }

        if (digits > 0 && digits + 1 < trimmed.Length && trimmed[digits] is '.' or ')' && trimmed[digits + 1] == ' ' &&
            int.TryParse(trimmed.AsSpan(0, digits), CultureInfo.InvariantCulture, out var start))
        {
            marker = new MarkdownListMarker(indent, isOrdered: true, start, markerWidth: digits + 2, trimmed[(digits + 2)..]);
            return true;
        }

        marker = default;
        return false;
    }

    [Pure]
    private static bool TryTask(string source, out bool isChecked, out string text)
    {
        if (source.Length >= 4 && source[0] == '[' && source[2] == ']' && source[3] == ' ' &&
            source[1] is ' ' or 'x' or 'X')
        {
            isChecked = source[1] is 'x' or 'X';
            text = source[4..];
            return true;
        }

        isChecked = false;
        text = string.Empty;
        return false;
    }

    [Pure]
    private static bool TryRadio(string source, out bool isChecked, out string text)
    {
        if (source.Length >= 4 && source[0] == '(' && source[2] == ')' && source[3] == ' ' &&
            source[1] is ' ' or 'x' or 'X')
        {
            isChecked = source[1] is 'x' or 'X';
            text = source[4..];
            return true;
        }

        isChecked = false;
        text = string.Empty;
        return false;
    }

    [Pure]
    private static bool TryTableAlignments(
        string source,
        [NotNullWhen(true)] out IReadOnlyList<DocumentTableCellAlignment>? alignments)
    {
        var cells = SplitTableRow(source);

        if (cells.Count == 0)
        {
            alignments = null;
            return false;
        }

        var result = new List<DocumentTableCellAlignment>(cells.Count);

        foreach (var cell in cells)
        {
            var value = cell.Trim();
            var leading = value.StartsWith(':');
            var trailing = value.EndsWith(':');
            var hyphens = value.Trim(':');

            if (hyphens.Length < 3 || hyphens.Any(static character => character != '-'))
            {
                alignments = null;
                return false;
            }

            result.Add((leading, trailing) switch
            {
                (true, true) => DocumentTableCellAlignment.Center,
                (false, true) => DocumentTableCellAlignment.Right,
                _ => DocumentTableCellAlignment.Left
            });
        }

        alignments = result;
        return true;
    }

    [Pure]
    private static List<string> SplitTableRow(string source)
    {
        var trimmed = source.Trim();

        if (trimmed.StartsWith('|'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith('|') && !EndsWithEscapedCharacter(trimmed, '|'))
        {
            trimmed = trimmed[..^1];
        }

        var cells = new List<string>();
        var cell = new StringBuilder();
        var codeDelimiterLength = 0;

        for (var index = 0; index < trimmed.Length; index++)
        {
            if (trimmed[index] == '\\' && index + 1 < trimmed.Length)
            {
                _ = cell.Append(trimmed[index]).Append(trimmed[index + 1]);
                index++;
                continue;
            }

            if (trimmed[index] == '`')
            {
                var run = CountRun(trimmed, index, '`');

                if (codeDelimiterLength == 0)
                {
                    codeDelimiterLength = run;
                }
                else if (run == codeDelimiterLength)
                {
                    codeDelimiterLength = 0;
                }

                _ = cell.Append(trimmed.AsSpan(index, run));
                index += run - 1;
                continue;
            }

            if (trimmed[index] == '|' && codeDelimiterLength == 0)
            {
                cells.Add(cell.ToString().Trim());
                _ = cell.Clear();
                continue;
            }

            _ = cell.Append(trimmed[index]);
        }

        cells.Add(cell.ToString().Trim());
        return cells;
    }

    [Pure]
    private static bool TryCalloutHeader(string source, out string kind, out string title)
    {
        var end = source.IndexOf(']');

        if (!source.StartsWith("[!", StringComparison.Ordinal) || end < 3)
        {
            kind = string.Empty;
            title = string.Empty;
            return false;
        }

        kind = source[2..end].ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(kind))
        {
            kind = string.Empty;
            title = string.Empty;
            return false;
        }

        title = source[(end + 1)..].Trim();
        return true;
    }

    [Pure]
    private static bool IsRule(string line)
    {
        // A thematic break, like every other block-start detector in this reader, is bounded to
        // CommonMark's 0-through-3-space indent: a line with four or more leading spaces is an
        // indented code block, not a rule. Stripping every space unconditionally before checking
        // the run - as this method used to - discarded that distinction along with the interior
        // spaces a spaced-out rule ("- - -") legitimately needs stripped.
        if (CountLeadingSpaces(line) > 3)
        {
            return false;
        }

        var compact = line.Replace(" ", string.Empty, StringComparison.Ordinal);
        return compact.Length >= 3 && compact.All(character => character == compact[0]) && compact[0] is '-' or '*' or '_';
    }

    [Pure]
    private static bool TrySetextUnderline(string source, out int level)
    {
        var indent = CountLeadingSpaces(source);

        if (indent > 3)
        {
            level = 0;
            return false;
        }

        var value = source[indent..].TrimEnd();

        if (value.Length == 0 || value.Any(character => character != value[0]) || value[0] is not ('=' or '-'))
        {
            level = 0;
            return false;
        }

        level = value[0] == '=' ? 1 : 2;
        return true;
    }

    [Pure]
    private bool IsBlockStart(string line) =>
        TryHeading(line, out _) ||
        line.TrimStart().StartsWith('>') ||
        line.TrimStart().StartsWith("```", StringComparison.Ordinal) ||
        line.TrimStart().StartsWith("~~~", StringComparison.Ordinal) ||
        TryListMarker(line, out _) ||
        IsRule(line);

    [Pure]
    private static int CountLeadingSpaces(string source)
    {
        var count = 0;

        while (count < source.Length && source[count] == ' ')
        {
            count++;
        }

        return count;
    }

    [Pure]
    private static int CountRun(string source, int start, char value)
    {
        var end = start;

        while (end < source.Length && source[end] == value)
        {
            end++;
        }

        return end - start;
    }

    [Pure]
    private static bool EndsWithUnescapedBackslash(string source)
    {
        var slashes = 0;

        for (var index = source.Length - 1; index >= 0 && source[index] == '\\'; index--)
        {
            slashes++;
        }

        return (slashes & 1) == 1;
    }

    [Pure]
    private static bool EndsWithEscapedCharacter(string source, char value)
    {
        if (source.Length == 0 || source[^1] != value)
        {
            return false;
        }

        var slashes = 0;

        for (var index = source.Length - 2; index >= 0 && source[index] == '\\'; index--)
        {
            slashes++;
        }

        return (slashes & 1) == 1;
    }

    [Pure]
    private static int CountCharacter(ReadOnlySpan<char> source, char value)
    {
        var count = 0;

        foreach (var character in source)
        {
            if (character == value)
            {
                count++;
            }
        }

        return count;
    }
}
