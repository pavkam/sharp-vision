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
    private const int _maximumRecursiveBlockDepth = 64;
    private const string _nestingLimitDiagnostic =
        "Markdown block nesting exceeded the supported limit; deeper markers remain literal.";

    /// <summary>Maximum characters allowed between <c>&amp;</c> and its terminating <c>;</c> when
    /// scanning for a character reference; bounds the lookahead so a bare, unterminated ampersand
    /// never triggers an unbounded scan.</summary>
    private const int _maximumCharacterReferenceBodyLength = 12;

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
        InlineCandidateScanCount = 0;

        if (source.Length > options.MaximumCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(source),
                source.Length,
                "The document exceeds the configured maximum character count.");
        }

        var normalized = source.Replace('\0', '\ufffd')
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var radioGroupOrdinal = 0;
        var diagnostics = new List<DocumentDiagnostic>();
        var blocks = ParseBlocks(lines, ref radioGroupOrdinal, blockDepth: 0, diagnostics);
        return new DocumentReadResult(blocks, diagnostics);
    }

    /// <summary>Gets the characters examined while indexing inline delimiter candidates during
    /// the most recent read, exposing the bounded-scan invariant for hostile inputs.</summary>
    internal int InlineCandidateScanCount { get; private set; }

    private List<DocumentBlock> ParseBlocks(
        string[] lines,
        ref int radioGroupOrdinal,
        int blockDepth,
        List<DocumentDiagnostic> diagnostics)
    {
        var blocks = new List<DocumentBlock>();
        var index = 0;

        while (index < lines.Length)
        {
            var line = lines[index];

            if (IsBlankLine(line))
            {
                index++;
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

            if (TryBlockQuoteMarker(line, out _))
            {
                blocks.Add(ParseQuote(lines, ref index, ref radioGroupOrdinal, blockDepth, diagnostics));
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
                if (blockDepth >= _maximumRecursiveBlockDepth)
                {
                    AddNestingLimitDiagnostic(diagnostics);
                    blocks.Add(CreateParagraph([line]));
                    index++;
                    continue;
                }

                blocks.Add(ParseList(lines, ref index, ref radioGroupOrdinal, blockDepth, diagnostics));
                continue;
            }

            var paragraphLines = new List<string>();
            var setextLevel = 0;

            while (index < lines.Length && !IsBlankLine(lines[index]))
            {
                if (paragraphLines.Count > 0 && TrySetextUnderline(lines[index], out setextLevel))
                {
                    index++;
                    break;
                }

                if (paragraphLines.Count > 0 && IsParagraphInterruptingBlockStart(lines[index]))
                {
                    break;
                }

                paragraphLines.Add(lines[index]);
                index++;
            }

            if (setextLevel > 0)
            {
                var setextHeading = new DocumentHeading(setextLevel);
                ParseParagraphLines(paragraphLines, setextHeading.Inlines);
                blocks.Add(setextHeading);
            }
            else
            {
                blocks.Add(CreateParagraph(paragraphLines));
            }
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
        var hasBodyLine = false;

        while (index < lines.Length && !IsFenceCloser(lines[index], fence))
        {
            if (hasBodyLine)
            {
                _ = body.Append('\n');
            }

            var line = lines[index];

            // fence.Indent is a column budget from a marker whose own opening indentation is
            // guaranteed tab-free (see TryFenceOpener), so it doubles as a character count there.
            // The body line's own leading run may contain tabs; clamping against its char length
            // (rather than its expanded column count) keeps the slice inside the whitespace run -
            // an atomic-tab simplification, not full CommonMark partial-tab column splitting.
            var removableIndent = Math.Min(fence.Indent, CountLeadingIndentation(line).Length);
            _ = body.Append(line.AsSpan(removableIndent));
            hasBodyLine = true;
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
        var indent = CountLeadingIndentation(source);

        if (indent.Column > 3 || indent.Length >= source.Length || source[indent.Length] is not ('`' or '~'))
        {
            fence = default;
            return false;
        }

        var marker = source[indent.Length];
        var length = CountRun(source, indent.Length, marker);

        if (length < 3)
        {
            fence = default;
            return false;
        }

        var info = source[(indent.Length + length)..].Trim();

        if (marker == '`' && info.Contains('`'))
        {
            fence = default;
            return false;
        }

        fence = new MarkdownFence(marker, length, indent.Column, info);
        return true;
    }

    [Pure]
    private static bool IsFenceCloser(string source, MarkdownFence fence)
    {
        var indent = CountLeadingIndentation(source);

        if (indent.Column > 3 || indent.Length >= source.Length || source[indent.Length] != fence.Marker)
        {
            return false;
        }

        var length = CountRun(source, indent.Length, fence.Marker);
        return length >= fence.Length && source.AsSpan(indent.Length + length).Trim().IsEmpty;
    }

    private bool TryHeading(string line, [NotNullWhen(true)] out DocumentHeading? heading)
    {
        var indent = CountLeadingIndentation(line);

        if (indent.Column > 3)
        {
            heading = null;
            return false;
        }

        var trimmed = line[indent.Length..];
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

        var contentStart = level;

        while (contentStart < trimmed.Length && trimmed[contentStart] is ' ' or '\t')
        {
            contentStart++;
        }

        var content = trimmed[contentStart..];
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
        int blockDepth,
        List<DocumentDiagnostic> diagnostics)
    {
        var quoted = new List<string>();
        var paragraphOpen = false;

        while (index < lines.Length)
        {
            if (TryBlockQuoteMarker(lines[index], out var contentStart))
            {
                var content = lines[index][contentStart..];
                quoted.Add(content);
                paragraphOpen = !IsBlankLine(content) && !IsParagraphInterruptingBlockStart(content);
                index++;
                continue;
            }

            // CommonMark lazy continuation: a line that carries no '>' marker still belongs to
            // the quote when it continues an already-open paragraph and does not itself look like
            // the start of another block. A blank line (handled by the loop condition failing on
            // IsBlankLine below) always closes that eligibility.
            if (!paragraphOpen || IsBlankLine(lines[index]) || IsParagraphInterruptingBlockStart(lines[index]))
            {
                break;
            }

            quoted.Add(lines[index]);
            index++;
        }

        if (blockDepth + 1 < _maximumRecursiveBlockDepth && Has(MarkdownExtension.Callouts) &&
            TryCalloutHeader(quoted[0], out var kind, out var title))
        {
            quoted.RemoveAt(0);
            var callout = new DocumentCallout { Kind = kind, Title = title };

            foreach (var block in ParseBlocks([.. quoted], ref radioGroupOrdinal, blockDepth + 1, diagnostics))
            {
                callout.Blocks.Add(block);
            }

            return callout;
        }

        var quote = new DocumentBlockQuote();

        if (blockDepth + 1 >= _maximumRecursiveBlockDepth)
        {
            AddNestingLimitDiagnostic(diagnostics);
            quote.Blocks.Add(CreateParagraph(quoted));
        }
        else
        {
            foreach (var block in ParseBlocks([.. quoted], ref radioGroupOrdinal, blockDepth + 1, diagnostics))
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
        int blockDepth,
        List<DocumentDiagnostic> diagnostics)
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
               marker.Indent == firstMarker.Indent && marker.Delimiter == firstMarker.Delimiter)
        {
            var item = new DocumentListItem();
            var continuation = new List<string>();
            var endListAfterItem = false;
            var paragraphOpen = marker.Content.Length > 0 && !IsParagraphInterruptingBlockStart(marker.Content);
            index++;

            while (index < lines.Length)
            {
                if (TryListMarker(lines[index], out var next) && next.Indent == firstMarker.Indent)
                {
                    break;
                }

                // CommonMark lazy continuation: an under-indented, marker-less line still belongs
                // to the item when it continues an already-open paragraph and does not itself look
                // like the start of another block; otherwise it ends the item as before. The
                // required column for guaranteed (non-lazy) continuation is the item's content
                // column - marker.Indent + marker.MarkerWidth - not the bare marker indent: a line
                // that reaches the marker column but falls short of the content column is still
                // under-indented for this item and must not be treated as anything but a lazy-
                // continuation candidate. A line whose column reaches the content column exactly is
                // sufficiently indented and always belongs to the item as an ordinary child block,
                // regardless of paragraphOpen, so the comparison is strict: only columns strictly
                // less than the content column count as under-indented (compare CommonMark's
                // "- # Foo\n  ## Bar\n  Baz" example, where "Baz" follows a heading - not a
                // paragraph - yet still continues the item because it reaches the content column).
                // The indentation check compares columns - a tab counts toward the threshold even
                // though CountLeadingIndentation only expands it to its next 4-column stop rather
                // than splitting it (the "atomic tab" simplification).
                if (!IsBlankLine(lines[index]) &&
                    CountLeadingIndentation(lines[index]).Column < marker.Indent + marker.MarkerWidth &&
                    (!paragraphOpen || IsParagraphInterruptingBlockStart(lines[index])))
                {
                    break;
                }

                if (IsBlankLine(lines[index]))
                {
                    paragraphOpen = false;
                    var afterBlankIndex = index + 1;

                    while (afterBlankIndex < lines.Length && IsBlankLine(lines[afterBlankIndex]))
                    {
                        afterBlankIndex++;
                    }

                    if (afterBlankIndex >= lines.Length)
                    {
                        index = afterBlankIndex;
                        break;
                    }

                    if (TryListMarker(lines[afterBlankIndex], out var afterBlank) &&
                        afterBlank.Indent == firstMarker.Indent)
                    {
                        index = afterBlankIndex;

                        if (afterBlank.Delimiter == firstMarker.Delimiter)
                        {
                            list.IsLoose = true;
                        }
                        else
                        {
                            endListAfterItem = true;
                        }

                        break;
                    }

                    // A blank line always ends any open paragraph (see the paragraphOpen = false
                    // above), so a line following one or more blank lines can only continue the
                    // item as an ordinary, properly indented child block - never as a lazy
                    // continuation. It therefore needs the same content-column threshold, and the
                    // same strict "<" as the check above: reaching the content column exactly is
                    // sufficient indentation and must not end the item.
                    if (CountLeadingIndentation(lines[afterBlankIndex]).Column < marker.Indent + marker.MarkerWidth)
                    {
                        index = afterBlankIndex;
                        break;
                    }

                    list.IsLoose = true;
                    continuation.Add(string.Empty);
                }
                else
                {
                    // marker.Indent + marker.MarkerWidth is a column budget derived from a marker
                    // whose own indentation is guaranteed tab-free, so it doubles as a character
                    // count here. The candidate line's own leading run may contain tabs; clamping
                    // against its char length (not its expanded column count) keeps the slice
                    // inside the whitespace run - the same atomic-tab simplification as the fenced
                    // code body strip above.
                    var remove = Math.Min(
                        marker.Indent + marker.MarkerWidth,
                        CountLeadingIndentation(lines[index]).Length);
                    var content = lines[index][remove..];
                    continuation.Add(content);
                    paragraphOpen = !IsBlankLine(content) && !IsParagraphInterruptingBlockStart(content);
                }

                index++;
            }

            if (Has(MarkdownExtension.TaskLists) && TryTask(marker.Content, out var isChecked, out var taskText))
            {
                var paragraph = new DocumentParagraph();
                paragraph.Inlines.Add(new DocumentInlineControl(new CheckBox { IsChecked = isChecked }));

                if (taskText.Length > 0)
                {
                    paragraph.Inlines.Add(new DocumentTextRun(" "));
                    ParseInlines(taskText, paragraph.Inlines);
                }

                item.Blocks.Add(paragraph);
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

            foreach (var block in ParseBlocks(
                         [.. continuation],
                         ref radioGroupOrdinal,
                         blockDepth + 1,
                         diagnostics))
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

    private static void AddNestingLimitDiagnostic(List<DocumentDiagnostic> diagnostics)
    {
        if (diagnostics.Any(static diagnostic => diagnostic.Message == _nestingLimitDiagnostic))
        {
            return;
        }

        diagnostics.Add(new DocumentDiagnostic(_nestingLimitDiagnostic, new DocumentSourceSpan(0, 0)));
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

        while (index < lines.Length &&
               !IsBlankLine(lines[index]) &&
               !IsBlockStart(lines[index]))
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
        ParseParagraphLines(lines, paragraph.Inlines);
        return paragraph;
    }

    private void ParseParagraphLines(List<string> lines, DocumentInlineCollection destination)
    {
        var source = new StringBuilder();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = index == 0
                ? TrimParagraphOpening(lines[index])
                : lines[index].TrimStart(' ', '\t');
            var hasFollowingLine = index + 1 < lines.Count;
            var spaceBreak = hasFollowingLine && line.EndsWith("  ", StringComparison.Ordinal);
            var slashBreak = hasFollowingLine && EndsWithUnescapedBackslash(line);

            if (!spaceBreak && !slashBreak)
            {
                line = line.TrimEnd(' ', '\t');
            }

            _ = source.Append(line);

            if (hasFollowingLine)
            {
                _ = source.Append('\n');
            }
        }

        ParseInlines(source.ToString(), destination);
    }

    [Pure]
    private static string TrimParagraphOpening(string line)
    {
        // A leading tab always expands to at least column 4 (CountLeadingIndentation rounds up to
        // the next 4-column stop), so comparing the true column - rather than the old space-only
        // count with an ad-hoc "next char is a tab" refusal - already keeps a tab-adjacent opening
        // untouched, making that separate check obsolete.
        var indent = CountLeadingIndentation(line);
        return indent.Column <= 3 ? line[indent.Length..] : line;
    }

    private void ParseInlines(string source, DocumentInlineCollection destination, bool insideLink = false)
    {
        var index = 0;
        var plain = new StringBuilder();
        var wikiCloserUnavailable = false;
        var codeSpanEnds = BuildCodeSpanEnds(source);
        var labelCloses = BuildLabelCloses(source, codeSpanEnds);

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
            if (source[index] == '\n')
            {
                var spaceBreak = index >= 2 && source[index - 1] == ' ' && source[index - 2] == ' ';
                var slashBreak = EndsWithUnescapedBackslash(source, index);

                if (spaceBreak)
                {
                    while (plain.Length > 0 && plain[^1] == ' ')
                    {
                        plain.Length--;
                    }
                }
                else if (slashBreak && plain.Length > 0 && plain[^1] == '\\')
                {
                    plain.Length--;
                }

                Flush();
                destination.Add(spaceBreak || slashBreak
                    ? new DocumentLineBreak()
                    : new DocumentSoftBreak());
                index++;
                continue;
            }

            if (source[index] == '\\' && index + 1 < source.Length && IsEscapablePunctuation(source[index + 1]))
            {
                _ = plain.Append(source[index + 1]);
                index += 2;
                continue;
            }

            if (source[index] == '&' &&
                TryDecodeCharacterReference(source, index, out var referenceLength, out var decoded))
            {
                _ = plain.Append(decoded);
                index += referenceLength;
                continue;
            }

            if (!insideLink && source[index] == '<' &&
                TryAngleAutolink(source, index, out var angleEnd, out var angleText, out var angleTarget))
            {
                Flush();
                destination.Add(new DocumentLink(angleText, angleTarget));
                index = angleEnd;
                continue;
            }

            if (!insideLink && Has(MarkdownExtension.Autolinks) &&
                TryExtendedAutolink(source, index, out var urlEnd, out var urlText, out var urlTarget))
            {
                Flush();
                destination.Add(new DocumentLink(urlText, urlTarget));
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

            if (TryEmphasisDelimited(source, index, "***", out var combinedEnd))
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

            if (TryEmphasisDelimited(source, index, "**", out var strongEnd))
            {
                Flush();
                var strong = new DocumentStrong();
                ParseInlines(source[(index + 2)..strongEnd], strong.Inlines, insideLink);
                destination.Add(strong);
                index = strongEnd + 2;
                continue;
            }

            if (TryEmphasisDelimited(source, index, "___", out var combinedUnderscoreEnd))
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

            if (TryEmphasisDelimited(source, index, "__", out var strongUnderscoreEnd))
            {
                Flush();
                var strongUnderscoreOnly = new DocumentStrong();
                ParseInlines(source[(index + 2)..strongUnderscoreEnd], strongUnderscoreOnly.Inlines, insideLink);
                destination.Add(strongUnderscoreOnly);
                index = strongUnderscoreEnd + 2;
                continue;
            }

            if (Has(MarkdownExtension.Strikethrough) &&
                TryStrikethrough(source, index, out var strikeEnd, out var strikeDelimiterLength))
            {
                Flush();
                var strike = new DocumentStrikethrough();
                ParseInlines(source[(index + strikeDelimiterLength)..strikeEnd], strike.Inlines, insideLink);
                destination.Add(strike);
                index = strikeEnd + strikeDelimiterLength;
                continue;
            }

            if (source[index] is '*' or '_' &&
                TryEmphasisDelimited(source, index, source[index].ToString(), out var emphasisEnd))
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
                var codeEnd = codeSpanEnds[index];

                if (codeEnd >= 0)
                {
                    Flush();
                    destination.Add(new DocumentCodeSpan(
                        NormalizeCodeSpan(source[(index + delimiterLength)..codeEnd])));
                    index = codeEnd + delimiterLength;
                    continue;
                }
            }

            if (!insideLink && source[index] == '[' && labelCloses[index] >= 0 &&
                TryLink(source, index, labelCloses[index], out var linkEnd, out var label, out var target))
            {
                var parsedLabel = new DocumentParagraph();
                ParseInlines(label, parsedLabel.Inlines);

                if (ContainsLink(parsedLabel.Inlines))
                {
                    _ = plain.Append(source[index]);
                    index++;
                    continue;
                }

                Flush();
                var link = new DocumentLink { Target = target.Length == 0 ? null : target };
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
    private static bool ContainsLink(IEnumerable<DocumentInline> inlines) =>
        inlines.Any(static inline => inline is DocumentLink ||
            (inline is DocumentInlineContainer container && ContainsLink(container.Inlines)));

    [Pure]
    private bool Has(MarkdownExtension extension) => (_extensions & extension) != 0;

    private int[] BuildCodeSpanEnds(string source)
    {
        var ends = new int[source.Length];
        Array.Fill(ends, -1);
        var previousByLength = new Dictionary<int, int>();
        var index = 0;

        while (index < source.Length)
        {
            InlineCandidateScanCount++;

            if (source[index] != '`')
            {
                index++;
                continue;
            }

            var length = CountRun(source, index, '`');
            InlineCandidateScanCount += length - 1;

            if (previousByLength.TryGetValue(length, out var previous))
            {
                ends[previous] = index;
            }

            previousByLength[length] = index;
            index += length;
        }

        return ends;
    }

    private int[] BuildLabelCloses(string source, int[] codeSpanEnds)
    {
        var closes = new int[source.Length];
        Array.Fill(closes, -1);
        var openers = new Stack<int>();
        var index = 0;

        while (index < source.Length)
        {
            InlineCandidateScanCount++;

            if (source[index] == '\\' && index + 1 < source.Length)
            {
                InlineCandidateScanCount++;
                index += 2;
                continue;
            }

            if (source[index] == '`' && codeSpanEnds[index] >= 0)
            {
                var delimiterLength = CountRun(source, index, '`');
                var next = codeSpanEnds[index] + delimiterLength;
                InlineCandidateScanCount += next - index - 1;
                index = next;
                continue;
            }

            if (source[index] == '<' && TryAngleAutolink(source, index, out var angleEnd, out _, out _))
            {
                InlineCandidateScanCount += angleEnd - index - 1;
                index = angleEnd;
                continue;
            }

            if (source[index] == '[')
            {
                openers.Push(index);
            }
            else if (source[index] == ']' && openers.TryPop(out var opener))
            {
                closes[opener] = index;
            }

            index++;
        }

        return closes;
    }

    [Pure]
    private static bool TryStrikethrough(string source, int index, out int end, out int delimiterLength)
    {
        delimiterLength = CountRun(source, index, '~');

        if (delimiterLength is not (1 or 2) ||
            index + delimiterLength >= source.Length ||
            char.IsWhiteSpace(source[index + delimiterLength]))
        {
            end = -1;
            return false;
        }

        var cursor = index + delimiterLength;

        while (cursor < source.Length)
        {
            if (source[cursor] == '\\' && cursor + 1 < source.Length)
            {
                cursor += 2;
                continue;
            }

            if (source[cursor] != '~')
            {
                cursor++;
                continue;
            }

            var candidateLength = CountRun(source, cursor, '~');

            if (candidateLength == delimiterLength && cursor > index + delimiterLength &&
                !char.IsWhiteSpace(source[cursor - 1]))
            {
                end = cursor;
                return true;
            }

            cursor += candidateLength;
        }

        end = -1;
        return false;
    }

    [Pure]
    private static bool TryEmphasisDelimited(string source, int index, string delimiter, out int end)
    {
        if (!source.AsSpan(index).StartsWith(delimiter, StringComparison.Ordinal))
        {
            end = -1;
            return false;
        }

        var marker = delimiter[0];
        var openingRunLength = CountRun(source, index, marker);

        if (!CanOpenEmphasisRun(source, index, openingRunLength, marker))
        {
            end = -1;
            return false;
        }

        var cursor = index + delimiter.Length;

        while (cursor < source.Length)
        {
            var candidate = FindUnescaped(source, delimiter, cursor);

            if (candidate < 0)
            {
                end = -1;
                return false;
            }

            var closingRunLength = CountRun(source, candidate, marker);

            if (candidate > index + delimiter.Length &&
                CanCloseEmphasisRun(source, candidate, closingRunLength, marker))
            {
                end = candidate;
                return true;
            }

            cursor = candidate + Math.Max(closingRunLength, 1);
        }

        end = -1;
        return false;
    }

    [Pure]
    private static bool TryLink(
        string source,
        int index,
        int closeLabel,
        out int end,
        out string label,
        out string target)
    {
        if (closeLabel + 1 >= source.Length || source[closeLabel + 1] != '(')
        {
            end = -1;
            label = string.Empty;
            target = string.Empty;
            return false;
        }

        var cursor = closeLabel + 2;

        if (!TrySkipInlineLinkWhitespace(source, ref cursor, out _) ||
            !TryInlineLinkDestination(source, ref cursor, out target))
        {
            end = -1;
            label = string.Empty;
            target = string.Empty;
            return false;
        }

        if (!TrySkipInlineLinkWhitespace(source, ref cursor, out var separated))
        {
            end = -1;
            label = string.Empty;
            target = string.Empty;
            return false;
        }

        if (separated && cursor < source.Length && source[cursor] is '\'' or '"' or '(' &&
            !TryInlineLinkTitle(source, ref cursor))
        {
            end = -1;
            label = string.Empty;
            target = string.Empty;
            return false;
        }

        if (!TrySkipInlineLinkWhitespace(source, ref cursor, out _) ||
            cursor >= source.Length || source[cursor] != ')')
        {
            end = -1;
            label = string.Empty;
            target = string.Empty;
            return false;
        }

        label = source[(index + 1)..closeLabel];
        end = cursor + 1;
        return true;
    }

    [Pure]
    private static bool TryInlineLinkDestination(string source, ref int cursor, out string target)
    {
        if (cursor >= source.Length || source[cursor] == ')')
        {
            target = string.Empty;
            return true;
        }

        if (source[cursor] == '<')
        {
            var start = ++cursor;

            while (cursor < source.Length)
            {
                if (source[cursor] == '\\' && cursor + 1 < source.Length &&
                    IsEscapablePunctuation(source[cursor + 1]))
                {
                    cursor += 2;
                    continue;
                }

                if (source[cursor] == '>')
                {
                    target = UnescapePunctuation(source[start..cursor]);
                    cursor++;
                    return true;
                }

                if (source[cursor] == '<' || char.IsControl(source[cursor]))
                {
                    target = string.Empty;
                    return false;
                }

                cursor++;
            }

            target = string.Empty;
            return false;
        }

        var destinationStart = cursor;
        var depth = 0;

        while (cursor < source.Length)
        {
            if (source[cursor] == '\\' && cursor + 1 < source.Length &&
                IsEscapablePunctuation(source[cursor + 1]))
            {
                cursor += 2;
                continue;
            }

            if (source[cursor] is ' ' or '\t' or '\n' or '\r')
            {
                break;
            }

            if (char.IsControl(source[cursor]) || source[cursor] == '<')
            {
                target = string.Empty;
                return false;
            }

            if (source[cursor] == '(')
            {
                depth++;
            }
            else if (source[cursor] == ')')
            {
                if (depth == 0)
                {
                    break;
                }

                depth--;
            }

            cursor++;
        }

        if (cursor == destinationStart || depth != 0)
        {
            target = string.Empty;
            return false;
        }

        target = UnescapePunctuation(source[destinationStart..cursor]);
        return true;
    }

    [Pure]
    private static bool TryInlineLinkTitle(string source, ref int cursor)
    {
        var opener = source[cursor];
        var closer = opener == '(' ? ')' : opener;
        cursor++;

        while (cursor < source.Length)
        {
            if (source[cursor] == '\\' && cursor + 1 < source.Length &&
                IsEscapablePunctuation(source[cursor + 1]))
            {
                cursor += 2;
                continue;
            }

            if (source[cursor] == closer)
            {
                cursor++;
                return true;
            }

            if (opener == '(' && source[cursor] == '(')
            {
                return false;
            }

            if (source[cursor] == '\n')
            {
                var next = cursor + 1;

                while (next < source.Length && source[next] is ' ' or '\t')
                {
                    next++;
                }

                if (next < source.Length && source[next] == '\n')
                {
                    return false;
                }
            }

            cursor++;
        }

        return false;
    }

    [Pure]
    private static bool TrySkipInlineLinkWhitespace(string source, ref int cursor, out bool skipped)
    {
        skipped = false;
        var lineEndings = 0;

        while (cursor < source.Length && source[cursor] is ' ' or '\t' or '\n' or '\r')
        {
            skipped = true;

            if (source[cursor] == '\n' ||
                (source[cursor] == '\r' && (cursor + 1 >= source.Length || source[cursor + 1] != '\n')))
            {
                lineEndings++;

                if (lineEndings > 1)
                {
                    return false;
                }
            }

            cursor++;
        }

        return true;
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
    private static string NormalizeCodeSpan(string source)
    {
        var normalized = source.Replace('\n', ' ').Replace('\r', ' ');

        return normalized.Length >= 2 && normalized[0] == ' ' && normalized[^1] == ' ' &&
            normalized.Any(static character => character != ' ')
            ? normalized[1..^1]
            : normalized;
    }

    [Pure]
    private static bool CanOpenEmphasisRun(string source, int index, int runLength, char marker)
    {
        var (leftFlanking, rightFlanking, beforePunctuation, _) =
            ClassifyEmphasisRun(source, index, runLength);
        return marker == '*'
            ? leftFlanking
            : leftFlanking && (!rightFlanking || beforePunctuation);
    }

    [Pure]
    private static bool CanCloseEmphasisRun(string source, int index, int runLength, char marker)
    {
        var (leftFlanking, rightFlanking, _, afterPunctuation) =
            ClassifyEmphasisRun(source, index, runLength);
        return marker == '*'
            ? rightFlanking
            : rightFlanking && (!leftFlanking || afterPunctuation);
    }

    [Pure]
    private static (bool LeftFlanking, bool RightFlanking, bool BeforePunctuation, bool AfterPunctuation)
        ClassifyEmphasisRun(string source, int index, int runLength)
    {
        var runEnd = index + runLength;
        var beforeWhitespace = index == 0 || Rune.IsWhiteSpace(GetRuneBefore(source, index));
        var afterWhitespace = runEnd >= source.Length || Rune.IsWhiteSpace(Rune.GetRuneAt(source, runEnd));
        var beforePunctuation = index > 0 && IsPunctuationOrSymbol(GetRuneBefore(source, index));
        var afterPunctuation = runEnd < source.Length && IsPunctuationOrSymbol(Rune.GetRuneAt(source, runEnd));
        var leftFlanking = !afterWhitespace && (!afterPunctuation || beforeWhitespace || beforePunctuation);
        var rightFlanking = !beforeWhitespace && (!beforePunctuation || afterWhitespace || afterPunctuation);
        return (leftFlanking, rightFlanking, beforePunctuation, afterPunctuation);
    }

    [Pure]
    private static Rune GetRuneBefore(string source, int index)
    {
        var runeStart = index - 1;

        if (runeStart > 0 && char.IsLowSurrogate(source[runeStart]) && char.IsHighSurrogate(source[runeStart - 1]))
        {
            runeStart--;
        }

        return Rune.GetRuneAt(source, runeStart);
    }

    [Pure]
    private static bool IsPunctuationOrSymbol(Rune value) => Rune.GetUnicodeCategory(value) is
        UnicodeCategory.ConnectorPunctuation or
        UnicodeCategory.DashPunctuation or
        UnicodeCategory.OpenPunctuation or
        UnicodeCategory.ClosePunctuation or
        UnicodeCategory.InitialQuotePunctuation or
        UnicodeCategory.FinalQuotePunctuation or
        UnicodeCategory.OtherPunctuation or
        UnicodeCategory.MathSymbol or
        UnicodeCategory.CurrencySymbol or
        UnicodeCategory.ModifierSymbol or
        UnicodeCategory.OtherSymbol;

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
                _ = result.Append(source[index]);
                continue;
            }

            if (source[index] == '&' &&
                TryDecodeCharacterReference(source, index, out var referenceLength, out var decoded))
            {
                _ = result.Append(decoded);
                index += referenceLength - 1;
                continue;
            }

            _ = result.Append(source[index]);
        }

        return result.ToString();
    }

    /// <summary>Attempts to decode an HTML character reference (named or numeric) starting at the
    /// <c>&amp;</c> found at <paramref name="index"/>. On success, <paramref name="length"/> is the
    /// number of source characters the reference occupies (including the leading <c>&amp;</c> and
    /// trailing <c>;</c>) and <paramref name="decoded"/> holds the replacement text. On failure the
    /// <c>&amp;</c> and everything after it are left completely untouched by the caller.</summary>
    [Pure]
    private static bool TryDecodeCharacterReference(string source, int index, out int length, out string decoded)
    {
        length = 0;
        decoded = string.Empty;

        var scanLimit = Math.Min(source.Length, index + _maximumCharacterReferenceBodyLength + 2);
        var semicolon = -1;

        for (var cursor = index + 1; cursor < scanLimit; cursor++)
        {
            if (source[cursor] == ';')
            {
                semicolon = cursor;
                break;
            }

            if (source[cursor] is not ('#' or 'x' or 'X' or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or
                (>= '0' and <= '9')))
            {
                break;
            }
        }

        if (semicolon < 0)
        {
            return false;
        }

        var body = source.AsSpan(index + 1, semicolon - index - 1);

        if (!(body.Length > 0 && body[0] == '#'
                ? TryDecodeNumericReference(body[1..], out decoded)
                : TryDecodeNamedReference(body, out decoded)))
        {
            return false;
        }

        length = semicolon - index + 1;
        return true;
    }

    /// <summary>Decodes a decimal (<c>N</c>) or hexadecimal (<c>xN</c>/<c>XN</c>) numeric character
    /// reference body. Per the CommonMark/HTML5 numeric-reference rules, the null code point, code
    /// points beyond U+10FFFF, and surrogate-range code points all decode to U+FFFD rather than
    /// being rejected.</summary>
    [Pure]
    private static bool TryDecodeNumericReference(ReadOnlySpan<char> digits, out string decoded)
    {
        decoded = string.Empty;

        if (digits.Length == 0)
        {
            return false;
        }

        int codepoint;

        if (digits[0] is 'x' or 'X')
        {
            var hexDigits = digits[1..];

            if (hexDigits.Length == 0 ||
                !int.TryParse(hexDigits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out codepoint))
            {
                return false;
            }
        }
        else if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out codepoint))
        {
            return false;
        }

        var rune = codepoint != 0 && Rune.TryCreate(codepoint, out var parsed) ? parsed : Rune.ReplacementChar;
        decoded = rune.ToString();
        return true;
    }

    /// <summary>Decodes a curated set of named character references: the five XML entities, the
    /// highest-frequency typographic and arrow entities, and a small set of accented Latin letters.
    /// Any name outside this curated set fails and is left completely literal by the caller - this
    /// reader does not attempt the full ~2200-entry HTML5 named-reference table.</summary>
    [Pure]
    private static bool TryDecodeNamedReference(ReadOnlySpan<char> name, out string decoded)
    {
        decoded = name switch
        {
            "amp" => "&",
            "lt" => "<",
            "gt" => ">",
            "quot" => "\"",
            "apos" => "'",
            "nbsp" => " ",
            "copy" => "©",
            "reg" => "®",
            "trade" => "™",
            "hellip" => "…",
            "mdash" => "—",
            "ndash" => "–",
            "larr" => "←",
            "rarr" => "→",
            "uarr" => "↑",
            "darr" => "↓",
            "eacute" => "é",
            "egrave" => "è",
            "agrave" => "à",
            "auml" => "ä",
            "ouml" => "ö",
            "uuml" => "ü",
            "ntilde" => "ñ",
            "ccedil" => "ç",
            _ => string.Empty
        };

        return decoded.Length > 0;
    }

    [Pure]
    private static bool TryAngleAutolink(
        string source,
        int index,
        out int end,
        out string text,
        out string target)
    {
        var close = source.IndexOf('>', index + 1);

        if (close > index + 1)
        {
            var candidate = source[(index + 1)..close];

            if (IsCommonMarkUriAutolink(candidate))
            {
                text = candidate;
                target = candidate;
                end = close + 1;
                return true;
            }

            if (IsCommonMarkEmailAutolink(candidate))
            {
                text = candidate;
                target = $"mailto:{candidate}";
                end = close + 1;
                return true;
            }
        }

        text = string.Empty;
        target = string.Empty;
        end = -1;
        return false;
    }

    [Pure]
    private static bool IsCommonMarkUriAutolink(string candidate)
    {
        var colon = candidate.IndexOf(':');

        if (colon is < 2 or > 32 || !char.IsAsciiLetter(candidate[0]))
        {
            return false;
        }

        for (var index = 1; index < colon; index++)
        {
            if (!char.IsAsciiLetterOrDigit(candidate[index]) && candidate[index] is not ('+' or '-' or '.'))
            {
                return false;
            }
        }

        var destination = candidate.AsSpan(colon + 1);

        if (destination.IsEmpty || destination.SequenceEqual("//"))
        {
            return false;
        }

        foreach (var character in destination)
        {
            if (character is <= ' ' or '\u007f' or '<')
            {
                return false;
            }
        }

        return true;
    }

    [Pure]
    private static bool IsCommonMarkEmailAutolink(string candidate)
    {
        var separator = candidate.IndexOf('@');

        if (separator <= 0 || separator != candidate.LastIndexOf('@') || separator == candidate.Length - 1)
        {
            return false;
        }

        foreach (var character in candidate.AsSpan(0, separator))
        {
            if (!char.IsAsciiLetterOrDigit(character) &&
                character is not ('.' or '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or
                    '/' or '=' or '?' or '^' or '_' or '`' or '{' or '|' or '}' or '~' or '-'))
            {
                return false;
            }
        }

        foreach (var label in candidate.AsSpan(separator + 1).ToString().Split('.'))
        {
            if (label.Length is < 1 or > 63 ||
                !char.IsAsciiLetterOrDigit(label[0]) ||
                !char.IsAsciiLetterOrDigit(label[^1]) ||
                label.Any(static character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryExtendedAutolink(
        string source,
        int index,
        out int end,
        out string text,
        out string target)
    {
        if (index > 0 && !char.IsWhiteSpace(source[index - 1]) && source[index - 1] is not ('(' or '*' or '_' or '~'))
        {
            end = -1;
            text = string.Empty;
            target = string.Empty;
            return false;
        }

        var remainder = source.AsSpan(index);
        var isUrl = remainder.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    remainder.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        var isWww = remainder.StartsWith("www.", StringComparison.OrdinalIgnoreCase);

        var length = 0;

        while (length < remainder.Length && remainder[length] is > ' ' and not ('\u007f' or '<' or '>'))
        {
            length++;
            InlineCandidateScanCount++;
        }

        while (length > 0 && remainder[length - 1] is '.' or ',' or ';' or ':' or '!' or '?' or '*' or '_' or '~')
        {
            length--;
            InlineCandidateScanCount++;
        }

        var openingParentheses = 0;
        var closingParentheses = 0;
        var openingBrackets = 0;
        var closingBrackets = 0;

        foreach (var character in remainder[..length])
        {
            InlineCandidateScanCount++;

            switch (character)
            {
                case '(':
                    openingParentheses++;
                    break;
                case ')':
                    closingParentheses++;
                    break;
                case '[':
                    openingBrackets++;
                    break;
                case ']':
                    closingBrackets++;
                    break;
                default:
                    break;
            }
        }

        while (length > 0 && remainder[length - 1] == ')' && closingParentheses > openingParentheses)
        {
            length--;
            closingParentheses--;
            InlineCandidateScanCount++;
        }

        while (length > 0 && remainder[length - 1] == ']' && closingBrackets > openingBrackets)
        {
            length--;
            closingBrackets--;
            InlineCandidateScanCount++;
        }

        text = remainder[..length].ToString();
        end = index + length;

        if (isUrl && TryGetExtendedAutolinkHost(text, out var urlHost) && IsGfmDomain(urlHost))
        {
            target = text;
            return true;
        }

        if (isWww && TryGetExtendedAutolinkHost($"http://{text}", out var wwwHost) && IsGfmDomain(wwwHost))
        {
            target = $"http://{text}";
            return true;
        }

        if (!isUrl && !isWww && IsCommonMarkEmailAutolink(text) && text[(text.IndexOf('@') + 1)..].Contains('.'))
        {
            target = $"mailto:{text}";
            return true;
        }

        end = -1;
        text = string.Empty;
        target = string.Empty;
        return false;
    }

    [Pure]
    private static bool TryGetExtendedAutolinkHost(string candidate, out string host)
    {
        var authority = candidate.IndexOf("//", StringComparison.Ordinal);
        var start = authority < 0 ? 0 : authority + 2;
        var end = candidate.IndexOfAny(['/', '?', '#'], start);
        var value = candidate[start..(end < 0 ? candidate.Length : end)];
        var port = value.LastIndexOf(':');

        if (port >= 0 && !value.AsSpan(port + 1).IsEmpty &&
            !value.AsSpan(port + 1).ContainsAnyExceptInRange('0', '9'))
        {
            value = value[..port];
        }

        host = value;
        return host.Length > 0;
    }

    [Pure]
    private static bool IsGfmDomain(string host)
    {
        if (!host.Contains('.'))
        {
            return false;
        }

        foreach (var label in host.Split('.'))
        {
            if (label.Length is < 1 or > 63 ||
                !char.IsAsciiLetterOrDigit(label[0]) ||
                !char.IsAsciiLetterOrDigit(label[^1]) ||
                label.Any(static character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            {
                return false;
            }
        }

        return true;
    }

    [Pure]
    private static bool TryListMarker(string line, out MarkdownListMarker marker)
    {
        var indent = CountLeadingIndentation(line);

        if (indent.Column > 3)
        {
            marker = default;
            return false;
        }

        var trimmed = line[indent.Length..];

        if (trimmed.Length > 0 && trimmed[0] is '-' or '+' or '*')
        {
            var suffix = trimmed.AsSpan(1);

            if (suffix.IsEmpty || ContainsOnlyListMarkerWhitespace(suffix))
            {
                marker = new MarkdownListMarker(
                    indent.Column,
                    isOrdered: false,
                    delimiter: trimmed[0],
                    start: 1,
                    markerWidth: 2,
                    string.Empty);
                return true;
            }

            var spacing = CountListMarkerSpaces(suffix);

            if (spacing > 0)
            {
                marker = new MarkdownListMarker(
                    indent.Column,
                    isOrdered: false,
                    delimiter: trimmed[0],
                    start: 1,
                    markerWidth: 1 + spacing,
                    suffix[spacing..].ToString());
                return true;
            }
        }

        var digits = 0;

        while (digits < trimmed.Length && char.IsAsciiDigit(trimmed[digits]))
        {
            digits++;

            if (digits > 9)
            {
                marker = default;
                return false;
            }
        }

        if (digits > 0 && digits < trimmed.Length && trimmed[digits] is '.' or ')' &&
            int.TryParse(trimmed.AsSpan(0, digits), CultureInfo.InvariantCulture, out var start))
        {
            var suffix = trimmed.AsSpan(digits + 1);

            if (suffix.IsEmpty || ContainsOnlyListMarkerWhitespace(suffix))
            {
                marker = new MarkdownListMarker(
                    indent.Column,
                    isOrdered: true,
                    delimiter: trimmed[digits],
                    start,
                    markerWidth: digits + 2,
                    string.Empty);
                return true;
            }

            var spacing = CountListMarkerSpaces(suffix);

            if (spacing > 0)
            {
                marker = new MarkdownListMarker(
                    indent.Column,
                    isOrdered: true,
                    delimiter: trimmed[digits],
                    start,
                    markerWidth: digits + 1 + spacing,
                    suffix[spacing..].ToString());
                return true;
            }
        }

        marker = default;
        return false;
    }

    [Pure]
    private static bool ContainsOnlyListMarkerWhitespace(ReadOnlySpan<char> source)
    {
        foreach (var character in source)
        {
            if (character is not (' ' or '\t'))
            {
                return false;
            }
        }

        return true;
    }

    [Pure]
    private static int CountListMarkerSpaces(ReadOnlySpan<char> source)
    {
        var spaces = 0;

        while (spaces < source.Length && source[spaces] == ' ')
        {
            spaces++;
        }

        return spaces > 4 ? 1 : spaces;
    }

    [Pure]
    private static bool TryTask(string source, out bool isChecked, out string text)
    {
        if (source.Length >= 4 && source[0] == '[' && source[2] == ']' &&
            (IsMarkdownWhitespace(source[1]) || source[1] is 'x' or 'X') &&
            IsMarkdownWhitespace(source[3]))
        {
            var contentStart = 4;

            while (contentStart < source.Length && IsMarkdownWhitespace(source[contentStart]))
            {
                contentStart++;
            }

            isChecked = source[1] is 'x' or 'X';
            text = source[contentStart..];
            return true;
        }

        isChecked = false;
        text = string.Empty;
        return false;
    }

    [Pure]
    private static bool IsMarkdownWhitespace(char value) => value is ' ' or '\t' or '\n' or '\v' or '\f' or '\r';

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
            var value = cell.AsSpan().Trim();

            if (value.IsEmpty)
            {
                alignments = null;
                return false;
            }

            var leading = value[0] == ':';
            var trailing = value[^1] == ':';
            var hyphenStart = leading ? 1 : 0;
            var hyphenEnd = value.Length - (trailing ? 1 : 0);

            if (hyphenStart >= hyphenEnd)
            {
                alignments = null;
                return false;
            }

            for (var index = hyphenStart; index < hyphenEnd; index++)
            {
                if (value[index] == '-')
                {
                    continue;
                }

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

        for (var index = 0; index < trimmed.Length; index++)
        {
            if (trimmed[index] == '\\' && index + 1 < trimmed.Length && trimmed[index + 1] == '|')
            {
                _ = cell.Append('|');
                index++;
                continue;
            }

            if (trimmed[index] == '\\' && index + 1 < trimmed.Length)
            {
                _ = cell.Append(trimmed[index]).Append(trimmed[index + 1]);
                index++;
                continue;
            }

            if (trimmed[index] == '|')
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
        var indent = CountLeadingIndentation(line);

        // The optional indentation prefix is distinct from whitespace between markers. Keeping
        // that boundary explicit prevents a leading tab - which now expands to a full 4-column
        // stop and so almost always exceeds the 3-column prefix budget on its own - from being
        // discarded as if it were an interior separator.
        if (indent.Column > 3 || indent.Length >= line.Length || line[indent.Length] is not ('-' or '*' or '_'))
        {
            return false;
        }

        var marker = line[indent.Length];
        var markerCount = 0;

        for (var index = indent.Length; index < line.Length; index++)
        {
            if (line[index] == marker)
            {
                markerCount++;
            }
            else if (line[index] is not (' ' or '\t'))
            {
                return false;
            }
        }

        return markerCount >= 3;
    }

    [Pure]
    private static bool TrySetextUnderline(string source, out int level)
    {
        var indent = CountLeadingIndentation(source);

        if (indent.Column > 3)
        {
            level = 0;
            return false;
        }

        var value = source[indent.Length..].TrimEnd();

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
        TryBlockQuoteMarker(line, out _) ||
        TryFenceOpener(line, out _) ||
        TryListMarker(line, out _) ||
        IsRule(line);

    [Pure]
    private bool IsParagraphInterruptingBlockStart(string line) =>
        TryListMarker(line, out var marker)
            ? marker.Content.Length > 0 && (!marker.IsOrdered || marker.Start == 1)
            : TryHeading(line, out _) ||
              TryBlockQuoteMarker(line, out _) ||
              TryFenceOpener(line, out _) ||
              IsRule(line);

    [Pure]
    private static bool TryBlockQuoteMarker(string source, out int contentStart)
    {
        var indent = CountLeadingIndentation(source);

        if (indent.Column > 3 || indent.Length >= source.Length || source[indent.Length] != '>')
        {
            contentStart = 0;
            return false;
        }

        contentStart = indent.Length + 1;

        if (contentStart < source.Length && source[contentStart] == ' ')
        {
            contentStart++;
        }

        return true;
    }

    /// <summary>Measures a line's leading run of spaces and tabs.</summary>
    /// <returns>The indentation width in columns - each tab rounds up to the next 4-column stop
    /// per CommonMark §2.2 ("atomic tab": a tab that straddles a threshold rounds to its full stop
    /// rather than being split, so the column is not a character offset) - alongside the number of
    /// characters the run actually occupies, which callers use as the char/slice offset instead.
    /// </returns>
    [Pure]
    private static (int Column, int Length) CountLeadingIndentation(string source)
    {
        var column = 0;
        var length = 0;

        while (length < source.Length)
        {
            if (source[length] == ' ')
            {
                column++;
                length++;
                continue;
            }

            if (source[length] == '\t')
            {
                column += 4 - (column % 4);
                length++;
                continue;
            }

            break;
        }

        return (column, length);
    }

    [Pure]
    private static bool IsBlankLine(string source) =>
        !source.AsSpan().ContainsAnyExcept(' ', '\t');

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
        => EndsWithUnescapedBackslash(source, source.Length);

    [Pure]
    private static bool EndsWithUnescapedBackslash(string source, int endExclusive)
    {
        var slashes = 0;

        for (var index = endExclusive - 1; index >= 0 && source[index] == '\\'; index--)
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

}
