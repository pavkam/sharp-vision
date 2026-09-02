// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

using System.Runtime.CompilerServices;

/// <summary>Owns one immutable semantic document stream, visible glyph map, and row-local hit index.</summary>
internal sealed class TextSelectionMap
{
    private readonly TextSelectionGlyph[] _glyphs;
    private readonly int[] _graphemeBoundaries;
    private readonly int[] _nearestOffsets;
    private readonly int[][] _rowGlyphIndexes;
    private readonly int[][] _rowPrefixMaxRightIndexes;
    private readonly long[][] _rowPrefixMaxRights;
    private readonly int[] _semanticGlyphIndexes;
    private readonly int[] _semanticPrefixMaxEndIndexes;
    private readonly int[] _semanticPrefixMaxEnds;

    /// <summary>Initializes one completed selection projection.</summary>
    /// <param name="text">The normalized complete semantic text.</param>
    /// <param name="glyphs">Visible glyphs in visual reading order.</param>
    /// <param name="sources">Embedded sources in semantic order.</param>
    /// <param name="lineCount">The non-negative number of projected visual lines.</param>
    internal TextSelectionMap(
        string text,
        TextSelectionGlyph[] glyphs,
        TextSelectionSource[] sources,
        int lineCount)
    {
        Debug.Assert(text is not null, "A document selection map owns semantic text.");
        Debug.Assert(glyphs is not null, "A document selection map owns glyphs.");
        Debug.Assert(sources is not null, "A document selection map owns sources.");
        Debug.Assert(lineCount >= 0, "A document selection map has a non-negative visual height.");

        Text = text;
        _glyphs = glyphs;
        _graphemeBoundaries = BuildGraphemeBoundaries(text);
        _semanticGlyphIndexes = BuildSemanticIndex(glyphs);
        BuildSemanticPrefixIndex(
            glyphs,
            _semanticGlyphIndexes,
            out _semanticPrefixMaxEnds,
            out _semanticPrefixMaxEndIndexes);
        Glyphs = Array.AsReadOnly(glyphs);
        Sources = Array.AsReadOnly(sources);
        _rowGlyphIndexes = BuildRowIndex(glyphs, lineCount);
        BuildRowQueryIndex(
            glyphs,
            _rowGlyphIndexes,
            out _rowPrefixMaxRights,
            out _rowPrefixMaxRightIndexes);
        _nearestOffsets = BuildNearestOffsets(glyphs, _rowGlyphIndexes);
        Fingerprint = ComputeFingerprint(text, sources);
    }

    /// <summary>Gets the empty map used before the first document layout.</summary>
    internal static TextSelectionMap Empty { get; } = new(string.Empty, [], [], 0);

    /// <summary>Gets the complete normalized semantic UTF-16 stream.</summary>
    internal string Text { get; }

    /// <summary>Gets visible semantic graphemes in document reading order.</summary>
    internal IReadOnlyList<TextSelectionGlyph> Glyphs { get; }

    /// <summary>Gets embedded source snapshots in document order.</summary>
    internal IReadOnlyList<TextSelectionSource> Sources { get; }

    /// <summary>Gets the stable same-process fingerprint of semantic text and ordered source identity.</summary>
    /// <remarks>
    /// Runtime identity participates because replacing a source with another source containing the
    /// same text must still invalidate selection. Exact source records remain available for
    /// collision-free comparison by the later mutation policy.
    /// </remarks>
    internal ulong Fingerprint { get; }

    /// <summary>Gets the number of visual rows participating in keyboard selection navigation.</summary>
    internal int VisualRowCount => _rowGlyphIndexes.Length;

    /// <summary>Resolves a captured source occurrence by identity, semantic range, and captured text.</summary>
    /// <param name="source">The non-null previously captured occurrence.</param>
    /// <returns>The first exact current occurrence, or null.</returns>
    /// <remarks>
    /// Range participates so one source identity projected more than once never drifts to another
    /// occurrence. Exact duplicates resolve to the first semantic occurrence deterministically.
    /// </remarks>
    internal TextSelectionSource? ResolveSourceOccurrence(TextSelectionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (var candidate in Sources)
        {
            if (ReferenceEquals(candidate.Source, source.Source) &&
                candidate.Range == source.Range &&
                string.Equals(candidate.Text, source.Text, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Gets the preceding extended-grapheme boundary at or before one validated endpoint.</summary>
    /// <param name="offset">A grapheme-aligned UTF-16 endpoint.</param>
    /// <returns>The preceding endpoint, saturated at zero.</returns>
    internal int PreviousBoundary(int offset) => PreviousBoundary(offset, out _);

    /// <summary>Gets the preceding boundary while reporting binary-index entries inspected.</summary>
    internal int PreviousBoundary(int offset, out int inspectedEntries)
    {
        Debug.Assert(offset >= 0 && offset <= Text.Length, "A navigation endpoint is inside semantic text.");
        var index = BinarySearchBoundary(offset, out inspectedEntries);
        return index <= 0 ? 0 : _graphemeBoundaries[index - 1];
    }

    /// <summary>Gets the following extended-grapheme boundary after one validated endpoint.</summary>
    /// <param name="offset">A grapheme-aligned UTF-16 endpoint.</param>
    /// <returns>The following endpoint, saturated at the semantic length.</returns>
    internal int NextBoundary(int offset) => NextBoundary(offset, out _);

    /// <summary>Gets the following boundary while reporting binary-index entries inspected.</summary>
    internal int NextBoundary(int offset, out int inspectedEntries)
    {
        Debug.Assert(offset >= 0 && offset <= Text.Length, "A navigation endpoint is inside semantic text.");
        var index = BinarySearchBoundary(offset, out inspectedEntries);
        return index >= _graphemeBoundaries.Length - 1 ? Text.Length : _graphemeBoundaries[index + 1];
    }

    /// <summary>Resolves one endpoint to its deterministic visual row and cell column.</summary>
    /// <param name="offset">A grapheme-aligned semantic endpoint.</param>
    /// <param name="row">Receives the nearest mapped visual row.</param>
    /// <param name="column">Receives the endpoint's cell column.</param>
    /// <returns>True when the map contains visible glyph geometry.</returns>
    internal bool TryGetVisualPosition(int offset, out int row, out int column)
        => TryGetVisualPosition(offset, out row, out column, out _);

    /// <summary>Resolves one endpoint while reporting semantic-index entries inspected.</summary>
    internal bool TryGetVisualPosition(int offset, out int row, out int column, out int inspectedEntries)
    {
        if (TryGetCaretGlyph(offset, out var glyph, out inspectedEntries))
        {
            row = glyph.Bounds.Y;
            column = glyph.Range.Start == offset
                ? glyph.Bounds.X
                : glyph.Bounds.Right;
            return true;
        }

        row = 0;
        column = 0;
        return false;
    }

    /// <summary>Finds the nearest grapheme endpoint at one visual row and remembered cell column.</summary>
    /// <param name="row">The requested visual row, clamped to the projection.</param>
    /// <param name="column">The remembered visual cell column.</param>
    /// <returns>A grapheme-aligned semantic endpoint.</returns>
    internal int OffsetAtVisualColumn(int row, int column)
    {
        return _rowGlyphIndexes.Length == 0
            ? 0
            : HitTest(new Point(column, Math.Clamp(row, 0, _rowGlyphIndexes.Length - 1)));
    }

    /// <summary>Gets one visual line's first or last selectable endpoint.</summary>
    /// <param name="offset">The current grapheme-aligned endpoint.</param>
    /// <param name="end">True for the line end; false for the line start.</param>
    /// <returns>The requested line endpoint, or the input when no glyph geometry exists.</returns>
    internal int VisualLineBoundary(int offset, bool end)
    {
        _ = TryGetVisualLineBoundary(offset, end, out var boundary, out _, out _);
        return boundary;
    }

    /// <summary>Gets a visual boundary together with the exact glyph geometry that selected it.</summary>
    internal bool TryGetVisualLineBoundary(
        int offset,
        bool end,
        out int boundary,
        out Rect bounds,
        out TextSelectionSource? source)
    {
        if (!TryGetVisualPosition(offset, out var row, out _))
        {
            boundary = offset;
            bounds = default;
            source = null;
            return false;
        }

        var indexes = _rowGlyphIndexes[Math.Clamp(row, 0, _rowGlyphIndexes.Length - 1)];
        if (indexes.Length == 0)
        {
            boundary = _nearestOffsets[row];
            bounds = default;
            source = SourceContaining(boundary);
            return false;
        }

        var selected = end ? _rowPrefixMaxRightIndexes[row][^1] : 0;
        var glyph = _glyphs[indexes[selected]];
        boundary = end ? glyph.Range.End : glyph.Range.Start;
        bounds = glyph.Bounds;
        source = glyph.Source;
        return true;
    }

    /// <summary>Gets the visible glyph rectangle nearest one endpoint and its embedded source occurrence.</summary>
    /// <param name="offset">The grapheme-aligned semantic endpoint.</param>
    /// <param name="bounds">Receives content-relative glyph cells.</param>
    /// <param name="source">Receives the embedded source occurrence, or null for document text.</param>
    /// <returns>True when visible glyph geometry exists.</returns>
    internal bool TryGetCaretGeometry(int offset, out Rect bounds, out TextSelectionSource? source)
        => TryGetCaretGeometry(offset, out bounds, out source, out _);

    /// <summary>Gets caret geometry while reporting semantic-index entries inspected.</summary>
    internal bool TryGetCaretGeometry(
        int offset,
        out Rect bounds,
        out TextSelectionSource? source,
        out int inspectedEntries)
    {
        if (TryGetCaretGlyph(offset, out var glyph, out inspectedEntries))
        {
            bounds = glyph.Bounds;
            source = SourceContaining(offset) ?? glyph.Source;
            return true;
        }

        bounds = default;
        source = SourceContaining(offset);
        inspectedEntries = 0;
        return false;
    }

    private TextSelectionSource? SourceContaining(int offset)
    {
        foreach (var source in Sources)
        {
            if (offset >= source.Range.Start && offset < source.Range.End)
            {
                return source;
            }
        }

        foreach (var source in Sources)
        {
            if (offset == source.Range.End)
            {
                return source;
            }
        }

        return null;
    }

    private bool TryGetCaretGlyph(int offset, out TextSelectionGlyph glyph, out int inspectedEntries)
    {
        inspectedEntries = 0;
        var low = 0;
        var high = _semanticGlyphIndexes.Length;

        while (low < high)
        {
            inspectedEntries++;
            var middle = low + ((high - low) / 2);
            if (_glyphs[_semanticGlyphIndexes[middle]].Range.Start < offset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        if (low < _semanticGlyphIndexes.Length &&
            _glyphs[_semanticGlyphIndexes[low]].Range.Start == offset)
        {
            inspectedEntries++;
            glyph = _glyphs[_semanticGlyphIndexes[low]];
            return true;
        }

        var previous = low > 0 ? _semanticPrefixMaxEndIndexes[low - 1] : -1;
        var selected = SelectNearestSemanticCandidate(offset, low, previous);

        if (selected < 0 || selected >= _semanticGlyphIndexes.Length)
        {
            glyph = default;
            return false;
        }

        inspectedEntries++;
        glyph = _glyphs[_semanticGlyphIndexes[selected]];
        return true;
    }

    /// <summary>Chooses the previous or next semantic glyph flanking one unmapped offset.</summary>
    /// <param name="offset">The semantic endpoint that matched no glyph exactly.</param>
    /// <param name="low">The candidate index at or after <paramref name="offset"/>, or the length.</param>
    /// <param name="previous">The candidate index with the greatest end before <paramref name="offset"/>, or -1.</param>
    /// <returns>The selected index into <see cref="_semanticGlyphIndexes"/>, or -1 when neither exists.</returns>
    /// <remarks>
    /// Candidates on the same visual row, or on immediately adjacent rows with no blank row
    /// between them, compare raw UTF-16 distance — the ordinary "hug the nearer side" placement
    /// used for same-row whitespace and for an everyday caret sitting at the very end of one line
    /// right before the next. Candidates separated by at least one fully blank row instead compare
    /// how many line-break clusters lie on each side and favor the farther one. A blank run has no
    /// glyph of its own, so every offset inside it would otherwise resolve to whichever real row
    /// is nearer — the same real row keyboard Up/Down lands back on through
    /// <see cref="BuildNearestOffsets"/>'s sparse-row lookup after a single step. Favoring the
    /// nearer row there makes a step away from it collapse right back, and can even reverse
    /// direction across several consecutive blank rows. Favoring the farther row instead
    /// guarantees the endpoint a subsequent same-direction step derives, via
    /// <see cref="OffsetAtVisualColumn"/>, lies on the correct side of the original offset.
    /// </remarks>
    private int SelectNearestSemanticCandidate(int offset, int low, int previous)
    {
        if (previous < 0)
        {
            return low < _semanticGlyphIndexes.Length ? low : -1;
        }

        if (low >= _semanticGlyphIndexes.Length)
        {
            return previous;
        }

        var previousGlyph = _glyphs[_semanticGlyphIndexes[previous]];
        var nextGlyph = _glyphs[_semanticGlyphIndexes[low]];
        var previousEnd = _semanticPrefixMaxEnds[low - 1];
        var nextStart = nextGlyph.Range.Start;
        var previousRow = previousGlyph.Bounds.Bottom - 1;
        var nextRow = nextGlyph.Bounds.Y;

        if (nextRow - previousRow <= 1)
        {
            var previousDistance = offset - previousEnd;
            var nextDistance = nextStart - offset;
            return nextDistance <= previousDistance ? low : previous;
        }

        var previousRowDistance = CountLineBreaks(previousEnd, offset);
        var nextRowDistance = CountLineBreaks(offset, nextStart);
        return previousRowDistance <= nextRowDistance ? low : previous;
    }

    /// <summary>Counts line-break grapheme clusters in one semantic range, one per crossed visual row.</summary>
    /// <param name="start">The inclusive UTF-16 start of the range.</param>
    /// <param name="end">The exclusive UTF-16 end of the range.</param>
    private int CountLineBreaks(int start, int end)
    {
        if (end <= start)
        {
            return 0;
        }

        var count = 0;
        var span = Text.AsSpan(start, end - start);

        foreach (var grapheme in Graphemes.Enumerate(span))
        {
            if (span.Slice(grapheme.Offset, grapheme.Length).IndexOfAny('\r', '\n') >= 0)
            {
                count++;
            }
        }

        return count;
    }

    private int BinarySearchBoundary(int offset, out int inspectedEntries)
    {
        inspectedEntries = 0;
        var low = 0;
        var high = _graphemeBoundaries.Length - 1;
        while (low <= high)
        {
            inspectedEntries++;
            var middle = low + ((high - low) / 2);
            var value = _graphemeBoundaries[middle];
            if (value == offset)
            {
                return middle;
            }

            if (value < offset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return Math.Clamp(low, 0, _graphemeBoundaries.Length - 1);
    }

    private static int[] BuildGraphemeBoundaries(string text)
    {
        var result = new List<int> { 0 };
        foreach (var grapheme in Graphemes.Enumerate(text))
        {
            result.Add(grapheme.Offset + grapheme.Length);
        }

        return [.. result];
    }

    private static int[] BuildSemanticIndex(TextSelectionGlyph[] glyphs)
    {
        var indexes = Enumerable.Range(0, glyphs.Length).ToArray();
        Array.Sort(indexes, (left, right) =>
        {
            var comparison = glyphs[left].Range.Start.CompareTo(glyphs[right].Range.Start);
            return comparison != 0 ? comparison : left.CompareTo(right);
        });
        return indexes;
    }

    private static void BuildSemanticPrefixIndex(
        TextSelectionGlyph[] glyphs,
        int[] indexes,
        out int[] ends,
        out int[] candidates)
    {
        ends = new int[indexes.Length];
        candidates = new int[indexes.Length];
        var maximum = -1;
        var candidate = -1;
        for (var index = 0; index < indexes.Length; index++)
        {
            var end = glyphs[indexes[index]].Range.End;
            if (end > maximum) { maximum = end; candidate = index; }
            ends[index] = maximum;
            candidates[index] = candidate;
        }
    }

    #region Hit testing

    /// <summary>Resolves one content-relative cell to a grapheme endpoint using only its visual row.</summary>
    /// <param name="point">The content-relative cell coordinate.</param>
    /// <returns>A grapheme-aligned UTF-16 endpoint in <see cref="Text"/>.</returns>
    /// <remarks>
    /// Row entries sort by horizontal origin, true right edge, semantic range, then original ordinal.
    /// Duplicate or overlapping rectangles use the first entry in that order, making source arrays
    /// with arbitrary ordering deterministic without changing <see cref="Glyphs"/>' semantic order.
    /// </remarks>
    internal int HitTest(Point point) => HitTest(point, out _);

    /// <summary>Resolves one content-relative cell to the start of the glyph occupying it, falling
    /// back to the nearest endpoint exactly like <see cref="HitTest(Point)"/> when no glyph does.</summary>
    /// <param name="point">The content-relative cell coordinate.</param>
    /// <returns>A grapheme-aligned UTF-16 endpoint in <see cref="Text"/>.</returns>
    /// <remarks>
    /// A single click places a caret, so <see cref="HitTest(Point)"/> resolves each cell of a wide
    /// glyph to its nearer boundary. A word or line selection instead addresses the glyph under
    /// the pointer as a whole - selecting by the nearer boundary would pick the right-hand
    /// neighbour whenever a multi-click lands on the trailing cell of a wide glyph, which the
    /// caret rule alone cannot distinguish from a click just past it.
    /// </remarks>
    internal int HitTestGlyph(Point point) => HitTest(point, out _, snapToGlyphStart: true);

    /// <summary>Resolves one cell while reporting the bounded number of row-index entries inspected.</summary>
    /// <param name="point">The content-relative cell coordinate.</param>
    /// <param name="inspectedEntries">
    /// Receives the number of binary-index entries and final glyph candidates examined.
    /// </param>
    /// <param name="snapToGlyphStart">
    /// Whether a cell inside a glyph resolves to that glyph's start rather than its nearer boundary.
    /// </param>
    /// <returns>A grapheme-aligned UTF-16 endpoint in <see cref="Text"/>.</returns>
    /// <remarks>
    /// This internal seam proves that long non-overlapping rows remain logarithmic without relying
    /// on wall-clock timing. It does not expose production selection state or private storage.
    /// </remarks>
    internal int HitTest(Point point, out int inspectedEntries, bool snapToGlyphStart = false)
    {
        inspectedEntries = 0;

        if (_rowGlyphIndexes.Length == 0 || _glyphs.Length == 0)
        {
            return 0;
        }

        var row = Math.Clamp(point.Y, 0, _rowGlyphIndexes.Length - 1);
        var indexes = _rowGlyphIndexes[row];

        if (indexes.Length == 0)
        {
            return _nearestOffsets[row];
        }

        var low = 0;
        var high = indexes.Length;

        // Find the first glyph whose start lies strictly after the pointer. Everything that can
        // contain the pointer is in the prefix, even when rectangles overlap or arrive reversed.
        while (low < high)
        {
            inspectedEntries++;
            var middle = low + ((high - low) / 2);
            var glyph = _glyphs[indexes[middle]];

            if (glyph.Bounds.X <= point.X)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        var firstAfter = low;
        var prefixMaxRights = _rowPrefixMaxRights[row];
        var prefixMaxRightIndexes = _rowPrefixMaxRightIndexes[row];

        if (firstAfter > 0 && prefixMaxRights[firstAfter - 1] > point.X)
        {
            low = 0;
            high = firstAfter;

            while (low < high)
            {
                inspectedEntries++;
                var middle = low + ((high - low) / 2);

                if (prefixMaxRights[middle] > point.X)
                {
                    high = middle;
                }
                else
                {
                    low = middle + 1;
                }
            }

            inspectedEntries++;
            var glyph = _glyphs[indexes[prefixMaxRightIndexes[low]]];
            var relative = (long) point.X - glyph.Bounds.X;
            return snapToGlyphStart || relative * 2 < glyph.Bounds.Width
                ? glyph.Range.Start
                : glyph.Range.End;
        }

        var previous = firstAfter > 0 ? prefixMaxRightIndexes[firstAfter - 1] : -1;

        if (previous < 0)
        {
            inspectedEntries++;
            return _glyphs[indexes[firstAfter]].Range.Start;
        }

        if (firstAfter >= indexes.Length)
        {
            inspectedEntries++;
            return _glyphs[indexes[previous]].Range.End;
        }

        inspectedEntries += 2;
        var previousGlyph = _glyphs[indexes[previous]];
        var nextGlyph = _glyphs[indexes[firstAfter]];
        var previousRight = prefixMaxRights[firstAfter - 1];
        var previousDistance = point.X - previousRight + 1;
        var nextDistance = (long) nextGlyph.Bounds.X - point.X;
        return previousDistance <= nextDistance ? previousGlyph.Range.End : nextGlyph.Range.Start;
    }

    private static int[][] BuildRowIndex(TextSelectionGlyph[] glyphs, int lineCount)
    {
        var rows = new List<int>[lineCount];

        for (var index = 0; index < glyphs.Length; index++)
        {
            var glyph = glyphs[index];
            var first = Math.Clamp(glyph.Bounds.Y, 0, lineCount);
            var last = Math.Clamp(glyph.Bounds.Bottom, 0, lineCount);

            for (var row = first; row < last; row++)
            {
                (rows[row] ??= []).Add(index);
            }
        }

        var result = new int[lineCount][];

        for (var row = 0; row < lineCount; row++)
        {
            rows[row]?.Sort((left, right) => CompareVisualGlyphs(glyphs, left, right));
            result[row] = rows[row]?.ToArray() ?? [];
        }

        return result;
    }

    private static void BuildRowQueryIndex(
        TextSelectionGlyph[] glyphs,
        int[][] rows,
        out long[][] prefixMaxRights,
        out int[][] prefixMaxRightIndexes)
    {
        prefixMaxRights = new long[rows.Length][];
        prefixMaxRightIndexes = new int[rows.Length][];

        for (var row = 0; row < rows.Length; row++)
        {
            var indexes = rows[row];
            var rights = new long[indexes.Length];
            var candidates = new int[indexes.Length];
            var maximum = long.MinValue;
            var candidate = -1;

            for (var index = 0; index < indexes.Length; index++)
            {
                var glyph = glyphs[indexes[index]];
                var right = (long) glyph.Bounds.X + glyph.Bounds.Width;

                if (right > maximum)
                {
                    maximum = right;
                    candidate = index;
                }

                rights[index] = maximum;
                candidates[index] = candidate;
            }

            prefixMaxRights[row] = rights;
            prefixMaxRightIndexes[row] = candidates;
        }
    }

    private static int CompareVisualGlyphs(TextSelectionGlyph[] glyphs, int leftIndex, int rightIndex)
    {
        var left = glyphs[leftIndex];
        var right = glyphs[rightIndex];
        var comparison = left.Bounds.X.CompareTo(right.Bounds.X);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = ((long) left.Bounds.X + left.Bounds.Width)
            .CompareTo((long) right.Bounds.X + right.Bounds.Width);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Range.Start.CompareTo(right.Range.Start);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Range.End.CompareTo(right.Range.End);
        return comparison != 0 ? comparison : leftIndex.CompareTo(rightIndex);
    }

    private static int[] BuildNearestOffsets(TextSelectionGlyph[] glyphs, int[][] rows)
    {
        var nearest = new int[rows.Length];
        var nextOffsets = new int[rows.Length];
        var nextRows = new int[rows.Length];
        var nextOffset = 0;
        var nextRow = -1;

        for (var row = rows.Length - 1; row >= 0; row--)
        {
            if (rows[row].Length > 0)
            {
                nextOffset = glyphs[rows[row][0]].Range.Start;
                nextRow = row;
            }

            nextOffsets[row] = nextOffset;
            nextRows[row] = nextRow;
        }

        var previousOffset = 0;
        var previousRow = -1;

        for (var row = 0; row < rows.Length; row++)
        {
            if (rows[row].Length > 0)
            {
                previousOffset = glyphs[rows[row][^1]].Range.End;
                previousRow = row;
                nearest[row] = previousOffset;
                continue;
            }

            if (previousRow >= 0 &&
                (nextRows[row] < 0 || row - previousRow <= nextRows[row] - row))
            {
                nearest[row] = previousOffset;
            }
            else if (nextRows[row] >= 0)
            {
                nearest[row] = nextOffsets[row];
            }
        }

        return nearest;
    }

    #endregion

    #region Mutation fingerprint

    internal static ulong ComputeFingerprint(string text, ReadOnlySpan<TextSelectionSource> sources)
    {
        const ulong offset = 14695981039346656037;
        var hash = offset;

        static void Append(ref ulong hashValue, ReadOnlySpan<char> value)
        {
            const ulong localPrime = 1099511628211;

            foreach (var character in value)
            {
                hashValue = unchecked((hashValue ^ character) * localPrime);
            }
        }

        static void AppendInt32(ref ulong hashValue, int value)
        {
            const ulong localPrime = 1099511628211;
            var bits = unchecked((uint) value);

            // Feed a fixed little-endian representation so checked build contexts and host
            // endianness cannot change the same-process semantic identity algorithm.
            for (var index = 0; index < sizeof(int); index++)
            {
                hashValue = unchecked((hashValue ^ (byte) bits) * localPrime);
                bits >>= 8;
            }
        }

        AppendInt32(ref hash, text.Length);
        Append(ref hash, text);
        AppendInt32(ref hash, sources.Length);

        foreach (var source in sources)
        {
            AppendInt32(ref hash, RuntimeHelpers.GetHashCode(source.Source));
            AppendInt32(ref hash, source.Range.Start);
            AppendInt32(ref hash, source.Range.Length);
            AppendInt32(ref hash, source.Text.Length);
            Append(ref hash, source.Text);
        }

        return hash;
    }

    #endregion
}
