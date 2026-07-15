// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;



/// <summary>Formats borrowed UTF-16 text into grapheme-safe terminal-cell lines.</summary>
public static class Layout
{
    private const int _tabSize = 4;
    private const string _ellipsis = "…";

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
    public static int Format(
        ReadOnlySpan<char> value,
        int width,
        Overflow overflow,
        Alignment alignment,
        Ambiguous ambiguous,
        Span<Line> destination)
    {
        Validate(overflow);
        var (wrapping, trimming) = overflow switch
        {
            Overflow.Wrap => (Wrapping.Word, Trimming.None),
            Overflow.WrapAnywhere => (Wrapping.Grapheme, Trimming.None),
            Overflow.Clip => (Wrapping.None, Trimming.Clip),
            Overflow.Ellipsis => (Wrapping.None, Trimming.WordEllipsis),
            Overflow.Visible => (Wrapping.None, Trimming.None),
            _ => throw new UnreachableException(),
        };

        return Format(value, width, wrapping, trimming, alignment, ambiguous, destination);
    }

    /// <summary>Formats text into caller-owned line storage and reports required capacity.</summary>
    /// <param name="value">The UTF-16 text borrowed for this call.</param>
    /// <param name="width">The non-negative finite line width in terminal cells.</param>
    /// <param name="wrapping">The logical-line wrapping policy.</param>
    /// <param name="trimming">The unwrapped overflow policy.</param>
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
    ///     Wrapping.Word,
    ///     Trimming.None,
    ///     Alignment.Start,
    ///     Ambiguous.Narrow,
    ///     lines);
    /// </code>
    /// </example>
    public static int Format(
        ReadOnlySpan<char> value,
        int width,
        Wrapping wrapping,
        Trimming trimming,
        Alignment alignment,
        Ambiguous ambiguous,
        Span<Line> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        Validate(wrapping);
        Validate(trimming);
        Validate(alignment);
        Validate(ambiguous);
        var count = 0;
        var start = 0;

        while (true)
        {
            var end = start;

            while (end < value.Length && value[end] is not ('\r' or '\n'))
            {
                end++;
            }

            if (wrapping == Wrapping.None)
            {
                FormatUnwrapped(
                    value[start..end],
                    start,
                    width,
                    trimming,
                    alignment,
                    ambiguous,
                    destination,
                    ref count);
            }
            else
            {
                FormatWrapped(
                    value[start..end],
                    start,
                    width,
                    wrapping,
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
            _ => throw new UnreachableException(),
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
            ? _tabSize - (current % _tabSize)
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
        Trimming trimming,
        Alignment alignment,
        Ambiguous ambiguous,
        Span<Line> destination,
        ref int count)
    {
        var completeCells = Cells(value, ambiguous);

        if (trimming == Trimming.None || completeCells <= width)
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

        var ellipsisCells = Width.Measure(_ellipsis, ambiguous).Cells;
        var ellipsis = trimming is Trimming.GraphemeEllipsis or Trimming.WordEllipsis &&
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

        if (trimming == Trimming.WordEllipsis && wordEnd > 0)
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
        Wrapping wrapping,
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

            if (wrapping == Wrapping.Word && breakEnd > lineStart)
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
                position += grapheme.Length;
                lineStart = position;
                Emit(
                    sourceOffset + position,
                    0,
                    0,
                    width,
                    alignment,
                    ellipsis: false,
                    destination,
                    ref count);
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

    private static void Validate<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The enum value is unknown.");
        }
    }
}
