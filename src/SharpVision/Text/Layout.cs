// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Formats borrowed UTF-16 text into grapheme-safe terminal-cell lines.</summary>
[PublicAPI]
public static class Layout
{
    internal const int TabSize = 4;

    /// <summary>Formats text with one overflow policy into caller-owned line storage.</summary>
    /// <param name="value">The UTF-16 text borrowed for this call.</param>
    /// <param name="width">The non-negative finite line width in terminal cells.</param>
    /// <param name="overflow">The horizontal overflow policy.</param>
    /// <param name="alignment">The horizontal placement policy.</param>
    /// <param name="ambiguous">The East Asian Ambiguous width policy.</param>
    /// <param name="destination">Caller-owned prefix storage.</param>
    /// <returns>The complete required line count, which may exceed destination length.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The width is negative or an enum value is unknown.
    /// </exception>
    /// <example>
    /// <code>
    /// Span&lt;Line&gt; lines = stackalloc Line[8];
    /// var required = Layout.Format(
    ///     "one two",
    ///     5,
    ///     Overflow.Wrap,
    ///     Alignment.Start,
    ///     Ambiguous.Narrow,
    ///     lines);
    /// </code>
    /// </example>
    public static int Format(
        ReadOnlySpan<char> value,
        int width,
        Overflow overflow,
        Alignment alignment,
        Ambiguous ambiguous,
        Span<Line> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        overflow.ValidateDefined(nameof(overflow), "The enum value is unknown.");
        alignment.ValidateDefined(nameof(alignment), "The enum value is unknown.");
        ambiguous.ValidateDefined(nameof(ambiguous), "The enum value is unknown.");
        var count = 0;
        var start = 0;

        while (true)
        {
            var end = start;

            while (end < value.Length && value[end] is not ('\r' or '\n'))
            {
                end++;
            }

            if (overflow is Overflow.Wrap or Overflow.WrapAnywhere)
            {
                FormatWrapped(
                    value[start..end],
                    start,
                    width,
                    overflow == Overflow.Wrap,
                    alignment,
                    ambiguous,
                    destination,
                    ref count);
            }
            else
            {
                FormatUnwrapped(
                    value[start..end],
                    start,
                    width,
                    overflow,
                    alignment,
                    ambiguous,
                    destination,
                    ref count);
            }

            if (end == value.Length)
            {
                break;
            }

            start = end + (value[end] == '\r' && end + 1 < value.Length && value[end + 1] == '\n'
                ? 2
                : 1);
        }

        return count;
    }

    private static int Align(int width, int cells, Alignment alignment)
    {
        var remaining = Math.Max(0, width - cells);
        return alignment switch
        {
            Alignment.Start => 0,
            Alignment.Center => remaining / 2,
            Alignment.End => remaining,
            _ => throw new UnreachableException()
        };
    }

    private static int Cells(ReadOnlySpan<char> value, Ambiguous ambiguous)
    {
        var result = 0;
        var position = 0;

        while (position < value.Length)
        {
            var grapheme = Next(value[position..]);
            var cells = ClusterCells(value.Slice(position, grapheme.Length), result, ambiguous);
            result = SaturatingAdd(result, cells);
            position += grapheme.Length;
        }

        return result;
    }

    private static int ClusterCells(
        ReadOnlySpan<char> value,
        int current,
        Ambiguous ambiguous)
    {
        return value.Length == 1 && value[0] == '\t'
            ? TabSize - (current % TabSize)
            : Width.Measure(value, ambiguous).Cells;
    }

    private static void Emit(
        int offset,
        int length,
        int cells,
        int width,
        Alignment alignment,
        bool ellipsis,
        Span<Line> destination,
        ref int count)
    {
        if (count < destination.Length)
        {
            destination[count] = new Line(
                offset,
                length,
                cells,
                Align(width, cells, alignment),
                ellipsis);
        }

        count++;
    }

    private static void FormatUnwrapped(
        ReadOnlySpan<char> value,
        int sourceOffset,
        int width,
        Overflow overflow,
        Alignment alignment,
        Ambiguous ambiguous,
        Span<Line> destination,
        ref int count)
    {
        var completeCells = Cells(value, ambiguous);

        if (overflow == Overflow.Visible || completeCells <= width)
        {
            Emit(
                sourceOffset,
                value.Length,
                completeCells,
                width,
                alignment,
                ellipsis: false,
                destination,
                ref count);
            return;
        }

        const int ellipsisCells = 1;
        var ellipsis = overflow == Overflow.Ellipsis &&
                       width >= ellipsisCells;
        var limit = Math.Max(0, width - (ellipsis ? ellipsisCells : 0));
        var position = 0;
        var cells = 0;
        var wordEnd = 0;
        var wordCells = 0;

        while (position < value.Length)
        {
            var grapheme = Next(value[position..]);
            var cluster = value.Slice(position, grapheme.Length);
            var clusterCells = ClusterCells(cluster, cells, ambiguous);

            if (clusterCells > limit - cells)
            {
                break;
            }

            if (IsWhitespace(cluster))
            {
                wordEnd = position;
                wordCells = cells;
            }

            cells += clusterCells;
            position += grapheme.Length;
        }

        if (overflow == Overflow.Ellipsis && wordEnd > 0)
        {
            position = wordEnd;
            cells = wordCells;
        }

        Emit(
            sourceOffset,
            position,
            cells + (ellipsis ? ellipsisCells : 0),
            width,
            alignment,
            ellipsis,
            destination,
            ref count);
    }

    private static void FormatWrapped(
        ReadOnlySpan<char> value,
        int sourceOffset,
        int width,
        bool wordBoundaries,
        Alignment alignment,
        Ambiguous ambiguous,
        Span<Line> destination,
        ref int count)
    {
        if (value.IsEmpty)
        {
            Emit(sourceOffset, 0, 0, width, alignment, ellipsis: false, destination, ref count);
            return;
        }

        var lineStart = 0;
        var position = 0;
        var cells = 0;
        var breakEnd = 0;
        var breakCells = 0;

        while (position < value.Length)
        {
            var grapheme = Next(value[position..]);
            var cluster = value.Slice(position, grapheme.Length);
            var clusterCells = ClusterCells(cluster, cells, ambiguous);

            if (clusterCells <= width - cells)
            {
                cells += clusterCells;
                position += grapheme.Length;

                if (IsWhitespace(cluster))
                {
                    breakEnd = position;
                    breakCells = cells;
                }

                continue;
            }

            if (wordBoundaries && breakEnd > lineStart)
            {
                Emit(
                    sourceOffset + lineStart,
                    breakEnd - lineStart,
                    breakCells,
                    width,
                    alignment,
                    ellipsis: false,
                    destination,
                    ref count);
                lineStart = breakEnd;
                position = breakEnd;
            }
            else if (position > lineStart)
            {
                Emit(
                    sourceOffset + lineStart,
                    position - lineStart,
                    cells,
                    width,
                    alignment,
                    ellipsis: false,
                    destination,
                    ref count);
                lineStart = position;
            }
            else
            {
                // The cluster is too wide to fit even alone on an empty line. Emit it on its own
                // line anyway, accepting the overflow, rather than advancing past it without ever
                // including it in an emitted Line — the grapheme must not be silently dropped
                // from the output.
                position += grapheme.Length;
                Emit(
                    sourceOffset + lineStart,
                    position - lineStart,
                    clusterCells,
                    width,
                    alignment,
                    ellipsis: false,
                    destination,
                    ref count);
                lineStart = position;
            }

            cells = 0;
            breakEnd = lineStart;
            breakCells = 0;
        }

        if (lineStart < value.Length)
        {
            Emit(
                sourceOffset + lineStart,
                value.Length - lineStart,
                cells,
                width,
                alignment,
                ellipsis: false,
                destination,
                ref count);
        }
    }

    private static bool IsWhitespace(ReadOnlySpan<char> value)
    {
        var status = Rune.DecodeFromUtf16(value, out var rune, out _);
        return status == OperationStatus.Done && Rune.IsWhiteSpace(rune);
    }

    private static int SaturatingAdd(int left, int right)
    {
        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private static Grapheme Next(ReadOnlySpan<char> value)
    {
        var enumerator = Graphemes.Enumerate(value).GetEnumerator();
        var moved = enumerator.MoveNext();
        Debug.Assert(moved, "A non-empty source suffix must contain one grapheme.");
        return enumerator.Current;
    }
}
