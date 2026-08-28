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

        var lines = CreateSourceLines(source);
        var radioGroupOrdinal = 0;
        var diagnostics = new List<DocumentDiagnostic>();
        var blocks = ParseBlocks(lines, ref radioGroupOrdinal, blockDepth: 0, diagnostics);
        return new DocumentReadResult(blocks, diagnostics);
    }

    /// <summary>Gets the characters examined while indexing inline delimiter candidates during
    /// the most recent read, exposing the bounded-scan invariant for hostile inputs.</summary>
    internal int InlineCandidateScanCount { get; private set; }

    private static MarkdownSourceLine[] CreateSourceLines(string source)
    {
        var lines = new List<MarkdownSourceLine>();
        var lineStart = 0;
        var index = 0;

        while (index < source.Length)
        {
            if (source[index] is not ('\r' or '\n'))
            {
                index++;
                continue;
            }

            lines.Add(new MarkdownSourceLine(source[lineStart..index].Replace('\0', '\ufffd'), lineStart));

            if (source[index] == '\r' && index + 1 < source.Length && source[index + 1] == '\n')
            {
                index++;
            }

            index++;
            lineStart = index;
        }

        lines.Add(new MarkdownSourceLine(source[lineStart..].Replace('\0', '\ufffd'), lineStart));
        return [.. lines];
    }

    private List<DocumentBlock> ParseBlocks(
        MarkdownSourceLine[] lines,
        ref int radioGroupOrdinal,
        int blockDepth,
        List<DocumentDiagnostic> diagnostics)
    {
        var blocks = new List<DocumentBlock>();
        var index = 0;

        while (index < lines.Length)
        {
            var line = lines[index];
            var text = line.Text;

            if (IsBlankLine(text))
            {
                index++;
                continue;
            }

            if (TryFence(lines, ref index, out var code))
            {
                blocks.Add(code);
                continue;
            }

            if (TryHeading(text, out var heading))
            {
                blocks.Add(heading);
                index++;
                continue;
            }

            if (IsRule(text))
            {
                blocks.Add(new DocumentSeparator());
                index++;
                continue;
            }

            if (TryBlockQuoteMarker(text, out _))
            {
                blocks.Add(ParseQuote(lines, ref index, ref radioGroupOrdinal, blockDepth, diagnostics));
                continue;
            }

            if (Has(MarkdownExtension.Tables) && index + 1 < lines.Length &&
                lines[index].Text.Contains('|') && lines[index + 1].Text.Contains('|') &&
                TryTableAlignments(lines[index + 1].Text, out var alignments) &&
                SplitTableRow(lines[index].Text).Count == alignments.Count)
            {
                blocks.Add(ParseTable(lines, ref index, alignments));
                continue;
            }

            if (TryListMarker(text, out _))
            {
                if (blockDepth >= _maximumRecursiveBlockDepth)
                {
                    AddNestingLimitDiagnostic(diagnostics, line);
                    blocks.Add(CreateParagraph([text]));
                    index++;
                    continue;
                }

                blocks.Add(ParseList(lines, ref index, ref radioGroupOrdinal, blockDepth, diagnostics));
                continue;
            }

            var paragraphLines = new List<string>();
            var setextLevel = 0;

            while (index < lines.Length && !IsBlankLine(lines[index].Text))
            {
                if (paragraphLines.Count > 0 && TrySetextUnderline(lines[index].Text, out setextLevel))
                {
                    index++;
                    break;
                }

                if (paragraphLines.Count > 0 && IsParagraphInterruptingBlockStart(lines[index].Text))
                {
                    break;
                }

                paragraphLines.Add(lines[index].Text);
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

    private static bool TryFence(
        MarkdownSourceLine[] lines,
        ref int index,
        [NotNullWhen(true)] out DocumentCodeBlock? code)
    {
        if (!TryFenceOpener(lines[index].Text, out var fence))
        {
            code = null;
            return false;
        }

        index++;
        var body = new StringBuilder();
        var hasBodyLine = false;

        while (index < lines.Length && !IsFenceCloser(lines[index].Text, fence))
        {
            if (hasBodyLine)
            {
                _ = body.Append('\n');
            }

            var line = lines[index].Text;

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
        MarkdownSourceLine[] lines,
        ref int index,
        ref int radioGroupOrdinal,
        int blockDepth,
        List<DocumentDiagnostic> diagnostics)
    {
        var quoted = new List<MarkdownSourceLine>();
        var firstMarkerLine = lines[index];
        var paragraphOpen = false;

        while (index < lines.Length)
        {
            if (TryBlockQuoteMarker(lines[index].Text, out var contentStart))
            {
                var content = lines[index].Slice(contentStart);
                quoted.Add(content);
                paragraphOpen = !IsBlankLine(content.Text) && !IsParagraphInterruptingBlockStart(content.Text);
                index++;
                continue;
            }

            // CommonMark lazy continuation: a line that carries no '>' marker still belongs to
            // the quote when it continues an already-open paragraph and does not itself look like
            // the start of another block. A blank line (handled by the loop condition failing on
            // IsBlankLine below) always closes that eligibility.
            if (!paragraphOpen || IsBlankLine(lines[index].Text) ||
                IsParagraphInterruptingBlockStart(lines[index].Text))
            {
                break;
            }

            quoted.Add(lines[index]);
            index++;
        }

        if (blockDepth < _maximumRecursiveBlockDepth && Has(MarkdownExtension.Callouts) &&
            TryCalloutHeader(quoted[0].Text, out var kind, out var title))
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

        if (blockDepth >= _maximumRecursiveBlockDepth)
        {
            AddNestingLimitDiagnostic(diagnostics, firstMarkerLine);
            quote.Blocks.Add(CreateParagraph([.. quoted.Select(static line => line.Text)]));
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
        MarkdownSourceLine[] lines,
        ref int index,
        ref int radioGroupOrdinal,
        int blockDepth,
        List<DocumentDiagnostic> diagnostics)
    {
        _ = TryListMarker(lines[index].Text, out var firstMarker);
        var list = new DocumentList(firstMarker.IsOrdered ? DocumentListKind.Numbered : DocumentListKind.Bulleted)
        {
            Start = firstMarker.Start
        };
        var radioGroup = Has(MarkdownExtension.RadioLists)
            ? FormattableString.Invariant($"markdown-radio-{radioGroupOrdinal++}")
            : null;
        RadioButton? selectedRadio = null;

        while (index < lines.Length && TryListMarker(lines[index].Text, out var marker) &&
               marker.Indent == firstMarker.Indent && marker.Delimiter == firstMarker.Delimiter)
        {
            var item = new DocumentListItem();
            var markerLine = lines[index];
            var continuation = new List<MarkdownSourceLine>();
            var endListAfterItem = false;
            var paragraphOpen = marker.Content.Length > 0 && !IsParagraphInterruptingBlockStart(marker.Content);
            index++;

            while (index < lines.Length)
            {
                if (TryListMarker(lines[index].Text, out var next) && next.Indent == firstMarker.Indent)
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
                if (!IsBlankLine(lines[index].Text) &&
                    CountLeadingIndentation(lines[index].Text).Column < marker.Indent + marker.MarkerWidth &&
                    (!paragraphOpen || IsParagraphInterruptingBlockStart(lines[index].Text)))
                {
                    break;
                }

                if (IsBlankLine(lines[index].Text))
                {
                    paragraphOpen = false;
                    var afterBlankIndex = index + 1;

                    while (afterBlankIndex < lines.Length && IsBlankLine(lines[afterBlankIndex].Text))
                    {
                        afterBlankIndex++;
                    }

                    if (afterBlankIndex >= lines.Length)
                    {
                        index = afterBlankIndex;
                        break;
                    }

                    if (TryListMarker(lines[afterBlankIndex].Text, out var afterBlank) &&
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
                    if (CountLeadingIndentation(lines[afterBlankIndex].Text).Column <
                        marker.Indent + marker.MarkerWidth)
                    {
                        index = afterBlankIndex;
                        break;
                    }

                    list.IsLoose = true;
                    continuation.Add(lines[index]);
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
                        CountLeadingIndentation(lines[index].Text).Length);
                    var content = lines[index].Slice(remove);
                    continuation.Add(content);
                    paragraphOpen = !IsBlankLine(content.Text) && !IsParagraphInterruptingBlockStart(content.Text);
                }

                index++;
            }

            if (Has(MarkdownExtension.TaskLists) && TryTask(marker.Content, out var isChecked, out var taskText))
            {
                var paragraph = CreateInteractiveParagraph(
                    new CheckBox { IsChecked = isChecked },
                    taskText,
                    continuation,
                    out var consumedLineCount);
                item.Blocks.Add(paragraph);
                AddParsedBlocks(
                    item,
                    [.. continuation.Skip(consumedLineCount)],
                    ref radioGroupOrdinal,
                    blockDepth,
                    diagnostics);
            }
            else if (radioGroup is not null && TryRadio(marker.Content, out isChecked, out var radioText))
            {
                if (isChecked && selectedRadio is not null)
                {
                    selectedRadio.IsChecked = false;
                }

                var radio = new RadioButton
                {
                    GroupName = radioGroup,
                    IsChecked = isChecked
                };
                MarkdownRadioRegistry.Register(radio);
                var paragraph = CreateInteractiveParagraph(
                    radio,
                    radioText,
                    continuation,
                    out var consumedLineCount);
                item.Blocks.Add(paragraph);
                AddParsedBlocks(
                    item,
                    [.. continuation.Skip(consumedLineCount)],
                    ref radioGroupOrdinal,
                    blockDepth,
                    diagnostics);

                if (isChecked)
                {
                    selectedRadio = radio;
                }
            }
            else
            {
                continuation.Insert(0, markerLine.Slice(markerLine.Text.Length - marker.Content.Length));
                AddParsedBlocks(item, [.. continuation], ref radioGroupOrdinal, blockDepth, diagnostics);
            }

            list.Items.Add(item);

            if (endListAfterItem)
            {
                break;
            }

        }

        return list;
    }

    private DocumentParagraph CreateInteractiveParagraph(
        ControlBase control,
        string label,
        List<MarkdownSourceLine> continuation,
        out int consumedLineCount)
    {
        var paragraphLines = new List<string>();

        if (label.Length > 0)
        {
            paragraphLines.Add(label);
        }

        consumedLineCount = 0;

        while (consumedLineCount < continuation.Count &&
               !IsBlankLine(continuation[consumedLineCount].Text) &&
               !IsParagraphInterruptingBlockStart(continuation[consumedLineCount].Text))
        {
            paragraphLines.Add(continuation[consumedLineCount].Text);
            consumedLineCount++;
        }

        var paragraph = CreateParagraph(paragraphLines);
        var hasLabel = paragraph.Inlines.Count > 0;
        paragraph.Inlines.Insert(0, new DocumentInlineControl(control));

        if (hasLabel)
        {
            paragraph.Inlines.Insert(1, new DocumentTextRun(" "));
        }

        return paragraph;
    }

    private void AddParsedBlocks(
        DocumentListItem item,
        MarkdownSourceLine[] lines,
        ref int radioGroupOrdinal,
        int blockDepth,
        List<DocumentDiagnostic> diagnostics)
    {
        foreach (var block in ParseBlocks(lines, ref radioGroupOrdinal, blockDepth + 1, diagnostics))
        {
            item.Blocks.Add(block);
        }
    }

    private static void AddNestingLimitDiagnostic(
        List<DocumentDiagnostic> diagnostics,
        MarkdownSourceLine implicatedLine)
    {
        if (diagnostics.Any(static diagnostic => diagnostic.Message == _nestingLimitDiagnostic))
        {
            return;
        }

        diagnostics.Add(
            new DocumentDiagnostic(
                _nestingLimitDiagnostic,
                new DocumentSourceSpan(implicatedLine.Offset, implicatedLine.Text.Length)));
    }

    private DocumentTable ParseTable(
        MarkdownSourceLine[] lines,
        ref int index,
        IReadOnlyList<DocumentTableCellAlignment> alignments)
    {
        var table = new DocumentTable();
        var header = CreateTableRow(SplitTableRow(lines[index].Text), alignments, isHeader: true);
        table.Rows.Add(header);
        index += 2;

        while (index < lines.Length &&
               !IsBlankLine(lines[index].Text) &&
               !IsBlockStart(lines[index].Text))
        {
            table.Rows.Add(CreateTableRow(SplitTableRow(lines[index].Text), alignments, isHeader: false));
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
        var nextAngleClose = BuildNextAngleClose(source);
        var labelCloses = BuildLabelCloses(source, codeSpanEnds, nextAngleClose);
        var emphasisCloses = BuildEmphasisCloses(source);
        var strikethroughCloses = BuildStrikethroughCloses(source);
        var tokenHasAtSign = BuildTokenHasAtSign(source);

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
                TryAngleAutolink(
                    source, index, nextAngleClose, out var angleEnd, out var angleText, out var angleTarget))
            {
                Flush();
                destination.Add(new DocumentLink(angleText, angleTarget));
                index = angleEnd;
                continue;
            }

            if (!insideLink && Has(MarkdownExtension.Autolinks) &&
                TryExtendedAutolink(source, index, tokenHasAtSign, out var urlEnd, out var urlText, out var urlTarget))
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

            if (source[index] is '*' or '_')
            {
                if (TryGetEmphasisClose(
                    emphasisCloses,
                    index,
                    out var emphasisClose,
                    out var emphasisDelimiterLength))
                {
                    Flush();

                    if (emphasisDelimiterLength == 2)
                    {
                        var strong = new DocumentStrong();
                        ParseInlines(
                            source[(index + emphasisDelimiterLength)..emphasisClose],
                            strong.Inlines,
                            insideLink);
                        destination.Add(strong);
                        index = emphasisClose + emphasisDelimiterLength;
                    }
                    else
                    {
                        var emphasis = new DocumentEmphasis();
                        ParseInlines(
                            source[(index + emphasisDelimiterLength)..emphasisClose],
                            emphasis.Inlines,
                            insideLink);
                        destination.Add(emphasis);
                        index = emphasisClose + emphasisDelimiterLength;
                    }

                    continue;
                }
            }

            if (Has(MarkdownExtension.Strikethrough) && source[index] == '~')
            {
                var rejectedRunLength = CountRun(source, index, '~');

                if (rejectedRunLength >= 3)
                {
                    _ = plain.Append(source, index, rejectedRunLength);
                    index += rejectedRunLength;
                    continue;
                }
            }

            if (Has(MarkdownExtension.Strikethrough) &&
                TryStrikethrough(source, index, strikethroughCloses, out var strikeEnd, out var strikeDelimiterLength))
            {
                Flush();
                var strike = new DocumentStrikethrough();
                ParseInlines(source[(index + strikeDelimiterLength)..strikeEnd], strike.Inlines, insideLink);
                destination.Add(strike);
                index = strikeEnd + strikeDelimiterLength;
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

    private int[] BuildLabelCloses(string source, int[] codeSpanEnds, int[] nextAngleClose)
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

            if (source[index] == '<' && TryAngleAutolink(source, index, nextAngleClose, out var angleEnd, out _, out _))
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

    /// <summary>Precomputes, for every position, the index of the next unconsumed <c>&gt;</c>
    /// character at or after that position (or -1 if none remains). Angle-autolink candidates look up
    /// their closing bracket in this array in O(1) instead of calling <c>string.IndexOf(char, int)</c>
    /// at every <c>&lt;</c>, which made a long run of unmatched angle brackets quadratic.</summary>
    private int[] BuildNextAngleClose(string source)
    {
        var next = new int[source.Length + 1];
        next[source.Length] = -1;

        for (var index = source.Length - 1; index >= 0; index--)
        {
            InlineCandidateScanCount++;
            next[index] = source[index] == '>' ? index : next[index + 1];
        }

        return next;
    }

    /// <summary>Precomputes, per strikethrough delimiter length (1 or 2), the position where an active
    /// opener of that exact length is closed - matched in a single forward pass instead of the old
    /// per-candidate rescan, which made a long run of open-only strikethrough delimiters (for example
    /// <c>"~a "</c> repeated many times) quadratic.</summary>
    private Dictionary<int, int[]> BuildStrikethroughCloses(string source)
    {
        var closes = new Dictionary<int, int[]>
        {
            [1] = CreateUnresolvedIndex(source.Length),
            [2] = CreateUnresolvedIndex(source.Length)
        };
        var openers = new Dictionary<int, Stack<int>> { [1] = new(), [2] = new() };
        var index = 0;

        while (index < source.Length)
        {
            InlineCandidateScanCount++;

            if (source[index] == '\\' && index + 1 < source.Length)
            {
                index += 2;
                continue;
            }

            if (source[index] != '~')
            {
                index++;
                continue;
            }

            var runLength = CountRun(source, index, '~');

            if (runLength is 1 or 2)
            {
                var canOpen = index + runLength < source.Length && !char.IsWhiteSpace(source[index + runLength]);
                var canClose = index > 0 && !char.IsWhiteSpace(source[index - 1]);
                var stack = openers[runLength];

                if (canClose && stack.Count > 0)
                {
                    closes[runLength][stack.Pop()] = index;
                }
                else if (canOpen)
                {
                    stack.Push(index);
                }
            }

            index += runLength;
        }

        return closes;
    }

    [Pure]
    private static bool TryStrikethrough(
        string source, int index, Dictionary<int, int[]> closes, out int end, out int delimiterLength)
    {
        delimiterLength = CountRun(source, index, '~');

        if (delimiterLength is not (1 or 2) ||
            index + delimiterLength >= source.Length ||
            char.IsWhiteSpace(source[index + delimiterLength]))
        {
            end = -1;
            return false;
        }

        end = closes[delimiterLength][index];
        return end >= 0;
    }

    /// <summary>Precomputes the position and width where each emphasis opener is closed. Delimiter
    /// runs are consumed incrementally, two markers at a time when both sides permit strong emphasis
    /// and one marker otherwise. A partially consumed opener remains available for a later closer, so
    /// arbitrary run lengths peel into nested semantic nodes instead of becoming wholly literal.</summary>
    private (int[] Closes, int[] DelimiterLengths) BuildEmphasisCloses(string source)
    {
        var closes = CreateUnresolvedIndex(source.Length);
        var delimiterLengths = new int[source.Length];
        var openers = new Dictionary<char, Stack<(int Index, int Remaining)>>
        {
            ['*'] = new(),
            ['_'] = new()
        };

        var index = 0;

        while (index < source.Length)
        {
            InlineCandidateScanCount++;

            if (source[index] == '\\' && index + 1 < source.Length)
            {
                index += 2;
                continue;
            }

            if (source[index] is not ('*' or '_'))
            {
                index++;
                continue;
            }

            var marker = source[index];
            var runLength = CountRun(source, index, marker);
            var canOpen = CanOpenEmphasisRun(source, index, runLength, marker);
            var canClose = CanCloseEmphasisRun(source, index, runLength, marker);
            var stack = openers[marker];

            if (canClose && stack.Count > 0)
            {
                var opener = stack.Pop();
                var delimiterLength = opener.Remaining >= 2 && runLength >= 2 ? 2 : 1;

                // Consume the outer edge of each run. Any markers left between those edges remain
                // inside the matched slice and are parsed recursively into the next semantic layer.
                closes[opener.Index] = index + runLength - delimiterLength;
                delimiterLengths[opener.Index] = delimiterLength;
                opener.Remaining -= delimiterLength;

                if (opener.Remaining > 0)
                {
                    stack.Push(opener);
                }
            }
            else if (canOpen)
            {
                stack.Push((index, runLength));
            }

            index += runLength;
        }

        return (closes, delimiterLengths);
    }

    /// <summary>Gets the closing position and consumed delimiter width for an emphasis opener.</summary>
    /// <param name="matches">The precomputed emphasis matches.</param>
    /// <param name="index">The candidate opener position.</param>
    /// <param name="close">The matched closer position, when successful.</param>
    /// <param name="delimiterLength">The number of markers consumed on each side.</param>
    /// <returns><see langword="true"/> when <paramref name="index"/> is a matched opener.</returns>
    [Pure]
    private static bool TryGetEmphasisClose(
        (int[] Closes, int[] DelimiterLengths) matches,
        int index,
        out int close,
        out int delimiterLength)
    {
        close = matches.Closes[index];
        delimiterLength = matches.DelimiterLengths[index];
        return close >= 0;
    }

    [Pure]
    private static int[] CreateUnresolvedIndex(int length)
    {
        var index = new int[length];
        Array.Fill(index, -1);
        return index;
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
        Rune runeBefore = default;
        Rune runeAfter = default;
        var hasRuneBefore = index > 0 && TryGetRuneBefore(source, index, out runeBefore);
        var hasRuneAfter = runEnd < source.Length && TryGetRuneAt(source, runEnd, out runeAfter);
        var beforeWhitespace = index == 0 || (hasRuneBefore && Rune.IsWhiteSpace(runeBefore));
        var afterWhitespace = runEnd >= source.Length || (hasRuneAfter && Rune.IsWhiteSpace(runeAfter));
        var beforePunctuation = hasRuneBefore && IsPunctuationOrSymbol(runeBefore);
        var afterPunctuation = hasRuneAfter && IsPunctuationOrSymbol(runeAfter);
        var leftFlanking = !afterWhitespace && (!afterPunctuation || beforeWhitespace || beforePunctuation);
        var rightFlanking = !beforeWhitespace && (!beforePunctuation || afterWhitespace || afterPunctuation);
        return (leftFlanking, rightFlanking, beforePunctuation, afterPunctuation);
    }

    /// <summary>Attempts to read the <see cref="Rune"/> immediately preceding <paramref name="index"/>,
    /// tolerating an unpaired UTF-16 surrogate at that position by reporting failure instead of
    /// throwing.</summary>
    [Pure]
    private static bool TryGetRuneBefore(string source, int index, out Rune rune)
    {
        var runeStart = index - 1;

        if (runeStart > 0 && char.IsLowSurrogate(source[runeStart]) && char.IsHighSurrogate(source[runeStart - 1]))
        {
            runeStart--;
        }

        return TryGetRuneAt(source, runeStart, out rune);
    }

    /// <summary>Attempts to read the <see cref="Rune"/> starting at <paramref name="index"/>, tolerating an
    /// unpaired UTF-16 surrogate at that position (a lone low surrogate, or a high surrogate not followed
    /// by a low surrogate) by reporting failure instead of throwing.</summary>
    [Pure]
    private static bool TryGetRuneAt(string source, int index, out Rune rune)
    {
        if (char.IsSurrogate(source[index]) &&
            !(char.IsHighSurrogate(source[index]) && index + 1 < source.Length && char.IsLowSurrogate(source[index + 1])))
        {
            rune = default;
            return false;
        }

        rune = Rune.GetRuneAt(source, index);
        return true;
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
        int[] nextAngleClose,
        out int end,
        out string text,
        out string target)
    {
        var close = nextAngleClose[index + 1];

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

    /// <summary>Reports whether <paramref name="value"/> could start the local part of an email
    /// address per <see cref="IsCommonMarkEmailAutolink"/> - the exact character set that method's own
    /// local-part loop accepts. Used only as a cheap, necessary-condition rejection before
    /// <see cref="TryExtendedAutolink"/> pays for its full candidate scan.</summary>
    [Pure]
    private static bool IsEmailAutolinkLocalPartCharacter(char value) =>
        char.IsAsciiLetterOrDigit(value) ||
        value is '.' or '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or
            '/' or '=' or '?' or '^' or '_' or '`' or '{' or '|' or '}' or '~' or '-';

    /// <summary>Precomputes, for every source position, whether the maximal run of non-whitespace,
    /// non-control, non-angle-bracket characters containing that position (the same token boundary
    /// <see cref="TryExtendedAutolink"/>'s own scan stops at) contains an <c>@</c> anywhere. A run of
    /// characters that are individually email-local-part-shaped (for example many consecutive
    /// <c>_</c>) but never form a valid email autolink would otherwise still make
    /// <see cref="TryExtendedAutolink"/> restart its own expensive token scan from every position in
    /// the run: each character both satisfies <see cref="IsEmailAutolinkLocalPartCharacter"/> and is
    /// itself accepted as a valid preceding character for the next position's candidate check,
    /// reproducing the same quadratic behavior <see cref="BuildStrikethroughCloses"/> and
    /// <see cref="BuildNextAngleClose"/> already guard against for their own delimiters. Since a valid
    /// email autolink always requires an <c>@</c> in this same token, rejecting a candidate outright
    /// when its token has none bounds the expensive scan to once per token instead of once per
    /// character.</summary>
    private bool[] BuildTokenHasAtSign(string source)
    {
        var hasAt = new bool[source.Length];
        var tokenStart = 0;
        var containsAt = false;

        for (var index = 0; index <= source.Length; index++)
        {
            InlineCandidateScanCount++;
            var isTokenCharacter = index < source.Length &&
                source[index] is > ' ' and not ('\u007f' or '<' or '>');

            if (isTokenCharacter)
            {
                containsAt |= source[index] == '@';
                continue;
            }

            if (containsAt)
            {
                Array.Fill(hasAt, true, tokenStart, index - tokenStart);
            }

            tokenStart = index + 1;
            containsAt = false;
        }

        return hasAt;
    }

    private bool TryExtendedAutolink(
        string source,
        int index,
        bool[] tokenHasAtSign,
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

        // Cheap shape check before the expensive scan below: a candidate that is neither a
        // recognized URL/www prefix nor shaped like the start of an email local part can never
        // succeed, so reject it in O(1) instead of paying for the token scan and bracket count. This
        // is what keeps a long run of unmatched punctuation (for example many consecutive '(') from
        // costing an O(remaining length) scan at every single position.
        if (!isUrl && !isWww && !IsEmailAutolinkLocalPartCharacter(source[index]))
        {
            end = -1;
            text = string.Empty;
            target = string.Empty;
            return false;
        }

        // A non-URL, non-www candidate can only ever succeed as an email autolink, which requires an
        // '@' somewhere in this same token. Without this, a long run of characters that are each
        // individually email-local-part-shaped (for example many consecutive '_') would still pay for
        // the full token scan below from every position in the run, since each character also
        // satisfies the preceding-character check above for the next position - reproducing the same
        // quadratic cost the shape check just above was meant to remove.
        if (!isUrl && !isWww && !tokenHasAtSign[index])
        {
            end = -1;
            text = string.Empty;
            target = string.Empty;
            return false;
        }

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
