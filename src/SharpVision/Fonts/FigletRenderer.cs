// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Fonts;

/// <summary>Composes FIG-font glyph rows using version 2 horizontal rules.</summary>
internal static class FigletRenderer
{
    private const FigletLayout _horizontalRules =
        FigletLayout.Equal |
        FigletLayout.Underscore |
        FigletLayout.Hierarchy |
        FigletLayout.OppositePair |
        FigletLayout.BigX |
        FigletLayout.HardBlank;

    /// <summary>Renders one validated request.</summary>
    /// <param name="font">The non-null immutable font.</param>
    /// <param name="text">The non-null input text.</param>
    /// <param name="options">The validated overrides.</param>
    /// <returns>The composed output.</returns>
    public static string Render(FigletFont font, string text, FigletOptions options)
    {
        var direction = options.Direction ?? font.Direction;
        var layout = options.Layout ?? font.Layout;
        var logicalLines = text.ReplaceLineEndings("\n").Split('\n');
        List<StringBuilder> composed = [];
        var composedWidth = 0;
        var composedLength = 0;

        foreach (var t in logicalLines)
        {
            Rune[] runes = [.. t.EnumerateRunes()];

            if (direction == FigletDirection.RightToLeft)
            {
                Array.Reverse(runes);
            }

            var rows = RenderLine(font, runes, layout);
            TrimLeading(rows);
            AppendVertical(font, composed, ref composedWidth, ref composedLength, rows, layout, font.HardBlank);
        }

        var output = new StringBuilder();

        for (var row = 0; row < composed.Count; row++)
        {
            if (row != 0)
            {
                _ = output.Append('\n');
            }

            _ = output.Append(composed[row]);
            EnsureLimit(font, output.Length);
        }

        return output.Replace(font.HardBlank, ' ').ToString();
    }

    private static StringBuilder[] RenderLine(
        FigletFont font,
        ReadOnlySpan<Rune> runes,
        FigletLayout layout)
    {
        var rows = new StringBuilder[font.Height];

        for (var row = 0; row < rows.Length; row++)
        {
            rows[row] = new StringBuilder();
        }

        foreach (var rune in runes)
        {
            var glyph = font.GetGlyph(rune.Value);
            var overlap = GetOverlap(rows, glyph, layout, font.HardBlank);

            for (var row = 0; row < rows.Length; row++)
            {
                Merge(rows[row], glyph.Rows[row], overlap, layout, font.HardBlank);
            }

            // Every row stays equally wide after each merge pass (glyph rows are themselves
            // equally wide, and the overlap is computed once per rune across all rows), so
            // checking a single row's length here bounds memory during composition of one
            // logical line instead of only after the entire line - and every unbounded logical
            // line - has already been built. The raw row length overstates what TrimLeading will
            // keep once the line is complete, since real glyphs routinely carry a leading blank
            // margin (see standard.flf) - checking it directly would throw on legitimate,
            // under-the-limit input purely because of margin columns that are always removed
            // before the output is ever returned. Subtracting the CURRENT common leading-blank
            // count avoids that: a merge can only turn a leading blank into a printable column
            // (never the reverse - Merge only replaces ' ' with the other side's character, or
            // keeps an existing printable column as-is), so each row's own leading-blank count is
            // monotonically non-increasing as more runes merge in, and so is the cross-row
            // minimum TrimLeading computes. That makes (raw length - current leading-blank count)
            // a value that only rises toward the eventual trimmed length as composition proceeds,
            // never overshoots it early.
            EnsureLimit(font, rows.Length == 0 ? 0 : rows[0].Length - MeasureLeadingBlank(rows));
        }

        return rows;
    }

    private static int GetOverlap(
        StringBuilder[] left,
        FigletGlyph right,
        FigletLayout layout,
        char hardBlank)
    {
        if ((layout & (FigletLayout.HorizontalFitting | FigletLayout.HorizontalSmushing)) == 0 ||
            left[0].Length == 0)
        {
            return 0;
        }

        var maximum = Math.Min(left[0].Length, right.Width);
        var result = maximum;

        for (var row = 0; row < left.Length; row++)
        {
            var trailing = 0;

            while (trailing < left[row].Length && left[row][left[row].Length - trailing - 1] == ' ')
            {
                trailing++;
            }

            var leading = 0;

            while (leading < right.Width && right.Rows[row][leading] == ' ')
            {
                leading++;
            }

            var rowOverlap = Math.Min(maximum, trailing + leading);
            var leftIndex = left[row].Length - trailing - 1;

            if (leftIndex >= 0 &&
                leading < right.Width &&
                Smush(left[row][leftIndex], right.Rows[row][leading], layout, hardBlank) != '\0')
            {
                rowOverlap = Math.Min(maximum, rowOverlap + 1);
            }

            result = Math.Min(result, rowOverlap);
        }

        return result;
    }

    private static void Merge(
        StringBuilder left,
        string right,
        int overlap,
        FigletLayout layout,
        char hardBlank)
    {
        var start = left.Length - overlap;

        for (var column = 0; column < overlap; column++)
        {
            var l = left[start + column];
            var r = right[column];
            left[start + column] = l == ' '
                ? r
                : r == ' '
                    ? l
                    : Smush(l, r, layout, hardBlank);
        }

        _ = left.Append(right.AsSpan(overlap));
    }

    private static char Smush(char left, char right, FigletLayout layout, char hardBlank)
    {
        if ((layout & FigletLayout.HorizontalSmushing) == 0)
        {
            return '\0';
        }

        var rules = layout & _horizontalRules;

        if (rules == 0)
        {
            // Universal smushing (HorizontalSmushing set with no specific rule bits, as declared
            // by the bundled shadow.flf, smshadow.flf, and mini.flf fonts) must still let a
            // visible pixel win over the other glyph's hardblank, matching the reference FIGlet
            // smushem algorithm's hardblank special case; only a genuine visible-vs-visible
            // collision falls back to preferring the right glyph's pixel.
            return right == hardBlank ? left : right;
        }

        if ((rules & FigletLayout.Equal) != 0 && left == right && left != hardBlank)
        {
            return left;
        }

        if ((rules & FigletLayout.Underscore) != 0)
        {
            if (left == '_' && IsHierarchy(right))
            {
                return right;
            }

            if (right == '_' && IsHierarchy(left))
            {
                return left;
            }
        }

        if ((rules & FigletLayout.Hierarchy) == 0)
        {
            return (rules & FigletLayout.OppositePair) != 0 && IsOpposite(left, right)
                ? '|'
                : (rules & FigletLayout.BigX) == 0
                    ? (rules & FigletLayout.HardBlank) != 0 && left == hardBlank && right == hardBlank
                        ? hardBlank
                        : '\0'
                    : (left, right) switch
                    {
                        ('/', '\\') => '|',
                        ('\\', '/') => 'Y',
                        ('>', '<') => 'X',
                        _ => (rules & FigletLayout.HardBlank) != 0 && left == hardBlank && right == hardBlank
                            ? hardBlank
                            : '\0'
                    };
        }

        var leftClass = Hierarchy(left);
        var rightClass = Hierarchy(right);

        return leftClass >= 0 && rightClass >= 0 && leftClass != rightClass
            ? leftClass > rightClass ? left : right
            : (rules & FigletLayout.OppositePair) != 0 && IsOpposite(left, right)
            ? '|'
            : (rules & FigletLayout.BigX) == 0
            ? (rules & FigletLayout.HardBlank) != 0 && left == hardBlank && right == hardBlank
                ? hardBlank
                : '\0'
            : (left, right) switch
            {
                ('/', '\\') => '|',
                ('\\', '/') => 'Y',
                ('>', '<') => 'X',
                _ => (rules & FigletLayout.HardBlank) != 0 &&
                     left == hardBlank &&
                     right == hardBlank
                    ? hardBlank
                    : '\0'
            };
    }

    private static bool IsHierarchy(char value) => Hierarchy(value) >= 0;

    private static int Hierarchy(char value) => value switch
    {
        '|' => 0,
        '/' or '\\' => 1,
        '[' or ']' => 2,
        '{' or '}' => 3,
        '(' or ')' => 4,
        '<' or '>' => 5,
        _ => -1
    };

    private static bool IsOpposite(char left, char right) =>
        (left, right) is ('[', ']') or (']', '[') or ('{', '}') or ('}', '{') or
        ('(', ')') or (')', '(');

    private static void EnsureLimit(FigletFont font, int length)
    {
        Debug.Assert(length >= 0, "StringBuilder lengths cannot be negative.");

        if (length > font.Limits.MaxOutputChars)
        {
            throw new InvalidOperationException("The rendered FIGlet output exceeds the configured limit.");
        }
    }

    private static void TrimLeading(StringBuilder[] rows)
    {
        var count = MeasureLeadingBlank(rows);

        if (count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            _ = row.Remove(0, count);
        }
    }

    /// <summary>Measures the common leading blank-column count TrimLeading would remove.</summary>
    /// <param name="rows">The rows composed so far for one logical line.</param>
    /// <returns>The number of leading blank columns shared by every row.</returns>
    private static int MeasureLeadingBlank(StringBuilder[] rows)
    {
        var count = rows.Length == 0 ? 0 : rows[0].Length;

        foreach (var row in rows)
        {
            var leading = 0;

            while (leading < row.Length && row[leading] == ' ')
            {
                leading++;
            }

            count = Math.Min(count, leading);
        }

        return count;
    }

    private static void AppendVertical(
        FigletFont font,
        List<StringBuilder> output,
        ref int outputWidth,
        ref int outputLength,
        StringBuilder[] rows,
        FigletLayout layout,
        char hardblank)
    {
        var rowsWidth = rows.Length == 0 ? 0 : rows.Max(row => row.Length);

        if (output.Count == 0)
        {
            output.AddRange(rows);
            outputWidth = rowsWidth;
            outputLength += rows.Sum(row => row.Length);
            EnsureLimit(font, outputLength);
            return;
        }

        var vertical = layout & (FigletLayout.VerticalFitting | FigletLayout.VerticalSmushing);

        if (vertical == 0)
        {
            output.AddRange(rows);
            outputWidth = Math.Max(outputWidth, rowsWidth);
            outputLength += rows.Sum(row => row.Length);
            EnsureLimit(font, outputLength);
            return;
        }

        var overlap = GetVerticalOverlap(output, outputWidth, rows, rowsWidth, layout, hardblank);
        var start = output.Count - overlap;

        for (var row = 0; row < overlap; row++)
        {
            var previousLength = output[start + row].Length;
            output[start + row] = MergeVertical(output[start + row], rows[row], layout, hardblank);

            // A merged row replaces an existing accumulated row rather than appending a fresh
            // one, so only the length delta - never the merged row's full new length - is new
            // output.
            outputLength += output[start + row].Length - previousLength;
        }

        for (var row = overlap; row < rows.Length; row++)
        {
            output.Add(rows[row]);
            outputLength += rows[row].Length;
        }

        outputWidth = Math.Max(outputWidth, rowsWidth);
        EnsureLimit(font, outputLength);
    }

    private static int GetVerticalOverlap(
        List<StringBuilder> top,
        int topWidth,
        StringBuilder[] bottom,
        int bottomWidth,
        FigletLayout layout,
        char hardBlank) =>
        GetVerticalOverlapCore(top, topWidth, bottom, bottomWidth, layout, hardBlank, out _);

    /// <summary>
    /// Implements <see cref="GetVerticalOverlap"/>, additionally reporting how many rows of
    /// accumulated output it inspected. Kept as a pure function returning that count - rather than
    /// a static counter field - so this stateless class stays that way (see the matching remark on
    /// <see cref="Text.Edit.IsBoundaryCore"/>).
    /// </summary>
    /// <param name="top">The accumulated output rows so far.</param>
    /// <param name="topWidth">The accumulated output's current column width.</param>
    /// <param name="bottom">The next glyph's rows.</param>
    /// <param name="bottomWidth">The next glyph's column width.</param>
    /// <param name="layout">The active vertical layout flags.</param>
    /// <param name="hardBlank">The font's hard-blank character.</param>
    /// <param name="topRowsInspected">
    /// Receives the number of distinct <paramref name="top"/> rows this call inspected. Exposed
    /// internally only so a test can prove this stays bounded by the glyph height, not the total
    /// accumulated output length, without a flaky wall-clock timing gate.
    /// </param>
    /// <returns>The number of trailing/leading rows that may be merged in place.</returns>
    internal static int GetVerticalOverlapCore(
        List<StringBuilder> top,
        int topWidth,
        StringBuilder[] bottom,
        int bottomWidth,
        FigletLayout layout,
        char hardBlank,
        out int topRowsInspected)
    {
        var maximum = Math.Min(top.Count, bottom.Length);
        var width = Math.Max(topWidth, bottomWidth);
        var result = maximum;
        var maximumTrailing = 0;

        for (var column = 0; column < width; column++)
        {
            var trailing = 0;

            while (trailing < maximum &&
                   IsVerticalBlank(Get(top[top.Count - trailing - 1], column), hardBlank))
            {
                trailing++;
            }

            maximumTrailing = Math.Max(maximumTrailing, trailing);

            var leading = 0;

            while (leading < bottom.Length && IsVerticalBlank(Get(bottom[leading], column), hardBlank))
            {
                leading++;
            }

            var columnOverlap = Math.Min(maximum, trailing + leading);
            var topIndex = top.Count - trailing - 1;

            if (topIndex >= 0 &&
                leading < bottom.Length &&
                VerticalSmush(
                    Get(top[topIndex], column),
                    Get(bottom[leading], column),
                    layout,
                    hardBlank) != '\0')
            {
                columnOverlap = Math.Min(maximum, columnOverlap + 1);
            }

            result = Math.Min(result, columnOverlap);
        }

        topRowsInspected = maximumTrailing;
        return result;
    }

    private static StringBuilder MergeVertical(
        StringBuilder top,
        StringBuilder bottom,
        FigletLayout layout,
        char hardBlank)
    {
        var width = Math.Max(top.Length, bottom.Length);
        var result = new StringBuilder(width);

        for (var column = 0; column < width; column++)
        {
            var upper = Get(top, column);
            var lower = Get(bottom, column);
            var value = IsVerticalBlank(upper, hardBlank)
                ? lower
                : IsVerticalBlank(lower, hardBlank)
                    ? upper
                    : VerticalSmush(upper, lower, layout, hardBlank);
            Debug.Assert(value != '\0', "Vertical overlap validation permits every merged collision.");
            _ = result.Append(value);
        }

        return result;
    }

    private static char VerticalSmush(
        char top,
        char bottom,
        FigletLayout layout,
        char hardBlank)
    {
        if ((layout & FigletLayout.VerticalSmushing) == 0 ||
            IsVerticalBlank(top, hardBlank) ||
            IsVerticalBlank(bottom, hardBlank))
        {
            return '\0';
        }

        var rules = layout &
                    (FigletLayout.VerticalEqual |
                     FigletLayout.VerticalUnderscore |
                     FigletLayout.VerticalHierarchy |
                     FigletLayout.VerticalHorizontalLine |
                     FigletLayout.VerticalSuperSmush);

        if (rules == 0)
        {
            return bottom;
        }

        if ((rules & FigletLayout.VerticalEqual) != 0 && top == bottom)
        {
            return top;
        }

        if ((rules & FigletLayout.VerticalUnderscore) != 0)
        {
            if (top == '_' && IsHierarchy(bottom))
            {
                return bottom;
            }

            if (bottom == '_' && IsHierarchy(top))
            {
                return top;
            }
        }

        if ((rules & FigletLayout.VerticalHierarchy) == 0)
        {
            return (rules & FigletLayout.VerticalHorizontalLine) != 0 &&
                   (top, bottom) is ('-', '_') or ('_', '-')
                ? '='
                : (rules & FigletLayout.VerticalSuperSmush) != 0 && top == '|' && bottom == '|'
                    ? '|'
                    : '\0';
        }

        var topClass = Hierarchy(top);
        var bottomClass = Hierarchy(bottom);

        return topClass >= 0 && bottomClass >= 0 && topClass != bottomClass
            ? topClass > bottomClass ? top : bottom
            : (rules & FigletLayout.VerticalHorizontalLine) != 0 &&
               (top, bottom) is ('-', '_') or ('_', '-')
            ? '='
            : (rules & FigletLayout.VerticalSuperSmush) != 0 && top == '|' && bottom == '|'
                ? '|'
                : '\0';
    }

    private static bool IsVerticalBlank(char value, char hardBlank) =>
        value == ' ' || value == hardBlank;

    private static char Get(StringBuilder value, int index) =>
        index < value.Length ? value[index] : ' ';
}
