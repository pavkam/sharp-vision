// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

// The project's own namespace, SharpVision.Document.Tests, nests textually under the SharpVision.Document
// segment, so an unqualified "Document" would otherwise resolve to that segment (as a namespace)
// rather than the Document control - this in-namespace alias, unlike a global one, takes priority
// over that enclosing-segment lookup in every position, including local-variable and return types.
using Document = Controls.Documents.Document;

/// <summary>Verifies the projection one <see cref="Document"/> paints for its node tree: block
/// spacing, inline wrapping, hard breaks and tabs, literal code, thematic breaks, block quote bars,
/// markup boundaries, ambiguous-width glyph repair, and live re-layout after a node mutates.</summary>
public sealed class DocumentLayoutSurfaceTests
{
    /// <summary>Verifies sibling blocks are separated by exactly one blank line.</summary>
    [Fact]
    public void Render_WhenBlocksAreSiblings_SeparatesThemWithExactlyOneBlankLine()
    {
        // Arrange
        var document = new Document
        {
            Blocks =
            {
                new DocumentHeading(1, "Title"),
                new DocumentParagraph("One"),
                new DocumentParagraph("Two")
            }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 6));

        // Assert
        probe.Rows().ShouldBe(["Title", string.Empty, "One", string.Empty, "Two", string.Empty]);
    }

    /// <summary>Verifies an empty paragraph occupies exactly one line, which is what makes it a
    /// deliberate way to add vertical space.</summary>
    [Fact]
    public void Render_WhenParagraphIsEmpty_OccupiesExactlyOneLine()
    {
        // Arrange
        var document = new Document
        {
            Blocks = { new DocumentParagraph(), new DocumentParagraph("After") }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 4));

        // Assert - one line for the empty paragraph, one blank separator, then the next block.
        probe.Rows().ShouldBe([string.Empty, string.Empty, "After", string.Empty]);
    }

    /// <summary>Verifies wrapping is greedy and breaks at whitespace rather than mid-word.</summary>
    [Fact]
    public void Render_WhenParagraphExceedsTheWidth_WrapsGreedilyAtWhitespace()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("alpha beta gamma delta") } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 3));

        // Assert
        probe.Rows().ShouldBe(["alpha beta", "gamma delta", string.Empty]);
    }

    /// <summary>Verifies a single token wider than the content width takes a line of its own instead
    /// of being packed beside its neighbors.</summary>
    [Fact]
    public void Render_WhenTokenIsWiderThanTheContentWidth_GivesItItsOwnLine()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("ab supercalifragilistic cd") } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(10, 3));

        // Assert - the over-wide token starts its own line and overflows it; "cd" reflows below.
        probe.Row(0).ShouldBe("ab");
        probe.Row(1).ShouldBe("supercalif");
        probe.Row(2).ShouldBe("cd");
    }

    /// <summary>Verifies wrapping treats each extended grapheme as an indivisible unit across
    /// combining, variation-selector, ZWJ, and wide-cell content.</summary>
    [Theory]
    [InlineData("界")]
    [InlineData("👩‍💻")]
    [InlineData("e\u0301")]
    [InlineData("✈️")]
    public void Render_WhenComplexGraphemesWrap_NeverSplitsACluster(string grapheme)
    {
        // Arrange
        var document = new Document
        {
            Blocks = { new DocumentParagraph($"{grapheme} {grapheme}") }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(2, 2));

        // Assert
        probe.Text(0, 0).ShouldBe(grapheme);
        probe.Text(0, 1).ShouldBe(grapheme);
    }

    /// <summary>Verifies authored leading whitespace survives at a paragraph start and immediately
    /// after a hard break, while whitespace a wrap pushed to the front of a continuation line is
    /// dropped.</summary>
    [Fact]
    public void Render_WhenLeadingWhitespaceIsAuthored_PreservesItButDropsWrappedIndentation()
    {
        // Arrange
        var paragraph = new DocumentParagraph();
        paragraph.Inlines.Add(new DocumentTextRun("  lead alpha beta"));
        paragraph.Inlines.Add(new DocumentLineBreak());
        paragraph.Inlines.Add(new DocumentTextRun("  after"));
        var document = new Document { Blocks = { paragraph } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 3));

        // Assert
        probe.Text(0, 0).ShouldBe(" ");
        probe.Row(0).ShouldBe("  lead alpha");
        probe.Row(1).ShouldBe("beta");
        probe.Row(2).ShouldBe("  after");
    }

    /// <summary>Verifies a trailing hard break leaves a trailing blank line rather than being
    /// swallowed.</summary>
    [Fact]
    public void Render_WhenParagraphEndsWithALineBreak_LeavesATrailingBlankLine()
    {
        // Arrange
        var paragraph = new DocumentParagraph();
        paragraph.Inlines.Add(new DocumentTextRun("only"));
        paragraph.Inlines.Add(new DocumentLineBreak());
        var document = new Document { Blocks = { paragraph, new DocumentParagraph("next") } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 4));

        // Assert - line 1 is the paragraph's own trailing blank, line 2 the sibling separator.
        probe.Rows().ShouldBe(["only", string.Empty, string.Empty, "next"]);
    }

    /// <summary>Verifies a newline inside a text run is a hard break and a tab advances four cells
    /// without displacing the columns that follow it.</summary>
    [Fact]
    public void Render_WhenRunContainsNewlineAndTab_BreaksAndAdvancesWithoutCorruptingColumns()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("a\nb\tc\td") } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(16, 2));

        // Assert
        probe.Row(0).ShouldBe("a");
        probe.Text(0, 1).ShouldBe("b");
        probe.Text(5, 1).ShouldBe("c");
        probe.Text(10, 1).ShouldBe("d");
        probe.Row(1).ShouldBe("b    c    d");
    }

    /// <summary>Verifies a code block is literal: markup tags render as characters rather than being
    /// parsed into styling.</summary>
    [Fact]
    public void Render_WhenCodeBlockContainsMarkup_RendersTheTagCharactersLiterally()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentCodeBlock("<b>x</b>") } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 1));

        // Assert
        probe.Row(0).ShouldBe("<b>x</b>");
        (probe.Cell(4, 0).Style.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies a code block splits on every line-break form and treats a CRLF pair as one
    /// break rather than two.</summary>
    [Fact]
    public void Render_WhenCodeBlockContainsMixedLineBreaks_SplitsEachIntoOneLine()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentCodeBlock("one\r\ntwo\rthree\nfour") } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 5));

        // Assert
        probe.Rows().ShouldBe(["one", "two", "three", "four", string.Empty]);
    }

    /// <summary>Verifies code tabs advance to cell-based stops after wide and combining graphemes,
    /// rather than using UTF-16 length as terminal geometry.</summary>
    [Theory]
    [InlineData("界\tX")]
    [InlineData("e\u0301\tX")]
    public void Render_WhenCodeTabFollowsComplexText_UsesTheNextCellStop(string text)
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentCodeBlock(text) } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(8, 1));

        // Assert
        probe.Text(4, 0).ShouldBe("X");
    }

    /// <summary>Verifies a code line longer than the content width is clipped rather than wrapped,
    /// because re-flowing code changes its meaning.</summary>
    [Fact]
    public void Render_WhenCodeLineExceedsTheWidth_ClipsItInsteadOfWrapping()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentCodeBlock("abcdefghij\nnext") } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(5, 3));

        // Assert
        probe.Row(0).ShouldBe("abcde");
        probe.Row(1).ShouldBe("next");
    }

    /// <summary>Verifies a thematic break spans the full width available at its nesting level and
    /// stops at a block quote's indent when nested inside one.</summary>
    [Fact]
    public void Render_WhenSeparatorIsNestedInAQuote_DrawsANarrowerRule()
    {
        // Arrange
        var quote = new DocumentBlockQuote();
        quote.Blocks.Add(new DocumentSeparator());
        var document = new Document { Blocks = { new DocumentSeparator(), quote } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(10, 3));

        // Assert - the root rule spans all ten cells; the quoted rule starts after the two-cell indent.
        probe.Row(0).ShouldBe(new string('\u2500', 10));
        probe.Row(2).ShouldBe("\u2502 " + new string('\u2500', 8));
    }

    /// <summary>Verifies a block quote indents its content by two cells and draws its bar on every
    /// line it spans, including wrapped continuations.</summary>
    [Fact]
    public void Render_WhenQuotedContentWraps_DrawsTheBarOnEveryLine()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentBlockQuote("alpha beta gamma") } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(10, 3));

        // Assert
        probe.Rows().ShouldBe(["\u2502 alpha", "\u2502 beta", "\u2502 gamma"]);
    }

    /// <summary>Verifies an empty quote's measured extent contains the bar it paints.</summary>
    [Fact]
    public void Render_WhenQuoteIsEmpty_IncludesItsBarInTheContentExtent()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentBlockQuote() } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(4, 1));

        // Assert: the fallback blank line reserves the same indent real quoted content would -
        // indent plus the quote's own two-column bar-and-space gutter - not one column less, so
        // a genuinely empty quote and a quote containing one empty paragraph report the same
        // extent.
        probe.Row(0).ShouldBe("\u2502");
        document.Extent.Width.ShouldBe(2);
    }

    /// <summary>Verifies a callout title flows under its indent and keeps the bar on every
    /// continuation rather than clipping inaccessible title text.</summary>
    [Fact]
    public void Render_WhenCalloutTitleExceedsWidth_WrapsTheWholeTitle()
    {
        // Arrange
        var document = new Document
        {
            Blocks = { new DocumentCallout { Kind = "NOTE", Title = "alpha beta" } }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(9, 3));

        // Assert
        probe.Rows().ShouldBe(["\u2502 [NOTE]", "\u2502 alpha", "\u2502 beta"]);
    }

    /// <summary>Verifies a quote nested inside a quote indents twice and draws two bars on every
    /// line.</summary>
    [Fact]
    public void Render_WhenQuotesNest_DrawsOneBarPerLevel()
    {
        // Arrange
        var inner = new DocumentBlockQuote("deep text");
        var document = new Document { Blocks = { new DocumentBlockQuote(inner) } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 2));

        // Assert
        probe.Rows().ShouldBe(["\u2502 \u2502 deep", "\u2502 \u2502 text"]);
    }

    /// <summary>Verifies markup styling applies at exact character boundaries, so a tag that opens
    /// mid-word styles only the characters it actually covers.</summary>
    [Fact]
    public void Render_WhenTagOpensMidWord_StylesOnlyTheCoveredCharacters()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("pre<b>post</b>") } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 1));

        // Assert
        probe.Row(0).ShouldBe("prepost");

        for (var column = 0; column < 3; column++)
        {
            (probe.Cell(column, 0).Style.Attributes & TerminalAttributes.Bold)
                .ShouldBe(TerminalAttributes.None);
        }

        for (var column = 3; column < 7; column++)
        {
            (probe.Cell(column, 0).Style.Attributes & TerminalAttributes.Bold)
                .ShouldBe(TerminalAttributes.Bold);
        }
    }

    /// <summary>Verifies a tag that closes immediately before punctuation leaves that punctuation
    /// unstyled rather than extending the span to the whole whitespace-delimited token.</summary>
    [Fact]
    public void Render_WhenTagClosesBeforePunctuation_LeavesThePunctuationUnstyled()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("go <b>fast</b>, ok") } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(16, 1));

        // Assert
        probe.Row(0).ShouldBe("go fast, ok");
        probe.Text(7, 0).ShouldBe(",");
        (probe.Cell(6, 0).Style.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.Bold);
        (probe.Cell(7, 0).Style.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies a wide ambiguous-width policy repairs every glyph that would no longer fit
    /// one cell - the bullet, the quote bar, and the rule - to its code-owned ASCII value, leaves the
    /// glyphs that still measure one cell as authored, and neither faults nor shifts the columns
    /// beside any of them.</summary>
    [Fact]
    public void Render_WhenAmbiguousWidthIsWide_RepairsOnlyTheGlyphsThatNoLongerFitOneCell()
    {
        // Arrange
        var third = new DocumentList { Items = { new DocumentListItem("C") } };
        var second = new DocumentList { Items = { new DocumentListItem("B", third) } };
        var first = new DocumentList { Items = { new DocumentListItem("A", second) } };
        var document = new Document
        {
            Blocks = { first, new DocumentBlockQuote("Q"), new DocumentSeparator() }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(8, 7), Ambiguous.Wide);

        // Assert - the ambiguous bullet, bar, and rule degrade; the two narrow nested bullets do not.
        probe.Rows().ShouldBe([
            "* A",
            "  \u25E6 B",
            "    \u25AA C",
            string.Empty,
            "| Q",
            string.Empty,
            "--------"
        ]);
    }

    /// <summary>Verifies a theme that replaces a bullet with an East Asian Ambiguous glyph still
    /// degrades to that bullet's own code-owned repair value under a wide policy, so every member of
    /// the family - not only the ones that happen to be ambiguous by default - stays one cell.</summary>
    [Fact]
    public void Render_WhenNestedBulletsAreAmbiguous_RepairsEachToItsOwnAsciiFallback()
    {
        // Arrange - U+25CB and U+25A0 are East Asian Ambiguous, so a wide policy makes both two cells.
        var third = new DocumentList { Items = { new DocumentListItem("C") } };
        var second = new DocumentList { Items = { new DocumentListItem("B", third) } };
        var first = new DocumentList { Items = { new DocumentListItem("A", second) } };
        var document = new Document
        {
            Blocks = { first },
            Style = DocumentStyle.Default with
            {
                Glyphs = DocumentGlyphs.Default with
                {
                    SecondBullet = new Rune('\u25CB'),
                    ThirdBullet = new Rune('\u25A0')
                }
            }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(10, 3), Ambiguous.Wide);

        // Assert
        probe.Rows().ShouldBe(["* A", "  o B", "    + C"]);
    }

    /// <summary>Verifies changing a mounted document's text run re-lays-out and repaints on the next
    /// frame with no other action.</summary>
    [Fact]
    public async Task Text_WhenTextRunChangesInAMountedDocument_RelaysOutAndRepaintsAsync()
    {
        // Arrange
        var run = new DocumentTextRun("one two");
        var paragraph = new DocumentParagraph();
        paragraph.Inlines.Add(run);
        var document = new Document { Blocks = { paragraph } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(8, 3),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 1)).Text.ShouldBe(" ");

        // Act
        await surface.UpdateAsync(() => run.Text = "one two three", "replace run text");

        // Assert - the longer text now wraps onto a second line.
        surface.Cell(new Point(0, 0)).Text.ShouldBe("o");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("t");
    }

    /// <summary>Verifies raising a mounted heading's level from a major to a minor level repaints it
    /// in the body face with bold weight instead of the accent heading face.</summary>
    [Fact]
    public async Task Level_WhenHeadingLevelChangesInAMountedDocument_RepaintsWithTheOtherFaceAsync()
    {
        // Arrange
        var heading = new DocumentHeading(1, "T");
        var document = new Document { Blocks = { heading } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(8, 1),
            TestContext.Current.CancellationToken);
        var major = surface.Cell(new Point(0, 0)).Style;

        // Act
        await surface.UpdateAsync(() => heading.Level = 3, "demote heading");

        // Assert
        var minor = surface.Cell(new Point(0, 0)).Style;
        minor.ShouldNotBe(major);
        (minor.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.Bold);
    }

    /// <summary>Verifies switching a mounted list's marker style and looseness re-lays-out the whole
    /// list on the next frame.</summary>
    [Fact]
    public async Task Kind_WhenListStateChangesInAMountedDocument_RelaysOutTheMarkersAndSpacingAsync()
    {
        // Arrange
        var list = new DocumentList
        {
            Items = { new DocumentListItem("First"), new DocumentListItem("Second") }
        };
        var document = new Document { Blocks = { list } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("\u2022");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("\u2022");

        // Act
        await surface.UpdateAsync(
            () =>
            {
                list.Kind = DocumentListKind.Numbered;
                list.IsLoose = true;
            },
            "renumber and loosen list");

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("1");
        surface.Cell(new Point(1, 0)).Text.ShouldBe(".");
        surface.Cell(new Point(0, 1)).Text.ShouldBe(" ");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("2");
    }

    /// <summary>Verifies replacing a mounted code block's text repaints its literal lines.</summary>
    [Fact]
    public async Task Text_WhenCodeBlockChangesInAMountedDocument_RepaintsItsLiteralLinesAsync()
    {
        // Arrange
        var code = new DocumentCodeBlock("first");
        var document = new Document { Blocks = { code } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(10, 2),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("f");

        // Act
        await surface.UpdateAsync(() => code.Text = "one\ntwo", "replace code text");

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("o");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("t");
    }

    /// <summary>Verifies changing a mounted link's text re-lays-out the surrounding flow and repaints
    /// the link, and that disabling it repaints without moving the text beside it.</summary>
    [Fact]
    public async Task Text_WhenLinkChangesInAMountedDocument_RelaysOutTheFlowAndRepaintsAsync()
    {
        // Arrange
        var link = new DocumentLink("go");
        var paragraph = new DocumentParagraph();
        paragraph.Inlines.Add(link);
        paragraph.Inlines.Add(new DocumentTextRun(" end"));
        var document = new Document { Blocks = { paragraph } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(3, 0)).Text.ShouldBe("e");

        // Act
        await surface.UpdateAsync(() => link.Text = "gone", "replace link text");

        // Assert - the following run shifted right by the two extra cells the link now occupies.
        surface.Cell(new Point(0, 0)).Text.ShouldBe("g");
        surface.Cell(new Point(5, 0)).Text.ShouldBe("e");

        // Act
        var before = surface.Cell(new Point(0, 0)).Style;
        await surface.UpdateAsync(() => link.IsEnabled = false, "disable link");

        // Assert
        surface.Cell(new Point(0, 0)).Style.ShouldNotBe(before);
        surface.Cell(new Point(5, 0)).Text.ShouldBe("e");
    }

    /// <summary>Verifies adding a block to a mounted document's own collection re-lays-out and paints
    /// the new content without any explicit invalidation from the caller.</summary>
    [Fact]
    public async Task Blocks_WhenABlockIsAddedToAMountedDocument_PaintsItOnTheNextFrameAsync()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("First") } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 2)).Text.ShouldBe(" ");

        // Act
        await surface.UpdateAsync(() => document.Blocks.Add(new DocumentParagraph("Second")), "append block");

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("F");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("S");
    }
}
