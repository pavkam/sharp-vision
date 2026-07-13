// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Fonts;

using System.Diagnostics;
using System.Text;

/// <summary>Composes FIGfont glyph rows using version 2 horizontal rules.</summary>
internal static class FigletRenderer
{
    private const FigletLayout _horizontalRules =
        FigletLayout.Equal |
        FigletLayout.Underscore |
        FigletLayout.Hierarchy |
        FigletLayout.OppositePair |
        FigletLayout.BigX |
        FigletLayout.Hardblank;

    /// <summary>Renders one validated request.</summary>
    /// <param name="font">The non-null immutable font.</param>
    /// <param name="text">The non-null input text.</param>
    /// <param name="options">The validated overrides.</param>
    /// <returns>The composed output.</returns>
    internal static string Render(FigletFont font, string text, FigletOptions options)
    {
        FigletDirection direction = options.Direction ?? font.Direction;
        FigletLayout layout = options.Layout ?? font.Layout;
        var logicalLines = text.ReplaceLineEndings("\n").Split('\n');
        List<StringBuilder> composed = [];

        for (var lineIndex = 0; lineIndex < logicalLines.Length; lineIndex++)
        {
            Rune[] runes = [.. logicalLines[lineIndex].EnumerateRunes()];

            if (direction == FigletDirection.RightToLeft)
            {
                Array.Reverse(runes);
            }

            StringBuilder[] rows = RenderLine(font, runes, layout);
            TrimLeading(rows);
            AppendVertical(composed, rows, layout, font.Hardblank);
        }

        StringBuilder output = new StringBuilder();

        for (var row = 0; row < composed.Count; row++)
        {
            if (row != 0)
            {
                _ = output.Append('\n');
            }

            _ = output.Append(composed[row]);
            EnsureLimit(font, output.Length);
        }

        return output.Replace(font.Hardblank, ' ').ToString();
    }

    private static StringBuilder[] RenderLine(
        FigletFont font,
        ReadOnlySpan<Rune> runes,
        FigletLayout layout)
    {
        StringBuilder[] rows = new StringBuilder[font.Height];

        for (var row = 0; row < rows.Length; row++)
        {
            rows[row] = new StringBuilder();
        }

        foreach (Rune rune in runes)
        {
            FigletGlyph glyph = font.GetGlyph(rune.Value);
            var overlap = GetOverlap(rows, glyph, layout, font.Hardblank);

            for (var row = 0; row < rows.Length; row++)
            {
                Merge(rows[row], glyph.Rows[row], overlap, layout, font.Hardblank);
            }
        }

        return rows;
    }

    private static int GetOverlap(
        StringBuilder[] left,
        FigletGlyph right,
        FigletLayout layout,
        char hardblank)
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
                Smush(left[row][leftIndex], right.Rows[row][leading], layout, hardblank) != '\0')
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
        char hardblank)
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
                    : Smush(l, r, layout, hardblank);
        }

        _ = left.Append(right.AsSpan(overlap));
    }

    private static char Smush(char left, char right, FigletLayout layout, char hardblank)
    {
        if ((layout & FigletLayout.HorizontalSmushing) == 0)
        {
            return '\0';
        }

        FigletLayout rules = layout & _horizontalRules;

        if (rules == 0)
        {
            return right;
        }

        if ((rules & FigletLayout.Equal) != 0 && left == right && left != hardblank)
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

        if ((rules & FigletLayout.Hierarchy) != 0)
        {
            var leftClass = Hierarchy(left);
            var rightClass = Hierarchy(right);

            if (leftClass >= 0 && rightClass >= 0 && leftClass != rightClass)
            {
                return leftClass > rightClass ? left : right;
            }
        }

        if ((rules & FigletLayout.OppositePair) != 0 && IsOpposite(left, right))
        {
            return '|';
        }

        if ((rules & FigletLayout.BigX) != 0)
        {
            if ((left, right) is ('/', '\\'))
            {
                return '|';
            }

            if ((left, right) is ('\\', '/'))
            {
                return 'Y';
            }

            if ((left, right) is ('>', '<'))
            {
                return 'X';
            }
        }

        return (rules & FigletLayout.Hardblank) != 0 && left == hardblank && right == hardblank
            ? hardblank
            : '\0';
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
        _ => -1,
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
        var count = rows.Length == 0 ? 0 : rows[0].Length;

        foreach (StringBuilder row in rows)
        {
            var leading = 0;

            while (leading < row.Length && row[leading] == ' ')
            {
                leading++;
            }

            count = Math.Min(count, leading);
        }

        if (count == 0)
        {
            return;
        }

        foreach (StringBuilder row in rows)
        {
            _ = row.Remove(0, count);
        }
    }

    private static void AppendVertical(
        List<StringBuilder> output,
        StringBuilder[] rows,
        FigletLayout layout,
        char hardblank)
    {
        if (output.Count == 0)
        {
            output.AddRange(rows);
            return;
        }

        FigletLayout vertical = layout & (FigletLayout.VerticalFitting | FigletLayout.VerticalSmushing);

        if (vertical == 0)
        {
            output.AddRange(rows);
            return;
        }

        var overlap = GetVerticalOverlap(output, rows, layout, hardblank);
        var start = output.Count - overlap;

        for (var row = 0; row < overlap; row++)
        {
            output[start + row] = MergeVertical(output[start + row], rows[row], layout, hardblank);
        }

        for (var row = overlap; row < rows.Length; row++)
        {
            output.Add(rows[row]);
        }
    }

    private static int GetVerticalOverlap(
        List<StringBuilder> top,
        StringBuilder[] bottom,
        FigletLayout layout,
        char hardblank)
    {
        var maximum = Math.Min(top.Count, bottom.Length);
        var width = Math.Max(
            top.Count == 0 ? 0 : top.Max(row => row.Length),
            bottom.Length == 0 ? 0 : bottom.Max(row => row.Length));
        var result = maximum;

        for (var column = 0; column < width; column++)
        {
            var trailing = 0;

            while (trailing < top.Count &&
                IsVerticalBlank(Get(top[top.Count - trailing - 1], column), hardblank))
            {
                trailing++;
            }

            var leading = 0;

            while (leading < bottom.Length && IsVerticalBlank(Get(bottom[leading], column), hardblank))
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
                    hardblank) != '\0')
            {
                columnOverlap = Math.Min(maximum, columnOverlap + 1);
            }

            result = Math.Min(result, columnOverlap);
        }

        return result;
    }

    private static StringBuilder MergeVertical(
        StringBuilder top,
        StringBuilder bottom,
        FigletLayout layout,
        char hardblank)
    {
        var width = Math.Max(top.Length, bottom.Length);
        StringBuilder result = new StringBuilder(width);

        for (var column = 0; column < width; column++)
        {
            var upper = Get(top, column);
            var lower = Get(bottom, column);
            var value = IsVerticalBlank(upper, hardblank)
                ? lower
                : IsVerticalBlank(lower, hardblank)
                    ? upper
                    : VerticalSmush(upper, lower, layout, hardblank);
            Debug.Assert(value != '\0', "Vertical overlap validation permits every merged collision.");
            _ = result.Append(value);
        }

        return result;
    }

    private static char VerticalSmush(
        char top,
        char bottom,
        FigletLayout layout,
        char hardblank)
    {
        if ((layout & FigletLayout.VerticalSmushing) == 0 ||
            IsVerticalBlank(top, hardblank) ||
            IsVerticalBlank(bottom, hardblank))
        {
            return '\0';
        }

        FigletLayout rules = layout &
            (FigletLayout.VerticalEqual |
                FigletLayout.VerticalUnderscore |
                FigletLayout.VerticalHierarchy |
                FigletLayout.VerticalHorizontalLine |
                FigletLayout.VerticalSupersmush);

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

        if ((rules & FigletLayout.VerticalHierarchy) != 0)
        {
            var topClass = Hierarchy(top);
            var bottomClass = Hierarchy(bottom);

            if (topClass >= 0 && bottomClass >= 0 && topClass != bottomClass)
            {
                return topClass > bottomClass ? top : bottom;
            }
        }

        return (rules & FigletLayout.VerticalHorizontalLine) != 0 &&
            (top, bottom) is ('-', '_') or ('_', '-')
                ? '='
                : (rules & FigletLayout.VerticalSupersmush) != 0 && top == '|' && bottom == '|'
                    ? '|'
                    : '\0';
    }

    private static bool IsVerticalBlank(char value, char hardblank) =>
        value == ' ' || value == hardblank;

    private static char Get(StringBuilder value, int index) =>
        index < value.Length ? value[index] : ' ';
}
