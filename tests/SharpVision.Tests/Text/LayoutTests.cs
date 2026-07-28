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

    /// <summary>Verifies zero width consumes no partial or wide source cluster.</summary>
    [Fact]
    public void Format_WhenWidthIsZero_ReturnsEmptyClippedLine()
    {
        var lines = Format("界", width: 0, overflow: Overflow.WrapAnywhere);

        lines.ShouldBe([new Line(1, 0, 0, 0, false)]);
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

    /// <summary>Verifies anywhere overflow breaks only between complete grapheme clusters.</summary>
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
        lines[1].ShouldBe(new Line(3, 0, 0, 0, false));
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
}
