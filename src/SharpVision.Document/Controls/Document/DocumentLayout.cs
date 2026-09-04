// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

using System.Runtime.InteropServices;

using SharpVision.Text;

using UnicodeWidth = Width;

/// <summary>Projects one <see cref="Document"/>'s node tree into positioned, styled lines.</summary>
/// <remarks>
/// <para>
/// The projection is a pure function of the tree, the content width, the ambiguous-width policy, and
/// the glyph family. It holds no reference to the document and produces no side effects, so it can be
/// rebuilt at any point in the layout pass - including the extra probe the scrolling container runs
/// to decide whether a scrollbar is needed.
/// </para>
/// <para>
/// Output is deliberately style-free beyond a <see cref="DocumentFaceKind"/> per run. Colors and
/// attributes resolve during painting, which is what lets a theme swap restyle the document without
/// a relayout.
/// </para>
/// </remarks>
internal sealed class DocumentLayout
{
    private static readonly DocumentFlowToken[] _emptyTokens = [];

    /// <summary>The cell advance one tab contributes.</summary>
    /// <remarks>
    /// Fixed rather than measured to the next stop because a tab's column is not knowable while
    /// tokenizing: wrapping decides the column, and wrapping happens afterwards. A constant advance
    /// keeps a tab's width identical wherever the line happens to break.
    /// </remarks>
    private const int _tabAdvance = 4;

    /// <summary>The gap between the widest marker in a list and its items' content.</summary>
    private const int _markerGap = 1;

    /// <summary>The indent a block quote adds, leaving room for its bar plus one blank column.</summary>
    private const int _quoteIndent = 2;

    private readonly List<DocumentFlowToken> _tokens = [];
    private readonly List<DocumentControlPlacement> _controlPlacements = [];
    private readonly List<DocumentLink> _links = [];
    private readonly List<DocumentLinkRegion> _linkRegions = [];
    private readonly List<DocumentMarkerPlacement> _markers = [];
    private readonly List<DocumentParsedRun> _parsedRuns = [];
    private readonly List<DocumentQuoteBar> _quoteBars = [];
    private readonly List<DocumentVisualLine> _lines = [];
    private readonly List<DocumentVisualRun> _runs = [];
    private readonly DocumentSelectionBuilder _selectionBuilder = new();

    private Ambiguous _ambiguousWidth;
    private DocumentGlyphs _glyphs;
    private int _width;

    #region Projection

    /// <summary>Gets the laid-out lines in top-to-bottom order.</summary>
    public IReadOnlyList<DocumentVisualLine> Lines => _lines;

    /// <summary>Gets every link in document order.</summary>
    public IReadOnlyList<DocumentLink> Links => _links;

    /// <summary>Gets every activatable link region.</summary>
    public IReadOnlyList<DocumentLinkRegion> LinkRegions => _linkRegions;

    /// <summary>Gets retained-control rectangles in content coordinates.</summary>
    public IReadOnlyList<DocumentControlPlacement> ControlPlacements => _controlPlacements;

    /// <summary>Gets the immutable semantic selection projection built beside the visual layout.</summary>
    public TextSelectionMap SelectionMap { get; private set; } = TextSelectionMap.Empty;

    /// <summary>Refreshes embedded glyph rectangles after retained children receive final bounds.</summary>
    /// <param name="contentOrigin">The absolute origin of document content coordinates.</param>
    /// <returns>True when every refreshed source kept the measured semantic text; otherwise false.</returns>
    internal bool RefreshSelectionGeometry(Point contentOrigin)
    {
        var semanticTextIsCurrent = _selectionBuilder.RefreshControlGeometry();
        var committedPlacements = new DocumentControlPlacement[_controlPlacements.Count];

        for (var index = 0; index < _controlPlacements.Count; index++)
        {
            var control = _controlPlacements[index].Control;
            committedPlacements[index] = new DocumentControlPlacement(
                control,
                new Rect(
                    SaturatingSubtract(control.Bounds.X, contentOrigin.X),
                    SaturatingSubtract(control.Bounds.Y, contentOrigin.Y),
                    control.Bounds.Width,
                    control.Bounds.Height));
        }

        SelectionMap = _selectionBuilder.Build(
            _lines,
            _runs,
            _parsedRuns,
            committedPlacements,
            _ambiguousWidth);
        return semanticTextIsCurrent;
    }

    /// <summary>Gets the widest line's cell width.</summary>
    public int MaxCells { get; private set; }

    /// <summary>Gets the runs on one line.</summary>
    /// <param name="line">The line to read.</param>
    /// <returns>The line's runs, in left-to-right order.</returns>
    [Pure]
    public ReadOnlySpan<DocumentVisualRun> RunsOf(DocumentVisualLine line) =>
        CollectionsMarshal.AsSpan(_runs).Slice(line.RunStart, line.RunCount);

    /// <summary>Gets the parsed display text one text run slices.</summary>
    /// <param name="run">The text run.</param>
    /// <returns>The parsed run supplying the run's characters.</returns>
    [Pure]
    public DocumentParsedRun ParsedRunOf(DocumentVisualRun run) => _parsedRuns[run.ParsedRunIndex];

    /// <summary>Gets every block quote's bar range.</summary>
    public IReadOnlyList<DocumentQuoteBar> QuoteBars => _quoteBars;

    /// <summary>Gets every list marker placement.</summary>
    public IReadOnlyList<DocumentMarkerPlacement> Markers => _markers;

    #endregion

    #region Building

    /// <summary>Rebuilds the projection for one document tree.</summary>
    /// <param name="blocks">The non-null root block sequence.</param>
    /// <param name="width">The available content width in cells.</param>
    /// <param name="ambiguousWidth">The live ambiguous-width policy used to measure and repair glyphs.</param>
    /// <param name="glyphs">The glyph family used for bullets, quote bars, and rules.</param>
    public void Build(
        DocumentBlockCollection blocks,
        int width,
        Ambiguous ambiguousWidth,
        DocumentGlyphs glyphs)
    {
        Debug.Assert(blocks is not null, "A projection always has a block sequence to build from.");

        _tokens.Clear();
        _controlPlacements.Clear();
        _links.Clear();
        _linkRegions.Clear();
        _markers.Clear();
        _parsedRuns.Clear();
        _quoteBars.Clear();
        _lines.Clear();
        _runs.Clear();
        _selectionBuilder.Reset();

        _ambiguousWidth = ambiguousWidth;
        _glyphs = glyphs;
        MaxCells = 0;
        _width = Math.Max(0, width);

        EmitBlocks(blocks, indent: 0, spacing: 1, DocumentFaceKind.Body, listDepth: 0, foregroundOverride: null);
        SelectionMap = _selectionBuilder.Build(
            _lines,
            _runs,
            _parsedRuns,
            _controlPlacements,
            _ambiguousWidth);
    }

    private void EmitBlocks(
        DocumentBlockCollection blocks,
        int indent,
        int spacing,
        DocumentFaceKind face,
        int listDepth,
        DocumentFaceKind? foregroundOverride,
        bool beginFirst = true)
    {
        var emittedBlocks = 0;
        var previousBlockIsEmptyList = false;

        for (var index = 0; index < blocks.Count; index++)
        {
            if (blocks[index] is DocumentBlockControl { Control.Visibility: Visibility.Collapsed })
            {
                continue;
            }

            var currentBlockIsEmptyList = blocks[index] is DocumentList { Items.Count: 0 };

            if (emittedBlocks > 0 && !previousBlockIsEmptyList && !currentBlockIsEmptyList)
            {
                for (var blank = 0; blank < spacing; blank++)
                {
                    EmitBlankLine(indent);
                }
            }

            if (beginFirst || emittedBlocks > 0)
            {
                _selectionBuilder.BeginBlockValue();
            }

            EmitBlock(blocks[index], indent, face, listDepth, foregroundOverride);
            emittedBlocks++;
            previousBlockIsEmptyList = currentBlockIsEmptyList;
        }
    }

    private void EmitBlock(
        DocumentBlock block,
        int indent,
        DocumentFaceKind face,
        int listDepth,
        DocumentFaceKind? foregroundOverride)
    {
        switch (block)
        {
            case DocumentParagraph paragraph:
                EmitFlow(paragraph.Inlines, indent, face, foregroundOverride);
                break;
            case DocumentHeading heading:
                EmitFlow(
                    heading.Inlines,
                    indent,
                    heading.Level <= 2 ? DocumentFaceKind.Heading : DocumentFaceKind.MinorHeading,
                    foregroundOverride);
                break;
            case DocumentList list:
                EmitList(list, indent, face, listDepth, foregroundOverride);
                break;
            case DocumentBlockQuote quote:
                EmitQuote(quote, indent, listDepth, foregroundOverride);
                break;
            case DocumentCodeBlock code:
                EmitCode(code, indent, foregroundOverride);
                break;
            case DocumentSeparator:
                EmitRule(indent, foregroundOverride);
                break;
            case DocumentBlockControl control:
                EmitControlBlock(control, indent);
                break;
            case DocumentCallout callout:
                EmitCallout(callout, indent, listDepth);
                break;
            case DocumentTable table:
                EmitTable(table, indent, foregroundOverride);
                break;
            default:
                throw new UnreachableException(
                    "DocumentBlock's hierarchy is closed to this assembly, so every block kind is handled.");
        }
    }

    private void EmitList(
        DocumentList list,
        int indent,
        DocumentFaceKind face,
        int listDepth,
        DocumentFaceKind? foregroundOverride)
    {
        if (list.Items.Count == 0)
        {
            EmitBlankLine(indent);
            return;
        }

        // The gutter is measured from the widest marker the list will actually draw rather than
        // assumed, so a list that reaches "10." or "100." keeps its content aligned and never paints
        // a marker over its own text.
        var gutter = 0;
        var markers = new string[list.Items.Count];

        for (var index = 0; index < list.Items.Count; index++)
        {
            markers[index] = ResolveMarker(list.Kind, list.Start, index, listDepth);
            gutter = Math.Max(gutter, MeasureCells(markers[index]));
        }

        gutter = gutter.Add(_markerGap);

        for (var index = 0; index < list.Items.Count; index++)
        {
            if (index > 0 && list.IsLoose)
            {
                EmitBlankLine(indent);
            }

            var firstLine = _lines.Count;
            _selectionBuilder.BeginBlockValue();
            var semanticMarker = list.Kind == DocumentListKind.Bulleted
                ? "- "
                : FormattableString.Invariant($"{(long) list.Start + index}. ");
            _selectionBuilder.AppendListMarker(
                semanticMarker,
                markers[index],
                firstLine,
                indent,
                gutter,
                _ambiguousWidth);

            // An item's own blocks are tight: CommonMark places an item's paragraph immediately
            // above its nested list with no blank line between them.
            EmitBlocks(
                list.Items[index].Blocks,
                indent.Add(gutter),
                spacing: 0,
                face,
                listDepth + 1,
                foregroundOverride,
                beginFirst: false);

            if (_lines.Count == firstLine)
            {
                EmitBlankLine(indent.Add(gutter));
            }

            _markers.Add(new DocumentMarkerPlacement(firstLine, indent, markers[index], foregroundOverride));
        }
    }

    private void EmitQuote(
        DocumentBlockQuote quote,
        int indent,
        int listDepth,
        DocumentFaceKind? foregroundOverride)
    {
        var firstLine = _lines.Count;
        EmitBlocks(
            quote.Blocks,
            indent.Add(_quoteIndent),
            spacing: 1,
            DocumentFaceKind.Quote,
            listDepth,
            foregroundOverride,
            beginFirst: false);

        if (_lines.Count == firstLine)
        {
            EmitBlankLine(indent.Add(_quoteIndent));
        }

        _quoteBars.Add(new DocumentQuoteBar(
            firstLine,
            _lines.Count - 1,
            indent,
            DocumentFaceKind.Quote,
            foregroundOverride));
    }

    private void EmitCode(DocumentCodeBlock code, int indent, DocumentFaceKind? foregroundOverride)
    {
        var text = code.Text;
        var start = 0;
        var emitted = false;
        var semanticLine = 0;

        while (start <= text.Length)
        {
            var end = text.AsSpan(start).IndexOfAny('\r', '\n');
            var lineEnd = end < 0 ? text.Length : start + end;

            if (semanticLine > 0)
            {
                _selectionBuilder.AppendHardBreak();
            }

            EmitCodeLine(text.AsSpan(start, lineEnd - start), indent, foregroundOverride);
            emitted = true;
            semanticLine++;

            if (end < 0)
            {
                break;
            }

            // A CRLF pair is one break, not two.
            start = lineEnd + 1;

            if (text[lineEnd] == '\r' && start < text.Length && text[start] == '\n')
            {
                start++;
            }
        }

        if (!emitted)
        {
            EmitBlankLine(indent);
        }
    }

    private void EmitCodeLine(
        ReadOnlySpan<char> line,
        int indent,
        DocumentFaceKind? foregroundOverride)
    {
        var display = ExpandTabs(line);
        var parsedRunIndex = _parsedRuns.Count;
        _parsedRuns.Add(new DocumentParsedRun(display, []));
        _selectionBuilder.AppendCodeLine(line, display, parsedRunIndex, _ambiguousWidth);

        var cells = MeasureCells(display);
        var runStart = _runs.Count;

        if (display.Length > 0)
        {
            _runs.Add(DocumentVisualRun.ForText(
                indent,
                cells,
                parsedRunIndex,
                0,
                display.Length,
                DocumentFaceKind.Code,
                linkIndex: -1,
                foregroundOverride));
        }

        CommitLine(runStart, indent.Add(cells));
    }

    private void EmitRule(int indent, DocumentFaceKind? foregroundOverride)
    {
        var cells = Math.Max(0, _width - indent);
        var runStart = _runs.Count;

        if (cells > 0)
        {
            _runs.Add(DocumentVisualRun.ForRepeat(
                indent,
                cells,
                Resolve(_glyphs.RuleGlyph),
                DocumentFaceKind.Rule,
                foregroundOverride));
        }

        CommitLine(runStart, indent.Add(cells));
    }

    private void EmitBlankLine(int indent) => CommitLine(_runs.Count, indent);

    private void EmitControlBlock(DocumentBlockControl block, int indent)
    {
        if (block.Control.IsDisposed)
        {
            EmitBlankLine(indent);
            return;
        }

        _selectionBuilder.AppendControl(block.Control);

        var desired = block.Control.DesiredSize;
        var height = Math.Max(1, desired.Height);
        _controlPlacements.Add(new DocumentControlPlacement(
            block.Control,
            new Rect(indent, _lines.Count, desired.Width, desired.Height)));

        for (var line = 0; line < height; line++)
        {
            CommitLine(_runs.Count, indent.Add(desired.Width));
        }
    }

    private void EmitCallout(
        DocumentCallout callout,
        int indent,
        int listDepth)
    {
        var firstLine = _lines.Count;
        var (bodyFace, titleFace) = CalloutFaces(callout.Kind);
        var title = callout.Title.Length == 0
            ? FormattableString.Invariant($"[{callout.Kind}]")
            : FormattableString.Invariant($"[{callout.Kind}] {callout.Title}");
        EmitLiteralFlow(title, indent.Add(_quoteIndent), titleFace, foregroundOverride: null);
        EmitBlocks(callout.Blocks, indent.Add(_quoteIndent), spacing: 1, bodyFace, listDepth, bodyFace);
        _quoteBars.Add(new DocumentQuoteBar(
            firstLine,
            _lines.Count - 1,
            indent,
            bodyFace,
            foregroundOverride: null));
    }

    [Pure]
    private static (DocumentFaceKind Body, DocumentFaceKind Title) CalloutFaces(string kind) =>
        kind.ToUpperInvariant() switch
        {
            "NOTE" => (DocumentFaceKind.CalloutNote, DocumentFaceKind.CalloutNoteTitle),
            "TIP" => (DocumentFaceKind.CalloutTip, DocumentFaceKind.CalloutTipTitle),
            "IMPORTANT" => (DocumentFaceKind.CalloutImportant, DocumentFaceKind.CalloutImportantTitle),
            "WARNING" => (DocumentFaceKind.CalloutWarning, DocumentFaceKind.CalloutWarningTitle),
            "CAUTION" => (DocumentFaceKind.CalloutCaution, DocumentFaceKind.CalloutCautionTitle),
            _ => (DocumentFaceKind.Callout, DocumentFaceKind.CalloutTitle)
        };

    private void EmitTable(
        DocumentTable table,
        int indent,
        DocumentFaceKind? foregroundOverride)
    {
        var columnCount = 0;

        foreach (var row in table.Rows)
        {
            columnCount = Math.Max(columnCount, row.Cells.Count);
        }

        if (columnCount == 0)
        {
            EmitBlankLine(indent);
            return;
        }

        var widths = new int[columnCount];
        var cells = new DocumentFlowToken[table.Rows.Count][][];

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            cells[rowIndex] = new DocumentFlowToken[row.Cells.Count][];

            if (rowIndex > 0)
            {
                _selectionBuilder.AppendHardBreak();
            }

            for (var column = 0; column < row.Cells.Count; column++)
            {
                if (column > 0)
                {
                    _ = _selectionBuilder.AppendLiteral("\t");
                }

                _tokens.Clear();
                Tokenize(
                    row.Cells[column].Inlines,
                    row.IsHeader ? DocumentFaceKind.TableHeader : DocumentFaceKind.Table,
                    row.IsHeader ? TerminalAttributes.Bold : TerminalAttributes.None,
                    linkIndex: -1,
                    linkTarget: null,
                    normalizeLineBreaks: true);
                cells[rowIndex][column] = [.. _tokens];
                widths[column] = Math.Max(widths[column], MeasureTableCell(cells[rowIndex][column]));
            }
        }

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            var face = row.IsHeader ? DocumentFaceKind.TableHeader : DocumentFaceKind.Table;
            var runStart = _runs.Count;
            var position = indent;
            AppendLiteral("|", ref position, face, foregroundOverride, isBold: row.IsHeader);

            for (var column = 0; column < columnCount; column++)
            {
                var tokens = column < cells[rowIndex].Length
                    ? cells[rowIndex][column]
                    : _emptyTokens;
                var alignment = column < row.Cells.Count
                    ? row.Cells[column].Alignment
                    : DocumentTableCellAlignment.Left;
                var missing = Math.Max(0, widths[column] - MeasureTableCell(tokens));
                var leading = alignment switch
                {
                    DocumentTableCellAlignment.Left => 0,
                    DocumentTableCellAlignment.Center => missing / 2,
                    DocumentTableCellAlignment.Right => missing,
                    _ => 0
                };

                AppendSpaces(leading.Add(1), ref position, face, foregroundOverride);

                foreach (var token in tokens)
                {
                    AppendTableToken(token, ref position, foregroundOverride);
                }

                AppendSpaces((missing - leading).Add(1), ref position, face, foregroundOverride);
                AppendLiteral("|", ref position, face, foregroundOverride, isBold: row.IsHeader);
            }

            CommitLine(runStart, position);
        }
    }

    private void AppendLiteral(
        string text,
        ref int column,
        DocumentFaceKind face,
        DocumentFaceKind? foregroundOverride,
        bool isBold)
    {
        if (column == int.MaxValue)
        {
            return;
        }

        var parsedRunIndex = _parsedRuns.Count;
        var spans = isBold && text.Length > 0
            ? (StyleSpan[])
            [
                new StyleSpan(
                    0,
                    text.Length,
                    foreground: null,
                    background: null,
                    TerminalAttributes.Bold,
                    Underline.None,
                    underlineColor: null,
                    link: null)
            ]
            : [];
        _parsedRuns.Add(new DocumentParsedRun(text, spans));
        var cells = MeasureCells(text);
        _runs.Add(DocumentVisualRun.ForText(
            column,
            cells,
            parsedRunIndex,
            0,
            text.Length,
            face,
            linkIndex: -1,
            foregroundOverride));
        column = column.Add(cells);
    }

    private void AppendSpaces(
        int cells,
        ref int column,
        DocumentFaceKind face,
        DocumentFaceKind? foregroundOverride)
    {
        if (cells <= 0)
        {
            return;
        }

        var drawableCells = Math.Min(cells, int.MaxValue - column);

        if (drawableCells > 0)
        {
            _runs.Add(DocumentVisualRun.ForRepeat(
                column,
                drawableCells,
                new Rune(' '),
                face,
                foregroundOverride));
        }

        column = column.Add(cells);
    }

    private void AppendTableToken(
        DocumentFlowToken token,
        ref int column,
        DocumentFaceKind? foregroundOverride)
    {
        if (column == int.MaxValue)
        {
            return;
        }

        if (token.Kind == DocumentFlowTokenKind.Break)
        {
            _runs.Add(DocumentVisualRun.ForRepeat(
                column,
                1,
                new Rune(' '),
                DocumentFaceKind.Table,
                foregroundOverride,
                token.SemanticRange));
            column = column.Add(1);
            return;
        }

        _runs.Add(token.Kind == DocumentFlowTokenKind.Control
            ? DocumentVisualRun.ForControl(column, token.Cells, token.Control!)
            : token.Glyph.Value != 0
                ? DocumentVisualRun.ForRepeat(
                    column,
                    token.Cells,
                    token.Glyph,
                    token.Face,
                    foregroundOverride,
                    token.SemanticRange,
                    token.ParsedRunIndex,
                    token.Offset,
                    token.Length,
                    token.LinkIndex)
                : token.ParsedRunIndex >= 0
                ? DocumentVisualRun.ForText(
                    column,
                    token.Cells,
                    token.ParsedRunIndex,
                    token.Offset,
                    token.Length,
                    token.Face,
                    token.LinkIndex,
                    foregroundOverride)
                : throw new UnreachableException("A non-control table token must provide text or a repeated glyph."));
        column = column.Add(token.Cells);
    }

    [Pure]
    private static int MeasureTableCell(IEnumerable<DocumentFlowToken> tokens)
    {
        var cells = 0;

        foreach (var token in tokens)
        {
            cells = cells.Add(token.Kind == DocumentFlowTokenKind.Break ? 1 : token.Cells);
        }

        return cells;
    }

    #endregion

    #region Inline flow

    private void EmitFlow(
        DocumentInlineCollection inlines,
        int indent,
        DocumentFaceKind face,
        DocumentFaceKind? foregroundOverride)
    {
        Tokenize(inlines, face);
        WrapTokens(indent, foregroundOverride);
    }

    private void EmitLiteralFlow(
        string text,
        int indent,
        DocumentFaceKind face,
        DocumentFaceKind? foregroundOverride)
    {
        _tokens.Clear();
        var parsedRunIndex = _parsedRuns.Count;
        _parsedRuns.Add(new DocumentParsedRun(text, []));
        _selectionBuilder.AppendParsedRun(parsedRunIndex, text);
        TokenizeText(text, parsedRunIndex, face, linkIndex: -1);
        WrapTokens(indent, foregroundOverride);
    }

    private void Tokenize(DocumentInlineCollection inlines, DocumentFaceKind face)
    {
        _tokens.Clear();
        Tokenize(
            inlines,
            face,
            TerminalAttributes.None,
            linkIndex: -1,
            linkTarget: null,
            normalizeLineBreaks: false);
    }

    private void Tokenize(
        DocumentInlineCollection inlines,
        DocumentFaceKind face,
        TerminalAttributes semanticAttributes,
        int linkIndex,
        string? linkTarget,
        bool normalizeLineBreaks)
    {

        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case DocumentTextRun run:
                    {
                        var spans = TextMarkup.Parse(run.Text.AsSpan(), out var display);
                        var parsedRunIndex = _parsedRuns.Count;
                        _parsedRuns.Add(new DocumentParsedRun(
                            display,
                            ApplySemanticAttributes(spans, semanticAttributes, linkTarget)));
                        _selectionBuilder.AppendParsedRun(parsedRunIndex, display, normalizeLineBreaks);
                        TokenizeText(display, parsedRunIndex, face, linkIndex);
                        break;
                    }

                case DocumentCodeSpan code:
                    {
                        var parsedRunIndex = _parsedRuns.Count;
                        _parsedRuns.Add(new DocumentParsedRun(
                            code.Text,
                            CreateLiteralSpans(code.Text, semanticAttributes, linkTarget)));
                        _selectionBuilder.AppendParsedRun(parsedRunIndex, code.Text, normalizeLineBreaks);
                        TokenizeText(code.Text, parsedRunIndex, DocumentFaceKind.Code, linkIndex);
                        break;
                    }

                case DocumentEmphasis emphasis:
                    Tokenize(
                        emphasis.Inlines,
                        face,
                        semanticAttributes | TerminalAttributes.Italic,
                        linkIndex,
                        linkTarget,
                        normalizeLineBreaks);
                    break;

                case DocumentStrong strong:
                    Tokenize(
                        strong.Inlines,
                        face,
                        semanticAttributes | TerminalAttributes.Bold,
                        linkIndex,
                        linkTarget,
                        normalizeLineBreaks);
                    break;

                case DocumentStrikethrough strikethrough:
                    Tokenize(
                        strikethrough.Inlines,
                        face,
                        semanticAttributes | TerminalAttributes.Strike,
                        linkIndex,
                        linkTarget,
                        normalizeLineBreaks);
                    break;

                case DocumentLink link:
                    {
                        var nestedLinkIndex = _links.Count;
                        _links.Add(link);
                        var firstToken = _tokens.Count;
                        Tokenize(
                            link.Inlines,
                            DocumentFaceKind.Link,
                            semanticAttributes,
                            nestedLinkIndex,
                            link.Target,
                            normalizeLineBreaks);

                        if (!_tokens.Skip(firstToken).Any(
                            token => token.LinkIndex == nestedLinkIndex && token.Cells > 0))
                        {
                            _links.RemoveAt(nestedLinkIndex);
                        }

                        break;
                    }

                case DocumentSoftBreak:
                    var softBreakRange = _selectionBuilder.AppendSoftBreak();
                    const string softBreakDisplay = " ";
                    var softBreakRunIndex = _parsedRuns.Count;
                    _parsedRuns.Add(new DocumentParsedRun(
                        softBreakDisplay,
                        CreateLiteralSpans(softBreakDisplay, semanticAttributes, linkTarget)));
                    _tokens.Add(DocumentFlowToken.ForBlank(
                        1,
                        face,
                        linkIndex,
                        softBreakRange,
                        softBreakRunIndex,
                        offset: 0,
                        length: 1));
                    break;

                case DocumentLineBreak:
                    if (normalizeLineBreaks)
                    {
                        _ = _selectionBuilder.AppendSoftBreak();
                    }
                    else
                    {
                        _selectionBuilder.AppendHardBreak();
                    }

                    _tokens.Add(DocumentFlowToken.ForBreak());
                    break;

                case DocumentInlineControl control:
                    if (control.Control.IsDisposed || control.Control.Visibility == Visibility.Collapsed)
                    {
                        break;
                    }

                    // A detached control has no committed measurement yet, so its default zero
                    // height cannot establish that it violates the one-row inline contract.
                    // Semantic projection must still include its selectable text before layout;
                    // once measured, any genuinely multi-row control remains invalid here.
                    if (control.Control.DesiredSize.Height > 1)
                    {
                        throw new InvalidOperationException(
                            "An inline control must resolve to exactly one cell of height. Use DocumentBlockControl for taller content.");
                    }

                    _selectionBuilder.AppendControl(control.Control);
                    _tokens.Add(DocumentFlowToken.ForControl(control.Control, control.Control.DesiredSize.Width));
                    break;

                default:
                    throw new UnreachableException(
                        "DocumentInline's hierarchy is closed to this assembly, so every inline kind is handled.");
            }
        }
    }

    [Pure]
    private static StyleSpan[] ApplySemanticAttributes(
        StyleSpan[] spans,
        TerminalAttributes semanticAttributes,
        string? linkTarget)
    {
        if ((semanticAttributes == TerminalAttributes.None && linkTarget is null) || spans.Length == 0)
        {
            return spans;
        }

        var styled = new StyleSpan[spans.Length];

        for (var index = 0; index < spans.Length; index++)
        {
            var span = spans[index];
            styled[index] = new StyleSpan(
                span.Offset,
                span.Length,
                span.Foreground,
                span.Background,
                span.Attributes | semanticAttributes,
                span.Underline,
                span.UnderlineColor,
                linkTarget ?? span.Link);
        }

        return styled;
    }

    [Pure]
    private static StyleSpan[] CreateLiteralSpans(
        string text,
        TerminalAttributes semanticAttributes,
        string? linkTarget) => text.Length == 0
        ? []
        :
        [
            new StyleSpan(
                0,
                text.Length,
                foreground: null,
                background: null,
                semanticAttributes,
                Underline.None,
                underlineColor: null,
                linkTarget)
        ];

    private void TokenizeText(string display, int parsedRunIndex, DocumentFaceKind face, int linkIndex)
    {
        var start = 0;
        var length = 0;
        var isSpace = false;

        void Flush(List<DocumentFlowToken> tokens, Ambiguous ambiguous)
        {
            if (length == 0)
            {
                return;
            }

            tokens.Add(DocumentFlowToken.ForText(
                isSpace ? DocumentFlowTokenKind.Space : DocumentFlowTokenKind.Word,
                parsedRunIndex,
                start,
                length,
                UnicodeWidth.Measure(display.AsSpan(start, length), ambiguous).Cells,
                face,
                linkIndex));
            length = 0;
        }

        foreach (var grapheme in Graphemes.Enumerate(display))
        {
            var offset = grapheme.Offset;

            // A line break inside a run is a hard break, and a tab is a fixed blank advance. Neither
            // may reach the canvas as text: Canvas.Draw would move the cursor itself and desynchronize
            // every column this projection computed.
            if (grapheme.Length == 1 && display[offset] is '\n' or '\r')
            {
                Flush(_tokens, _ambiguousWidth);

                // A CRLF pair segments as one cluster, so a lone '\r' here is a break of its own.
                _tokens.Add(DocumentFlowToken.ForBreak());
                continue;
            }

            if (grapheme.Length == 2 && display[offset] == '\r' && display[offset + 1] == '\n')
            {
                Flush(_tokens, _ambiguousWidth);
                _tokens.Add(DocumentFlowToken.ForBreak());
                continue;
            }

            if (grapheme.Length == 1 && display[offset] == '\t')
            {
                Flush(_tokens, _ambiguousWidth);
                _tokens.Add(DocumentFlowToken.ForBlank(
                    _tabAdvance,
                    face,
                    linkIndex,
                    _selectionBuilder.ParsedRangeOf(parsedRunIndex, offset, grapheme.Length),
                    parsedRunIndex,
                    offset,
                    grapheme.Length));
                continue;
            }

            // NBSP and narrow NBSP are Unicode glue, while word joiner is not whitespace at all.
            // Keeping all three inside the surrounding word prevents a wrap opportunity that would
            // violate their explicit non-breaking contract.
            var graphemeIsSpace = grapheme.Length == 1 &&
                                  char.IsWhiteSpace(display[offset]) &&
                                  display[offset] is not ('\u00a0' or '\u202f');

            if (length == 0)
            {
                start = offset;
                isSpace = graphemeIsSpace;
            }
            else if (graphemeIsSpace != isSpace)
            {
                Flush(_tokens, _ambiguousWidth);
                start = offset;
                isSpace = graphemeIsSpace;
            }

            length = offset + grapheme.Length - start;
        }

        Flush(_tokens, _ambiguousWidth);
    }

    private void WrapTokens(int indent, DocumentFaceKind? foregroundOverride)
    {
        // A degenerate width still has to make progress, so at least one cell is always available;
        // an over-wide token then simply overflows its line rather than looping forever.
        var available = Math.Max(1, SaturatingSubtract(_width, indent));
        var runStart = _runs.Count;
        var column = indent;
        var lineWidth = 0;
        var hasContent = false;
        var wrapped = false;

        foreach (var token in _tokens)
        {
            if (token.Kind == DocumentFlowTokenKind.Break)
            {
                CommitLine(runStart, indent.Add(lineWidth));
                runStart = _runs.Count;
                column = indent;
                lineWidth = 0;
                hasContent = false;
                wrapped = false;
                continue;
            }

            // Whitespace is dropped only where a wrap put it at the start of a continuation line.
            // Leading whitespace the author actually typed - at the start of a paragraph, or right
            // after a hard break - is content and survives.
            if (token.Kind == DocumentFlowTokenKind.Space && !hasContent && wrapped)
            {
                continue;
            }

            if (hasContent && lineWidth.Add(token.Cells) > available)
            {
                CommitLine(runStart, indent.Add(lineWidth));
                runStart = _runs.Count;
                column = indent;
                lineWidth = 0;
                hasContent = false;
                wrapped = true;

                if (token.Kind == DocumentFlowTokenKind.Space)
                {
                    continue;
                }
            }

            _runs.Add(token.Kind == DocumentFlowTokenKind.Control
                ? DocumentVisualRun.ForControl(column, token.Cells, token.Control!)
                : token.Glyph.Value != 0
                    ? DocumentVisualRun.ForRepeat(
                        column,
                        token.Cells,
                        token.Glyph,
                        token.Face,
                        foregroundOverride,
                        token.SemanticRange,
                        token.ParsedRunIndex,
                        token.Offset,
                        token.Length,
                        token.LinkIndex)
                    : token.ParsedRunIndex >= 0
                    ? DocumentVisualRun.ForText(
                        column,
                        token.Cells,
                        token.ParsedRunIndex,
                        token.Offset,
                        token.Length,
                        token.Face,
                        token.LinkIndex,
                        foregroundOverride)
                    : throw new UnreachableException("A non-control flow token must provide text or a repeated glyph."));

            column = column.Add(token.Cells);
            lineWidth = lineWidth.Add(token.Cells);
            hasContent = true;
        }

        CommitLine(runStart, indent.Add(lineWidth));
    }

    #endregion

    #region Line commit

    private void CommitLine(int runStart, int cells)
    {
        var line = new DocumentVisualLine(runStart, _runs.Count - runStart, cells);
        var lineIndex = _lines.Count;
        _lines.Add(line);
        MaxCells = Math.Max(MaxCells, cells);
        RecordLinkRegions(line, lineIndex);
        RecordControlPlacements(line, lineIndex);
    }

    private void RecordControlPlacements(DocumentVisualLine line, int lineIndex)
    {
        foreach (var run in RunsOf(line))
        {
            if (run.Kind == DocumentRunKind.Control)
            {
                _controlPlacements.Add(new DocumentControlPlacement(
                    run.Control!,
                    new Rect(run.Column, lineIndex, run.Cells, 1)));
            }
        }
    }

    private void RecordLinkRegions(DocumentVisualLine line, int lineIndex)
    {
        var index = line.RunStart;
        var end = line.RunStart + line.RunCount;

        while (index < end)
        {
            var linkIndex = _runs[index].LinkIndex;

            if (linkIndex < 0)
            {
                index++;
                continue;
            }

            // Consecutive runs of one link merge into a single region so a link split across several
            // tokens still hit-tests as one contiguous target.
            var column = _runs[index].Column;
            var cells = 0;

            while (index < end && _runs[index].LinkIndex == linkIndex)
            {
                cells = cells.Add(_runs[index].Cells);
                index++;
            }

            if (cells > 0)
            {
                _linkRegions.Add(new DocumentLinkRegion(linkIndex, lineIndex, column, cells));
            }
        }
    }

    #endregion

    #region Measurement helpers

    [Pure]
    private string ResolveMarker(DocumentListKind kind, int start, int index, int depth) => kind switch
    {
        DocumentListKind.Bulleted => Resolve(BulletFor(depth)).ToString(),
        DocumentListKind.Numbered => FormattableString.Invariant($"{(long) start + index}."),
        _ => throw new UnreachableException("The list marker style is validated on assignment.")
    };

    [Pure]
    private ControlGlyph BulletFor(int depth) => (depth % 3) switch
    {
        0 => _glyphs.FirstBulletGlyph,
        1 => _glyphs.SecondBulletGlyph,
        _ => _glyphs.ThirdBulletGlyph
    };

    [Pure]
    private Rune Resolve(ControlGlyph glyph) => glyph.Value.Resolve(glyph.Fallback, _ambiguousWidth);

    [Pure]
    private int MeasureCells(ReadOnlySpan<char> value) => UnicodeWidth.Measure(value, _ambiguousWidth).Cells;

    [Pure]
    private string ExpandTabs(ReadOnlySpan<char> value)
    {
        if (value.IndexOf('\t') < 0)
        {
            return value.ToString();
        }

        var builder = new StringBuilder(value.Length + _tabAdvance);
        var cells = 0;

        foreach (var grapheme in Graphemes.Enumerate(value))
        {
            var slice = value.Slice(grapheme.Offset, grapheme.Length);

            if (grapheme.Length == 1 && slice[0] == '\t')
            {
                var advance = _tabAdvance - (cells % _tabAdvance);
                _ = builder.Append(' ', advance);
                cells = cells.Add(advance);
                continue;
            }

            _ = builder.Append(slice);
            cells = cells.Add(UnicodeWidth.Measure(slice, _ambiguousWidth).Cells);
        }

        return builder.ToString();
    }

    [Pure]
    private static int SaturatingSubtract(int value, int offset)
    {
        var result = (long) value - offset;
        return result < int.MinValue
            ? int.MinValue
            : result > int.MaxValue
                ? int.MaxValue
                : (int) result;
    }

    #endregion
}
