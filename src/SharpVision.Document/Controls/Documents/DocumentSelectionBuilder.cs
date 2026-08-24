// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

using UnicodeWidth = Width;

/// <summary>Builds one document selection map beside the semantic layout projection.</summary>
/// <remarks>
/// Parsed display text is registered at the same point it is created, so markup parsing and code
/// normalization happen once. The final geometry pass walks visual runs by row and never derives
/// reading order from rendered cells.
/// </remarks>
internal sealed class DocumentSelectionBuilder
{
    private readonly Dictionary<ControlBase, SelectableTextSnapshot> _controlSnapshots =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ControlBase, DocumentSelectionSource> _controlSources =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<DocumentSelectionGlyph> _directGlyphs = [];
    private readonly Dictionary<(int ParsedRun, int Offset, int Length), Selection> _parsedRanges = [];
    private readonly List<DocumentSelectionSource> _sources = [];
    private readonly StringBuilder _text = new();

    private bool _pendingBlockSeparator;

    #region Semantic construction

    /// <summary>Clears all semantic and source state before a fresh layout.</summary>
    internal void Reset()
    {
        _controlSnapshots.Clear();
        _controlSources.Clear();
        _directGlyphs.Clear();
        _parsedRanges.Clear();
        _sources.Clear();
        _ = _text.Clear();
        _pendingBlockSeparator = false;
    }

    /// <summary>Marks the next non-empty semantic contribution as an adjacent block value.</summary>
    internal void BeginBlockValue()
    {
        if (_text.Length > 0)
        {
            _pendingBlockSeparator = true;
        }
    }

    /// <summary>Appends and indexes one parsed inline display without parsing its markup again.</summary>
    /// <param name="parsedRunIndex">The index assigned by the owning layout.</param>
    /// <param name="display">The markup-free display text.</param>
    internal void AppendParsedRun(int parsedRunIndex, string display)
    {
        Debug.Assert(parsedRunIndex >= 0, "A parsed selection run has a real layout index.");
        Debug.Assert(display is not null, "A parsed selection run has display text.");

        foreach (var grapheme in Graphemes.Enumerate(display))
        {
            var cluster = display.AsSpan(grapheme.Offset, grapheme.Length);

            if (IsLineBreak(cluster))
            {
                AppendHardBreak();
                continue;
            }

            EnsureContentBoundary();
            var start = _text.Length;
            _ = _text.Append(cluster);
            _parsedRanges.Add(
                (parsedRunIndex, grapheme.Offset, grapheme.Length),
                new Selection(start, _text.Length));
        }
    }

    /// <summary>Appends one soft semantic break as an ordinary space.</summary>
    internal Selection AppendSoftBreak() => AppendLiteral(" ");

    /// <summary>Gets the semantic grapheme range registered for one parsed display grapheme.</summary>
    /// <param name="parsedRunIndex">The owning parsed run.</param>
    /// <param name="offset">The grapheme's UTF-16 offset in that display.</param>
    /// <param name="length">The grapheme's UTF-16 length.</param>
    /// <returns>The semantic range, or an empty range when the grapheme is structural.</returns>
    internal Selection ParsedRangeOf(int parsedRunIndex, int offset, int length) =>
        _parsedRanges.GetValueOrDefault((parsedRunIndex, offset, length));

    /// <summary>Appends one hard semantic break normalized to LF.</summary>
    internal void AppendHardBreak()
    {
        EnsureContentBoundary();
        _ = _text.Append('\n');
    }

    /// <summary>Appends a semantic delimiter with no required cell geometry.</summary>
    /// <param name="value">The non-null delimiter or literal content.</param>
    /// <returns>Its range in the complete stream.</returns>
    internal Selection AppendLiteral(string value)
    {
        Debug.Assert(value is not null, "A semantic literal is non-null.");

        if (value.Length == 0)
        {
            return new Selection(_text.Length, _text.Length);
        }

        EnsureContentBoundary();
        var start = _text.Length;
        _ = _text.Append(value);
        return new Selection(start, _text.Length);
    }

    /// <summary>Maps a canonical list marker to its displayed marker and reserved following gutter.</summary>
    /// <param name="semanticMarker">The canonical marker including its following space.</param>
    /// <param name="displayMarker">The marker glyphs painted by layout.</param>
    /// <param name="line">The marker's visual row.</param>
    /// <param name="column">The marker's first visual column.</param>
    /// <param name="gutterCells">The complete marker-and-gap width.</param>
    /// <param name="ambiguousWidth">The live terminal width policy used to paint the marker.</param>
    internal void AppendListMarker(
        string semanticMarker,
        string displayMarker,
        int line,
        int column,
        int gutterCells,
        Ambiguous ambiguousWidth)
    {
        Debug.Assert(semanticMarker.Length >= 2, "A canonical list marker includes a following space.");
        Debug.Assert(displayMarker.Length > 0, "A displayed list marker is non-empty.");

        var range = AppendLiteral(semanticMarker);
        var semanticOffset = range.Start;
        var markerCells = UnicodeWidth.Measure(displayMarker, ambiguousWidth).Cells;
        var semanticPrefix = semanticMarker.AsSpan(0, semanticMarker.Length - 1);

        if (semanticPrefix.SequenceEqual(displayMarker))
        {
            var markerColumn = column;

            foreach (var grapheme in Graphemes.Enumerate(semanticPrefix))
            {
                var cluster = semanticPrefix.Slice(grapheme.Offset, grapheme.Length);
                var width = Math.Max(1, UnicodeWidth.Measure(cluster, ambiguousWidth).Cells);
                _directGlyphs.Add(new DocumentSelectionGlyph(
                    new Selection(
                        semanticOffset + grapheme.Offset,
                        semanticOffset + grapheme.Offset + grapheme.Length),
                    new Rect(markerColumn, line, width, 1)));
                markerColumn += width;
            }
        }
        else
        {
            _directGlyphs.Add(new DocumentSelectionGlyph(
                new Selection(semanticOffset, semanticOffset + semanticPrefix.Length),
                new Rect(column, line, Math.Max(1, markerCells), 1)));
        }

        var spaceStart = range.End - 1;
        _directGlyphs.Add(new DocumentSelectionGlyph(
            new Selection(spaceStart, range.End),
            new Rect(
                SaturatingAdd(column, markerCells),
                line,
                Math.Max(1, gutterCells - markerCells),
                1)));
    }

    /// <summary>Appends one normalized code line and associates its expanded display cells.</summary>
    /// <param name="source">The original line without its line terminator.</param>
    /// <param name="display">The same line with tabs expanded for painting.</param>
    /// <param name="parsedRunIndex">The parsed run containing <paramref name="display"/>.</param>
    /// <param name="ambiguousWidth">The live terminal width policy.</param>
    internal void AppendCodeLine(
        ReadOnlySpan<char> source,
        string display,
        int parsedRunIndex,
        Ambiguous ambiguousWidth)
    {
        var displayOffset = 0;
        var cells = 0;

        foreach (var grapheme in Graphemes.Enumerate(source))
        {
            var cluster = source.Slice(grapheme.Offset, grapheme.Length);
            EnsureContentBoundary();
            var semanticStart = _text.Length;
            _ = _text.Append(cluster);
            var semanticRange = new Selection(semanticStart, _text.Length);

            if (cluster.Length == 1 && cluster[0] == '\t')
            {
                var advance = 4 - (cells % 4);

                for (var index = 0; index < advance; index++)
                {
                    _parsedRanges.Add((parsedRunIndex, displayOffset++, 1), semanticRange);
                }

                cells += advance;
                continue;
            }

            _parsedRanges.Add((parsedRunIndex, displayOffset, cluster.Length), semanticRange);
            displayOffset += cluster.Length;
            cells += UnicodeWidth.Measure(cluster, ambiguousWidth).Cells;
        }

        Debug.Assert(displayOffset == display.Length, "Code tab expansion and semantic indexing stay aligned.");
    }

    /// <summary>Appends one embedded control snapshot at the current semantic position.</summary>
    /// <param name="control">The measured retained control.</param>
    internal void AppendControl(ControlBase control)
    {
        Debug.Assert(control is not null, "An embedded semantic control is non-null.");

        if (control is not ISelectableTextSource source)
        {
            return;
        }

        var snapshot = source.GetSelectableTextSnapshot();
        var range = AppendLiteral(snapshot.Text);
        var selectionSource = new DocumentSelectionSource(
            source,
            control as ISelectableTextViewport,
            range,
            snapshot.Text,
            source.SelectableTextVersion);
        _sources.Add(selectionSource);
        _controlSnapshots.Add(control, snapshot);
        _controlSources.Add(control, selectionSource);
    }

    /// <summary>Refreshes only matching embedded glyph geometry after retained controls are arranged.</summary>
    /// <remarks>
    /// Semantic text and source order remain those captured during measure. A source that changes
    /// text between measure and arrange contributes no refreshed geometry and returns false so the
    /// owner can rebuild and commit the current semantic projection before arrange completes.
    /// </remarks>
    /// <returns>True when every source retained its measured semantic text; otherwise false.</returns>
    internal bool RefreshControlGeometry()
    {
        var semanticTextIsCurrent = true;

        foreach (var pair in _controlSources)
        {
            if (pair.Key.IsDisposed)
            {
                _ = _controlSnapshots.Remove(pair.Key);
                semanticTextIsCurrent = false;
                continue;
            }

            var snapshot = pair.Value.Source.GetSelectableTextSnapshot();

            if (string.Equals(snapshot.Text, pair.Value.Text, StringComparison.Ordinal))
            {
                _controlSnapshots[pair.Key] = snapshot;
                pair.Value.UpdateInvalidationVersion(pair.Value.Source.SelectableTextVersion);
            }
            else
            {
                _ = _controlSnapshots.Remove(pair.Key);
                semanticTextIsCurrent = false;
            }
        }

        return semanticTextIsCurrent;
    }

    #endregion

    #region Geometry projection

    /// <summary>Creates the immutable map after visual line and control geometry has committed.</summary>
    /// <param name="lines">Projected visual lines in row order.</param>
    /// <param name="runs">All visual runs sliced by <paramref name="lines"/>.</param>
    /// <param name="parsedRuns">The parsed displays referenced by text runs.</param>
    /// <param name="placements">Embedded control rectangles in content coordinates.</param>
    /// <param name="ambiguousWidth">The live terminal width policy.</param>
    /// <returns>An independently owned semantic map.</returns>
    internal DocumentSelectionMap Build(
        IReadOnlyList<DocumentVisualLine> lines,
        IReadOnlyList<DocumentVisualRun> runs,
        IReadOnlyList<DocumentParsedRun> parsedRuns,
        IReadOnlyList<DocumentControlPlacement> placements,
        Ambiguous ambiguousWidth)
    {
        var glyphs = new List<DocumentSelectionGlyph>();
        var directIndex = 0;
        var inlineControls = new HashSet<ControlBase>(ReferenceEqualityComparer.Instance);
        var placementByControl = new Dictionary<ControlBase, DocumentControlPlacement>(
            ReferenceEqualityComparer.Instance);

        foreach (var run in runs)
        {
            if (run.Kind == DocumentRunKind.Control)
            {
                _ = inlineControls.Add(run.Control!);
            }
        }

        var blockPlacementsByRow = new List<DocumentControlPlacement>[lines.Count];

        foreach (var placement in placements)
        {
            placementByControl.Add(placement.Control, placement);

            if (!inlineControls.Contains(placement.Control) &&
                placement.Bounds.Y >= 0 &&
                placement.Bounds.Y < blockPlacementsByRow.Length)
            {
                (blockPlacementsByRow[placement.Bounds.Y] ??= []).Add(placement);
            }
        }

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            while (directIndex < _directGlyphs.Count && _directGlyphs[directIndex].Bounds.Y == lineIndex)
            {
                glyphs.Add(_directGlyphs[directIndex++]);
            }

            if (blockPlacementsByRow[lineIndex] is { } blockPlacements)
            {
                foreach (var placement in blockPlacements)
                {
                    AppendControlGlyphs(glyphs, placement);
                }
            }

            var line = lines[lineIndex];

            for (var runIndex = line.RunStart; runIndex < line.RunStart + line.RunCount; runIndex++)
            {
                var run = runs[runIndex];

                if (run.Kind == DocumentRunKind.Text)
                {
                    AppendTextGlyphs(glyphs, parsedRuns[run.ParsedRunIndex], run, lineIndex, ambiguousWidth);
                }
                else if (run.Kind == DocumentRunKind.Repeat && !run.SemanticRange.IsEmpty)
                {
                    glyphs.Add(new DocumentSelectionGlyph(
                        run.SemanticRange,
                        new Rect(run.Column, lineIndex, run.Cells, 1)));
                }
                else if (run.Kind == DocumentRunKind.Control)
                {
                    AppendControlGlyphs(glyphs, placementByControl[run.Control!]);
                }
            }
        }

        return new DocumentSelectionMap(_text.ToString(), [.. glyphs], [.. _sources], lines.Count);
    }

    private void AppendTextGlyphs(
        List<DocumentSelectionGlyph> glyphs,
        DocumentParsedRun parsed,
        DocumentVisualRun run,
        int line,
        Ambiguous ambiguousWidth)
    {
        var x = run.Column;

        foreach (var grapheme in Graphemes.Enumerate(parsed.Display.AsSpan(run.Offset, run.Length)))
        {
            var absoluteOffset = run.Offset + grapheme.Offset;
            var cluster = parsed.Display.AsSpan(absoluteOffset, grapheme.Length);
            var width = UnicodeWidth.Measure(cluster, ambiguousWidth).Cells;

            if (width > 0 &&
                _parsedRanges.TryGetValue(
                (run.ParsedRunIndex, absoluteOffset, grapheme.Length),
                out var range))
            {
                if (glyphs.Count > 0 &&
                    glyphs[^1].Range == range &&
                    glyphs[^1].Source is null &&
                    glyphs[^1].Bounds.Y == line &&
                    glyphs[^1].Bounds.Right == x)
                {
                    var previous = glyphs[^1];
                    glyphs[^1] = new DocumentSelectionGlyph(
                        range,
                        new Rect(previous.Bounds.X, line, previous.Bounds.Width + width, 1));
                }
                else
                {
                    glyphs.Add(new DocumentSelectionGlyph(range, new Rect(x, line, width, 1)));
                }
            }

            x = SaturatingAdd(x, width);
        }
    }

    private void AppendControlGlyphs(
        List<DocumentSelectionGlyph> glyphs,
        DocumentControlPlacement placement)
    {
        if (!_controlSources.TryGetValue(placement.Control, out var source) ||
            !_controlSnapshots.TryGetValue(placement.Control, out var snapshot))
        {
            return;
        }

        foreach (var glyph in snapshot.Glyphs)
        {
            glyphs.Add(new DocumentSelectionGlyph(
                new Selection(source.Range.Start + glyph.Range.Start, source.Range.Start + glyph.Range.End),
                new Rect(
                    SaturatingAdd(placement.Bounds.X, glyph.Bounds.X),
                    SaturatingAdd(placement.Bounds.Y, glyph.Bounds.Y),
                    glyph.Bounds.Width,
                    glyph.Bounds.Height),
                source));
        }
    }

    #endregion

    #region Semantic helpers

    private void EnsureContentBoundary()
    {
        if (!_pendingBlockSeparator)
        {
            return;
        }

        if (_text.Length > 0 && _text[^1] != '\n')
        {
            _ = _text.Append('\n');
        }

        _pendingBlockSeparator = false;
    }

    [Pure]
    private static bool IsLineBreak(ReadOnlySpan<char> value) =>
        value is ['\r'] or ['\n'] or ['\r', '\n'];

    [Pure]
    private static int SaturatingAdd(int left, int right)
    {
        var result = (long) left + right;
        return result < int.MinValue
            ? int.MinValue
            : result > int.MaxValue
                ? int.MaxValue
                : (int) result;
    }

    #endregion
}
