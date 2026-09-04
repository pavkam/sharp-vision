// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Text;

using TextLayout = SharpVision.Text.Layout;

/// <summary>Verifies grapheme-safe wrapping, trimming, alignment, and line metrics.</summary>
public sealed class LayoutTests
{
    /// <summary>Verifies empty content produces one stable empty logical line.</summary>
    [Fact]
    public void Format_WhenContentIsEmpty_ReturnsOneEmptyLine()
    {
        var lines = Format(string.Empty, width: 8);

        lines.ShouldBe([new Line(0, 0, 0, 0, false)]);
    }

    /// <summary>Verifies CR, LF, and CRLF delimit logical lines without entering slices.</summary>
    [Theory]
    [InlineData("a\rb", 2)]
    [InlineData("a\nb", 2)]
    [InlineData("a\r\nb", 2)]
    public void Format_WhenContentContainsNewlines_ExcludesDelimiters(string content, int count)
    {
        var lines = Format(content, width: 8);

        lines.Length.ShouldBe(count);
        content.AsSpan(lines[0].Offset, lines[0].Length).ToString().ShouldBe("a");
        content.AsSpan(lines[1].Offset, lines[1].Length).ToString().ShouldBe("b");
    }

    /// <summary>Verifies grapheme wrapping never splits combining or wide clusters.</summary>
    [Fact]
    public void Format_WhenWrappingByGrapheme_PreservesClusterBoundaries()
    {
        const string content = "e\u0301界x";
        var lines = Format(content, width: 2, overflow: Overflow.WrapAnywhere);

        lines.ShouldBe([
            new Line(0, 2, 1, 0, false),
            new Line(2, 1, 2, 0, false),
            new Line(3, 1, 1, 0, false)
        ]);
    }

    /// <summary>Verifies word wrapping prefers the last complete separator boundary.</summary>
    [Fact]
    public void Format_WhenWrappingWords_MovesWholeWordToNextLine()
    {
        const string content = "one two";
        var lines = Format(content, width: 5, overflow: Overflow.Wrap);

        lines.Length.ShouldBe(2);
        content.AsSpan(lines[0].Offset, lines[0].Length).ToString().ShouldBe("one ");
        content.AsSpan(lines[1].Offset, lines[1].Length).ToString().ShouldBe("two");
        lines.Select(static line => line.Cells).ShouldBe([4, 3]);
    }

    /// <summary>Verifies a non-terminal word that fills the line exactly still advances the
    /// word-boundary tracker, so the line is not needlessly re-wrapped at an earlier, stale
    /// boundary.</summary>
    [Fact]
    public void Format_WhenNonTerminalWordFillsLineExactly_DoesNotReWrapAtStaleBoundary()
    {
        const string content = "one two three";
        var lines = Format(content, width: 7, overflow: Overflow.Wrap);

        lines.Length.ShouldBe(2);
        content.AsSpan(lines[0].Offset, lines[0].Length).ToString().ShouldBe("one two");
        content.AsSpan(lines[1].Offset, lines[1].Length).ToString().ShouldBe("three");
        lines.Select(static line => line.Cells).ShouldBe([7, 5]);
    }

    /// <summary>Verifies a run that fills the line exactly under WrapAnywhere still treats the
    /// immediately following whitespace as the break, so the next line does not open with a
    /// stray leading space the way word-boundary wrapping already avoids.</summary>
    [Fact]
    public void Format_WhenOverflowWrapAnywhereFillsLineExactly_ConsumesFollowingWhitespace()
    {
        const string content = "alpha beta";
        var lines = Format(content, width: 5, overflow: Overflow.WrapAnywhere);

        lines.Length.ShouldBe(2);
        content.AsSpan(lines[0].Offset, lines[0].Length).ToString().ShouldBe("alpha");
        content.AsSpan(lines[1].Offset, lines[1].Length).ToString().ShouldBe("beta");
        lines.Select(static line => line.Cells).ShouldBe([5, 4]);
    }

    /// <summary>Verifies clipping removes only complete overflowing graphemes.</summary>
    [Fact]
    public void Format_WhenClipping_TruncatesAtGraphemeBoundary()
    {
        const string content = "ab界c";
        var lines = Format(content, width: 3, overflow: Overflow.Clip);

        lines.ShouldBe([new Line(0, 2, 2, 0, false)]);
    }

    /// <summary>Verifies grapheme ellipsis reserves one cell and marks the line.</summary>
    [Fact]
    public void Format_WhenUsingGraphemeEllipsis_ReservesEllipsisCell()
    {
        const string content = "ab界c";
        var lines = Format(content, width: 4, overflow: Overflow.Ellipsis);

        lines.ShouldBe([new Line(0, 2, 3, 0, true)]);
    }

    /// <summary>Verifies word ellipsis backs up to a complete word boundary.</summary>
    [Fact]
    public void Format_WhenUsingWordEllipsis_RemovesPartialWord()
    {
        const string content = "one two";
        var lines = Format(content, width: 6, overflow: Overflow.Ellipsis);

        lines.ShouldBe([new Line(0, 3, 4, 0, true)]);
    }

    /// <summary>Verifies a non-terminal word that fills the truncation limit exactly is kept in the
    /// ellipsized output rather than silently dropped by a stale word-boundary tracker.</summary>
    [Fact]
    public void Format_WhenNonTerminalWordFillsEllipsisLimitExactly_KeepsWordInOutput()
    {
        const string content = "aa bbbb ccccc";
        var lines = Format(content, width: 8, overflow: Overflow.Ellipsis);

        lines.ShouldBe([new Line(0, 7, 8, 0, true)]);
    }

    /// <summary>Verifies start, center, and end alignment produce integer leading cells.</summary>
    [Theory]
    [InlineData(Alignment.Start, 0)]
    [InlineData(Alignment.Center, 3)]
    [InlineData(Alignment.End, 7)]
    public void Format_WhenAlignmentChanges_ComputesLeadingCells(Alignment alignment, int leading)
    {
        var lines = Format("abc", width: 10, alignment: alignment);

        lines[0].Leading.ShouldBe(leading);
    }

    /// <summary>Verifies tab widths advance to explicit four-cell stops.</summary>
    [Fact]
    public void Format_WhenContentContainsTabs_UsesFourCellStops()
    {
        var lines = Format("a\tb", width: 10);

        lines[0].Cells.ShouldBe(5);
    }

    /// <summary>Verifies emoji ZWJ, selectors, and invalid UTF-16 remain whole segments.</summary>
    [Fact]
    public void Format_WhenContentIsComplexUnicode_UsesSharedWidthPolicy()
    {
        const string content = "👩‍💻\uFE0F\uD800";
        var lines = Format(content, width: 8);

        lines[0].Length.ShouldBe(content.Length);
        lines[0].Cells.ShouldBe(3);
    }

    /// <summary>Verifies zero width still emits the sole cluster on its own line rather than
    /// dropping it — there is no narrower line it could ever fit on.</summary>
    [Fact]
    public void Format_WhenWidthIsZero_StillEmitsSoleClusterRatherThanDroppingIt()
    {
        var lines = Format("界", width: 0, overflow: Overflow.WrapAnywhere);

        lines.ShouldBe([new Line(0, 1, 2, 0, false)]);
    }

    /// <summary>Verifies the returned count reports capacity beyond caller storage.</summary>
    [Fact]
    public void Format_WhenDestinationIsShort_ReportsRequiredCapacityAndWritesPrefix()
    {
        var lines = new Line[1];

        var required = TextLayout.Format(
            "a\nb".AsSpan(),
            8,
            Overflow.Visible,
            Alignment.Start,
            Ambiguous.Narrow,
            lines);

        required.ShouldBe(2);
        lines[0].ShouldBe(new Line(0, 1, 1, 0, false));
    }

    /// <summary>Verifies public enum and width arguments reject invalid input.</summary>
    [Fact]
    public void Format_WhenArgumentsAreInvalid_ThrowsBeforeWritingDestination()
    {
        var lines = new[] { new Line(1, 1, 1, 1, true) };
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            TextLayout.Format("x", -1, Overflow.Visible, Alignment.Start, Ambiguous.Narrow, lines));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            TextLayout.Format("x", 1, (Overflow) 99, Alignment.Start, Ambiguous.Narrow, lines));

        lines[0].ShouldBe(new Line(1, 1, 1, 1, true));
    }

    /// <summary>Verifies word overflow prefers a complete separator boundary.</summary>
    [Fact]
    public void Format_WhenOverflowWrapIsUsed_MovesWholeWordToNextLine()
    {
        Span<Line> lines = stackalloc Line[4];

        var count = TextLayout.Format(
            "one two",
            5,
            Overflow.Wrap,
            Alignment.Start,
            Ambiguous.Narrow,
            lines);

        count.ShouldBe(2);
        lines[0].ShouldBe(new Line(0, 4, 4, 0, false));
        lines[1].ShouldBe(new Line(4, 3, 3, 0, false));
    }

    /// <summary>Verifies anywhere overflow breaks only between complete grapheme clusters, and that a
    /// cluster too wide to fit alone on an empty line is still emitted on its own line — accepting
    /// the overflow — rather than silently dropped.</summary>
    [Fact]
    public void Format_WhenOverflowWrapAnywhereIsUsed_PreservesGraphemes()
    {
        Span<Line> lines = stackalloc Line[4];

        var count = TextLayout.Format(
            "e\u0301界",
            1,
            Overflow.WrapAnywhere,
            Alignment.Start,
            Ambiguous.Narrow,
            lines);

        count.ShouldBe(2);
        lines[0].ShouldBe(new Line(0, 2, 1, 0, false));
        lines[1].ShouldBe(new Line(2, 1, 2, 0, false));
    }

    /// <summary>Verifies clip overflow keeps one line ending at a complete grapheme.</summary>
    [Fact]
    public void Format_WhenOverflowClipIsUsed_ClipsCompleteCluster()
    {
        Span<Line> lines = stackalloc Line[2];

        var count = TextLayout.Format(
            "ab界",
            3,
            Overflow.Clip,
            Alignment.Start,
            Ambiguous.Narrow,
            lines);

        count.ShouldBe(1);
        lines[0].ShouldBe(new Line(0, 2, 2, 0, false));
    }

    /// <summary>Verifies ellipsis overflow prefers a word boundary and reserves the marker width.</summary>
    [Fact]
    public void Format_WhenOverflowEllipsisIsUsed_TrimsAtWordBoundary()
    {
        Span<Line> lines = stackalloc Line[2];

        var count = TextLayout.Format(
            "one two",
            6,
            Overflow.Ellipsis,
            Alignment.Start,
            Ambiguous.Narrow,
            lines);

        count.ShouldBe(1);
        lines[0].ShouldBe(new Line(0, 3, 4, 0, true));
    }

    /// <summary>Verifies visible overflow reports complete width without clipping.</summary>
    [Fact]
    public void Format_WhenOverflowVisibleIsUsed_KeepsCompleteLogicalLine()
    {
        Span<Line> lines = stackalloc Line[2];

        var count = TextLayout.Format(
            "abcdefgh",
            4,
            Overflow.Visible,
            Alignment.Start,
            Ambiguous.Narrow,
            lines);

        count.ShouldBe(1);
        lines[0].ShouldBe(new Line(0, 8, 8, 0, false));
    }

    /// <summary>Verifies an unknown overflow value fails before mutating caller storage.</summary>
    [Fact]
    public void Format_WhenOverflowIsUnknown_ThrowsBeforeWritingDestination()
    {
        var lines = new[] { new Line(1, 1, 1, 1, true) };

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            TextLayout.Format(
                "x",
                1,
                (Overflow) 99,
                Alignment.Start,
                Ambiguous.Narrow,
                lines));

        lines[0].ShouldBe(new Line(1, 1, 1, 1, true));
    }

    private static Line[] Format(
        string content,
        int width,
        Overflow overflow = Overflow.Visible,
        Alignment alignment = Alignment.Start)
    {
        Span<Line> initial = stackalloc Line[4];
        var required = TextLayout.Format(
            content,
            width,
            overflow,
            alignment,
            Ambiguous.Narrow,
            initial);
        var result = new Line[required];
        _ = TextLayout.Format(
            content,
            width,
            overflow,
            alignment,
            Ambiguous.Narrow,
            result);
        return result;
    }

    private const int _caseCount = 5_000;
    private const int _seed = 0x007E_875A;

    /// <summary>Verifies every emitted slice is deterministic, bounded, and grapheme-safe.</summary>
    [Fact]
    public void Format_WhenInputsAreRandomized_PreservesGraphemeAndCellInvariants()
    {
        var random = new Random(_seed);

        for (var sample = 0; sample < _caseCount; sample++)
        {
            var content = Content(random);
            var width = random.Next(0, 21);
            var overflow = (Overflow) random.Next(0, 5);
            var alignment = (Alignment) random.Next(0, 3);
            var ambiguous = (Ambiguous) random.Next(0, 2);
            var context = $"seed=0x{_seed:X8}, case={sample}, width={width}, " +
                          $"overflow={overflow}, alignment={alignment}, " +
                          $"utf16={Convert.ToHexString(Encoding.Unicode.GetBytes(content))}";
            var first = Format(content, width, overflow, alignment, ambiguous);
            var second = Format(content, width, overflow, alignment, ambiguous);
            var boundaries = Boundaries(content);
            var previous = 0;

            second.ShouldBe(first, context);
            first.ShouldNotBeEmpty(context);

            foreach (var line in first)
            {
                line.Offset.ShouldBeGreaterThanOrEqualTo(previous, context);
                line.Offset.ShouldBeLessThanOrEqualTo(content.Length, context);
                line.Length.ShouldBeGreaterThanOrEqualTo(0, context);
                (line.Offset + line.Length).ShouldBeLessThanOrEqualTo(content.Length, context);
                boundaries.ShouldContain(line.Offset, context);
                boundaries.ShouldContain(line.Offset + line.Length, context);
                line.Cells.ShouldBe(Cells(content.AsSpan(line.Offset, line.Length), ambiguous) +
                                    (line.HasEllipsis ? 1 : 0), context);
                line.Leading.ShouldBeGreaterThanOrEqualTo(0, context);

                // A single grapheme cluster too wide to fit even alone is still emitted on its
                // own line under Wrap/WrapAnywhere, accepting the overflow, rather than dropped —
                // that line is the sole documented exception to the width bound.
                var isSingleOverflowingCluster = overflow is Overflow.Wrap or Overflow.WrapAnywhere &&
                    GraphemeCount(content.AsSpan(line.Offset, line.Length)) == 1;

                if (width > 0 && overflow != Overflow.Visible && !isSingleOverflowingCluster)
                {
                    line.Cells.ShouldBeLessThanOrEqualTo(width, context);
                }

                previous = line.Offset + line.Length;
            }
        }
    }

    private static HashSet<int> Boundaries(string content)
    {
        HashSet<int> result = [0, content.Length];

        foreach (var grapheme in Graphemes.Enumerate(content))
        {
            _ = result.Add(grapheme.Offset);
            _ = result.Add(grapheme.Offset + grapheme.Length);
        }

        return result;
    }

    private static int GraphemeCount(ReadOnlySpan<char> value)
    {
        var count = 0;

        foreach (var _ in Graphemes.Enumerate(value))
        {
            count++;
        }

        return count;
    }

    private static int Cells(ReadOnlySpan<char> value, Ambiguous ambiguous)
    {
        var result = 0;

        foreach (var grapheme in Graphemes.Enumerate(value))
        {
            var cluster = value.Slice(grapheme.Offset, grapheme.Length);
            result += cluster.Length == 1 && cluster[0] == '\t'
                ? 4 - (result % 4)
                : Width.Measure(cluster, ambiguous).Cells;
        }

        return result;
    }

    private static string Content(Random random)
    {
        ReadOnlySpan<string> tokens =
        [
            "a", " ", "\t", "\r", "\n", "\r\n", "e\u0301", "界", "·", "👩‍💻", "\uFE0F",
            "\uD800", "\uDC00"
        ];
        var result = new StringBuilder();
        var count = random.Next(0, 25);

        for (var index = 0; index < count; index++)
        {
            _ = result.Append(tokens[random.Next(tokens.Length)]);
        }

        return result.ToString();
    }

    private static Line[] Format(
        string content,
        int width,
        Overflow overflow,
        Alignment alignment,
        Ambiguous ambiguous)
    {
        var required = TextLayout.Format(
            content,
            width,
            overflow,
            alignment,
            ambiguous,
            []);
        var result = new Line[required];
        _ = TextLayout.Format(
            content,
            width,
            overflow,
            alignment,
            ambiguous,
            result);
        return result;
    }
}
