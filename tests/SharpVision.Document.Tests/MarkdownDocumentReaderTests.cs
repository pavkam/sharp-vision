// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

using SharpVision.Text;

using DocumentControl = Controls.Documents.Document;

/// <summary>Verifies native Markdown parsing and independently selectable extensions.</summary>
public sealed class MarkdownDocumentReaderTests
{
    /// <summary>Verifies representative CommonMark blocks and semantic inlines become document nodes.</summary>
    [Fact]
    public void Read_WhenCommonMarkSourceIsProvided_ProducesSemanticDocumentTree()
    {
        // Arrange
        const string source = """
            # Heading

            Plain *emphasis* and **strong** with [link](https://example.invalid).

            - first
            - second

            > quoted

            ```csharp
            var answer = 42;
            ```
            """;
        var reader = new MarkdownDocumentReader();

        // Act
        var result = reader.Read(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.Blocks.Count.ShouldBe(5);
        _ = result.Blocks[0].ShouldBeOfType<DocumentHeading>();
        var paragraph = result.Blocks[1].ShouldBeOfType<DocumentParagraph>();
        paragraph.Inlines.ShouldContain(static inline => inline is DocumentEmphasis);
        paragraph.Inlines.ShouldContain(static inline => inline is DocumentStrong);
        paragraph.Inlines.ShouldContain(static inline => inline is DocumentLink);
        _ = result.Blocks[2].ShouldBeOfType<DocumentList>();
        _ = result.Blocks[3].ShouldBeOfType<DocumentBlockQuote>();
        result.Blocks[4].ShouldBeOfType<DocumentCodeBlock>().Language.ShouldBe("csharp");
    }

    /// <summary>Verifies an ATX heading interrupts a preceding paragraph without a blank line.</summary>
    [Fact]
    public void Read_WhenHeadingFollowsParagraph_ProducesSeparateBlocks()
    {
        // Arrange
        const string source = "Paragraph\n# Heading";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks.Count.ShouldBe(2);
        _ = result.Blocks[0].ShouldBeOfType<DocumentParagraph>();
        result.Blocks[1].ShouldBeOfType<DocumentHeading>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("Heading");
    }

    /// <summary>Verifies a block quote marker accepts only the zero-to-three-space indentation
    /// range defined by CommonMark.</summary>
    [Theory]
    [InlineData("> quote", true)]
    [InlineData(" > quote", true)]
    [InlineData("  > quote", true)]
    [InlineData("   > quote", true)]
    [InlineData("    > quote", false)]
    public void Read_WhenBlockQuoteMarkerIndentVaries_RecognizesOnlyUpToThreeSpaces(
        string source,
        bool expectedQuote)
    {
        // Arrange and act
        var block = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem();

        // Assert
        if (expectedQuote)
        {
            block.ShouldBeOfType<DocumentBlockQuote>().Blocks.ShouldHaveSingleItem()
                .ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
                .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("quote");
        }
        else
        {
            block.ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
                .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe(source);
        }
    }

    /// <summary>Verifies an over-indented marker neither interrupts a paragraph nor remains inside
    /// a preceding block quote.</summary>
    [Fact]
    public void Read_WhenOverIndentedBlockQuoteMarkerFollowsContent_PreservesLiteralParagraphContent()
    {
        // Arrange
        const string source = "paragraph\n    > literal\n\n> quoted\n    > sibling";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks.Count.ShouldBe(3);
        var paragraph = result.Blocks[0].ShouldBeOfType<DocumentParagraph>();
        paragraph.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("> literal");

        var quote = result.Blocks[1].ShouldBeOfType<DocumentBlockQuote>();
        quote.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines
            .ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("quoted");
        result.Blocks[2].ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("    > sibling");
    }

    /// <summary>Verifies the largest permitted ordered marker remains a numbered list marker.</summary>
    [Fact]
    public void Read_WhenOrderedMarkerHasNineDigits_ProducesNumberedList()
    {
        // Arrange
        const string source = "123456789. item";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        var list = result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentList>();
        list.Kind.ShouldBe(DocumentListKind.Numbered);
        list.Start.ShouldBe(123456789);
    }

    /// <summary>Verifies a ten-digit prefix remains ordinary paragraph text instead of becoming an
    /// ordered list marker.</summary>
    [Fact]
    public void Read_WhenOrderedMarkerHasTenDigits_PreservesParagraphText()
    {
        // Arrange
        const string source = "1234567890. item";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines
            .ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe(source);
    }

    /// <summary>Verifies an ordered list starting at one may interrupt an open paragraph.</summary>
    [Fact]
    public void Read_WhenOrderedListStartingAtOneFollowsParagraph_ProducesSeparateBlocks()
    {
        // Arrange
        const string source = "paragraph\n1. item";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks.Count.ShouldBe(2);
        _ = result.Blocks[0].ShouldBeOfType<DocumentParagraph>();
        result.Blocks[1].ShouldBeOfType<DocumentList>().Start.ShouldBe(1);
    }

    /// <summary>Verifies a non-one ordered start cannot interrupt an open paragraph but remains a
    /// valid marker when it begins a later block.</summary>
    [Fact]
    public void Read_WhenOrderedListStartingAboveOneFollowsParagraph_PreservesParagraphUntilBlankLine()
    {
        // Arrange
        const string source = "paragraph\n2. prose\n\n2. item";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks.Count.ShouldBe(2);
        var paragraph = result.Blocks[0].ShouldBeOfType<DocumentParagraph>();
        paragraph.Inlines.Count.ShouldBe(3);
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("paragraph");
        _ = paragraph.Inlines[1].ShouldBeOfType<DocumentSoftBreak>();
        paragraph.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("2. prose");
        result.Blocks[1].ShouldBeOfType<DocumentList>().Start.ShouldBe(2);
    }

    /// <summary>Verifies each bullet delimiter can form a standalone empty list item, including
    /// when trailing whitespace follows the marker.</summary>
    [Theory]
    [InlineData("-")]
    [InlineData("+")]
    [InlineData("*")]
    [InlineData("-\t")]
    public void Read_WhenEmptyBulletMarkerIsStandalone_ProducesEmptyListItem(string source)
    {
        // Arrange
        var reader = new MarkdownDocumentReader();

        // Act
        var result = reader.Read(source);

        // Assert
        var list = result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentList>();
        list.Kind.ShouldBe(DocumentListKind.Bulleted);
        list.Items.ShouldHaveSingleItem().Blocks.ShouldBeEmpty();
    }

    /// <summary>Verifies both ordered delimiters can form standalone empty items and preserve the
    /// authored start value.</summary>
    [Theory]
    [InlineData("1.", 1)]
    [InlineData("2)", 2)]
    [InlineData("3.\t", 3)]
    public void Read_WhenEmptyOrderedMarkerIsStandalone_ProducesEmptyListItem(string source, int start)
    {
        // Arrange
        var reader = new MarkdownDocumentReader();

        // Act
        var result = reader.Read(source);

        // Assert
        var list = result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentList>();
        list.Kind.ShouldBe(DocumentListKind.Numbered);
        list.Start.ShouldBe(start);
        list.Items.ShouldHaveSingleItem().Blocks.ShouldBeEmpty();
    }

    /// <summary>Verifies empty ordered items remain in their authored middle position.</summary>
    [Fact]
    public void Read_WhenOrderedListContainsEmptyMiddleItem_PreservesAllItems()
    {
        // Arrange
        const string source = "1. first\n2.\n3. third";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        var list = result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentList>();
        list.Items.Count.ShouldBe(3);
        list.Items[0].Blocks.ShouldNotBeEmpty();
        list.Items[1].Blocks.ShouldBeEmpty();
        list.Items[2].Blocks.ShouldNotBeEmpty();
    }

    /// <summary>Verifies empty bullet items remain at both list boundaries.</summary>
    [Fact]
    public void Read_WhenBulletListStartsAndEndsWithEmptyItems_PreservesAllItems()
    {
        // Arrange
        const string source = "-\n- middle\n-";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        var list = result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentList>();
        list.Items.Count.ShouldBe(3);
        list.Items[0].Blocks.ShouldBeEmpty();
        list.Items[1].Blocks.ShouldNotBeEmpty();
        list.Items[2].Blocks.ShouldBeEmpty();
    }

    /// <summary>Verifies an empty bullet or ordered item cannot interrupt paragraph continuation
    /// text.</summary>
    [Theory]
    [InlineData("*")]
    [InlineData("1.")]
    [InlineData("*\t")]
    public void Read_WhenEmptyListMarkerFollowsParagraph_PreservesParagraph(string marker)
    {
        // Arrange
        var source = $"paragraph\n{marker}";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        var paragraph = result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();
        paragraph.Inlines.Count.ShouldBe(3);
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("paragraph");
        _ = paragraph.Inlines[1].ShouldBeOfType<DocumentSoftBreak>();
        paragraph.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe(marker.TrimEnd(' ', '\t'));
    }

    /// <summary>Verifies disabled non-standard syntax remains ordinary visible source.</summary>
    [Fact]
    public void Read_WhenExtensionsAreDisabled_PreservesNonStandardSyntaxLiterally()
    {
        // Arrange
        const string source = "[[Target|Alias]]\n\n- [ ] task\n- ( ) choice";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks[0].ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("[[Target|Alias]]");
        var list = result.Blocks[1].ShouldBeOfType<DocumentList>();
        _ = list.Items[0].Blocks[0].ShouldBeOfType<DocumentParagraph>();
        _ = list.Items[1].Blocks[0].ShouldBeOfType<DocumentParagraph>();
    }

    /// <summary>Verifies wiki links, task items, radio items, and callouts are individually opt-in.</summary>
    [Fact]
    public void Read_WhenOptionalExtensionsAreEnabled_ProducesInteractiveAndCalloutNodes()
    {
        // Arrange
        const string source = """
            [[Target#Part|Alias]]

            - [x] complete
            - [ ] pending

            - ( ) Alpha
            - (x) Beta

            > [!NOTE] Read this
            > Callout body.
            """;
        var reader = new MarkdownDocumentReader(new MarkdownOptions
        {
            Extensions = MarkdownExtension.WikiLinks |
                         MarkdownExtension.TaskLists |
                         MarkdownExtension.RadioLists |
                         MarkdownExtension.Callouts
        });

        // Act
        var result = reader.Read(source);

        // Assert
        var wiki = result.Blocks[0].ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentLink>();
        wiki.Text.ShouldBe("Alias");
        wiki.Target.ShouldBe("Target#Part");

        var tasks = result.Blocks[1].ShouldBeOfType<DocumentList>();
        tasks.Items[0].Blocks[0].ShouldBeOfType<DocumentBlockControl>().Control
            .ShouldBeOfType<CheckBox>().IsChecked.ShouldBe(true);

        var radios = result.Blocks[2].ShouldBeOfType<DocumentList>();
        var first = radios.Items[0].Blocks[0].ShouldBeOfType<DocumentBlockControl>().Control
            .ShouldBeOfType<RadioButton>();
        var second = radios.Items[1].Blocks[0].ShouldBeOfType<DocumentBlockControl>().Control
            .ShouldBeOfType<RadioButton>();
        first.GroupName.ShouldBe(second.GroupName);
        second.IsChecked.ShouldBeTrue();

        var callout = result.Blocks[3].ShouldBeOfType<DocumentCallout>();
        callout.Kind.ShouldBe("NOTE");
        callout.Title.ShouldBe("Read this");
        callout.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("Callout body.");
    }

    /// <summary>Verifies malformed source with several selected radios normalizes to the last
    /// authored selection before the controls are mounted.</summary>
    [Fact]
    public void Read_WhenRadioGroupHasSeveralCheckedItems_KeepsOnlyTheLastSelection()
    {
        // Arrange
        var reader = new MarkdownDocumentReader(new MarkdownOptions
        {
            Extensions = MarkdownExtension.RadioLists
        });

        // Act
        var list = reader.Read("- (x) First\n- (x) Second").Blocks[0].ShouldBeOfType<DocumentList>();
        var first = list.Items[0].Blocks[0].ShouldBeOfType<DocumentBlockControl>().Control
            .ShouldBeOfType<RadioButton>();
        var second = list.Items[1].Blocks[0].ShouldBeOfType<DocumentBlockControl>().Control
            .ShouldBeOfType<RadioButton>();

        // Assert
        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeTrue();
    }

    /// <summary>Verifies loading replaces a document's previous tree through the format abstraction.</summary>
    [Fact]
    public void Load_WhenMarkdownReaderSucceeds_ReplacesTheCurrentBlocks()
    {
        // Arrange
        var document = new DocumentControl
        {
            Blocks = { new DocumentParagraph { Inlines = { new DocumentTextRun("old") } } }
        };

        // Act
        var result = document.Load("# New", new MarkdownDocumentReader());

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        document.Blocks.Count.ShouldBe(1);
        document.Blocks[0].ShouldBeOfType<DocumentHeading>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("New");
    }

    /// <summary>Verifies the GFM table extension records header status and per-column alignment.</summary>
    [Fact]
    public void Read_WhenTableExtensionIsEnabled_ProducesAlignedTableCells()
    {
        // Arrange
        const string source = """
            | Name | Count | State |
            | :--- | ---: | :---: |
            | Alpha | 12 | ready |
            """;
        var reader = new MarkdownDocumentReader(new MarkdownOptions
        {
            Extensions = MarkdownExtension.Tables
        });

        // Act
        var result = reader.Read(source);

        // Assert
        var table = result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentTable>();
        table.Rows.Count.ShouldBe(2);
        table.Rows[0].IsHeader.ShouldBeTrue();
        table.Rows[1].Cells[0].Alignment.ShouldBe(DocumentTableCellAlignment.Left);
        table.Rows[1].Cells[1].Alignment.ShouldBe(DocumentTableCellAlignment.Right);
        table.Rows[1].Cells[2].Alignment.ShouldBe(DocumentTableCellAlignment.Center);
    }

    /// <summary>Verifies extended URL recognition is isolated from the strikethrough extension.</summary>
    [Fact]
    public void Read_WhenOnlyAutolinksAreEnabled_LeavesStrikethroughLiteralAndCreatesUrlLink()
    {
        // Arrange
        var reader = new MarkdownDocumentReader(new MarkdownOptions
        {
            Extensions = MarkdownExtension.Autolinks
        });

        // Act
        var paragraph = reader.Read("Visit https://example.invalid and ~~keep~~.")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.Any(static inline => inline is DocumentLink
        {
            Target: "https://example.invalid"
        }).ShouldBeTrue();
        paragraph.Inlines.Any(static inline => inline is DocumentStrikethrough).ShouldBeFalse();
    }

    /// <summary>Verifies malformed callout markers remain readable quote content.</summary>
    [Fact]
    public void Read_WhenCalloutKindIsWhitespace_PreservesHeaderAsBlockQuote()
    {
        // Arrange
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.Callouts });

        // Act
        var block = reader.Read("> [! ]").Blocks.ShouldHaveSingleItem();

        // Assert
        block.ShouldBeOfType<DocumentBlockQuote>().Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("[! ]");
    }

    /// <summary>Verifies a closing fence must match the opening marker length and grammar.</summary>
    [Fact]
    public void Read_WhenLongFenceContainsShortOrSuffixedFence_PreservesThoseLinesAsCode()
    {
        // Arrange
        const string source = "````csharp\na\n```\n```not-close\nb\n````";

        // Act
        var code = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentCodeBlock>();

        // Assert
        code.Language.ShouldBe("csharp");
        code.Text.ShouldBe("a\n```\n```not-close\nb");
    }

    /// <summary>Verifies a backtick fence with a forbidden backtick in its info string remains
    /// ordinary continuation text inside the surrounding paragraph.</summary>
    [Fact]
    public void Read_WhenInvalidBacktickFenceFollowsParagraph_PreservesOneParagraph()
    {
        // Arrange
        const string source = "paragraph\n```bad`info\ncontinuation";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        var paragraph = result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("paragraph");
        paragraph.Inlines.OfType<DocumentSoftBreak>().Count().ShouldBe(2);
        paragraph.Inlines[^1].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("continuation");
    }

    /// <summary>Verifies a fence-like line beyond the three-space block indent cannot interrupt
    /// an open paragraph for either fence marker.</summary>
    [Theory]
    [InlineData("    ```text")]
    [InlineData("    ~~~text")]
    public void Read_WhenOverIndentedFenceFollowsParagraph_PreservesOneParagraph(string fence)
    {
        // Arrange
        var source = $"paragraph\n{fence}\ncontinuation";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        var paragraph = result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();
        paragraph.Inlines.Count.ShouldBe(5);
        paragraph.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe(fence.TrimStart(' ', '\t'));
        paragraph.Inlines[4].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("continuation");
    }

    /// <summary>Verifies a valid opener still interrupts a paragraph and produces a code block.</summary>
    [Fact]
    public void Read_WhenValidFenceFollowsParagraph_ProducesParagraphAndCodeBlock()
    {
        // Arrange
        const string source = "paragraph\n```text\ncode\n```";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks.Count.ShouldBe(2);
        _ = result.Blocks[0].ShouldBeOfType<DocumentParagraph>();
        var code = result.Blocks[1].ShouldBeOfType<DocumentCodeBlock>();
        code.Language.ShouldBe("text");
        code.Text.ShouldBe("code");
    }

    /// <summary>Verifies baseline punctuation escapes suppress inline syntax and hide the escape slash.</summary>
    [Fact]
    public void Read_WhenPunctuationIsBackslashEscaped_PreservesLiteralText()
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read(@"\*literal\* and \[label](target)")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text
            .ShouldBe("*literal* and [label](target)");
    }

    /// <summary>Verifies double-underscore strong emphasis - the extremely common alternative
    /// spelling to <c>**</c> - is recognized, matching CommonMark's own two equivalent strong
    /// delimiters.</summary>
    [Fact]
    public void Read_WhenTextUsesDoubleUnderscoreStrong_CreatesDocumentStrong()
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read("__bold__")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();

        // Assert
        var strong = paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentStrong>();
        strong.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("bold");
    }

    /// <summary>Verifies triple-underscore strong-and-emphasized text - the underscore equivalent
    /// to <c>***</c> - nests an emphasis inline inside a strong inline.</summary>
    [Fact]
    public void Read_WhenTextUsesTripleUnderscoreStrongAndEmphasis_NestsEmphasisInsideStrong()
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read("___both___")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();

        // Assert
        var strong = paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentStrong>();
        var emphasis = strong.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentEmphasis>();
        emphasis.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("both");
    }

    /// <summary>Verifies a bare thematic-break line produces a DocumentSeparator.</summary>
    [Fact]
    public void Read_WhenLineIsThreeAsterisks_ProducesThematicBreak()
    {
        // Arrange and act
        var block = new MarkdownDocumentReader().Read("***").Blocks.ShouldHaveSingleItem();

        // Assert
        _ = block.ShouldBeOfType<DocumentSeparator>();
    }

    /// <summary>Verifies spaces and tabs may separate each thematic-break marker family after an
    /// otherwise valid zero-to-three-space indentation prefix.</summary>
    [Theory]
    [InlineData("*\t*\t*")]
    [InlineData("- \t-\t -")]
    [InlineData("  _\t_ _\t")]
    public void Read_WhenThematicBreakMarkersUseTabs_ProducesSeparator(string source)
    {
        // Arrange and act
        var block = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem();

        // Assert
        _ = block.ShouldBeOfType<DocumentSeparator>();
    }

    /// <summary>Verifies a tab-separated thematic break uses the same recognition path when it
    /// interrupts an open paragraph.</summary>
    [Fact]
    public void Read_WhenTabSeparatedThematicBreakFollowsParagraph_ProducesSeparateBlocks()
    {
        // Arrange and act
        var blocks = new MarkdownDocumentReader().Read("paragraph\n*\t*\t*").Blocks;

        // Assert
        blocks.Count.ShouldBe(2);
        _ = blocks[0].ShouldBeOfType<DocumentParagraph>();
        _ = blocks[1].ShouldBeOfType<DocumentSeparator>();
    }

    /// <summary>Verifies a leading tab remains structural indentation rather than being discarded
    /// as an interior thematic-break separator.</summary>
    [Fact]
    public void Read_WhenThematicBreakStartsAfterTab_PreservesLiteralParagraphText()
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read("\t*\t*\t*").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("\t*\t*\t*");
    }

    /// <summary>Verifies a thematic-break-looking line indented four or more spaces - CommonMark's
    /// indented-code-block threshold - is not treated as a rule, matching every other
    /// indent-sensitive block-start detector in this reader.</summary>
    [Fact]
    public void Read_WhenRuleLineHasFourSpaceIndent_DoesNotProduceThematicBreak()
    {
        // Arrange and act
        var blocks = new MarkdownDocumentReader().Read("paragraph\n    ***").Blocks;

        // Assert
        blocks.ShouldNotContain(static block => block is DocumentSeparator);
    }

    /// <summary>Verifies intraword underscores are ordinary text rather than emphasis delimiters.</summary>
    [Fact]
    public void Read_WhenUnderscoresAreIntraword_DoesNotCreateEmphasis()
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read("foo_bar_baz")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("foo_bar_baz");
    }

    /// <summary>Verifies whitespace-adjacent delimiter runs cannot open or close emphasis at any
    /// supported asterisk or underscore run length.</summary>
    [Theory]
    [InlineData("x * foo*")]
    [InlineData("*foo *")]
    [InlineData("** foo**")]
    [InlineData("**foo **")]
    [InlineData("*** foo***")]
    [InlineData("***foo ***")]
    [InlineData("_ foo_")]
    [InlineData("_foo _")]
    [InlineData("__ foo__")]
    [InlineData("__foo __")]
    [InlineData("___ foo___")]
    [InlineData("___foo ___")]
    public void Read_WhenEmphasisDelimiterIsWhitespaceAdjacent_PreservesLiteralRuns(string source)
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe(source);
    }

    /// <summary>Verifies punctuation-sensitive opening follows CommonMark's left-flanking rule
    /// rather than treating every matching asterisk as emphasis.</summary>
    [Theory]
    [InlineData("a*\"foo\"*")]
    [InlineData("a_\"foo\"_")]
    [InlineData("a*“foo”*")]
    [InlineData("a*😀foo*")]
    public void Read_WhenEmphasisOpenerIsBetweenTextAndPunctuation_PreservesLiteralRuns(string source)
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe(source);
    }

    /// <summary>Verifies whitespace before a punctuation-followed opener satisfies the
    /// punctuation exception in CommonMark's left-flanking rule.</summary>
    [Theory]
    [InlineData("a *\"foo\"*", "\"foo\"")]
    [InlineData("a _\"foo\"_", "\"foo\"")]
    [InlineData("a *“foo”*", "“foo”")]
    [InlineData("a *😀foo*", "😀foo")]
    public void Read_WhenPunctuationFollowedEmphasisOpenerFollowsWhitespace_CreatesEmphasis(
        string source,
        string expected)
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.Count.ShouldBe(2);
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("a ");
        paragraph.Inlines[1].ShouldBeOfType<DocumentEmphasis>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe(expected);
    }

    /// <summary>Verifies an underscore that is both left- and right-flanking inside a word is
    /// skipped as a closer so the later valid delimiter closes the original run.</summary>
    [Fact]
    public void Read_WhenIntrawordUnderscorePrecedesValidCloser_ClosesAtValidDelimiter()
    {
        // Arrange and act
        var emphasis = new MarkdownDocumentReader().Read("_foo_bar_").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentEmphasis>();

        // Assert
        emphasis.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("foo_bar");
    }

    /// <summary>Verifies a code span may use a longer delimiter to contain a shorter backtick run.</summary>
    [Fact]
    public void Read_WhenCodeSpanUsesLongDelimiter_PreservesShortBackticks()
    {
        // Arrange and act
        var code = new MarkdownDocumentReader().Read("``a ` b``")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentCodeSpan>();

        // Assert
        code.Text.ShouldBe("a ` b");
    }

    /// <summary>Verifies delimiter-based inline containers retain their parsing state across a
    /// soft line ending and own the resulting soft-break node.</summary>
    [Theory]
    [InlineData("*foo\nbar*", "emphasis")]
    [InlineData("**foo\nbar**", "strong")]
    [InlineData("~~foo\nbar~~", "strikethrough")]
    public void Read_WhenDelimitedInlineSpansSoftBreak_CreatesOneContainer(string source, string expectedKind)
    {
        // Arrange
        var extensions = expectedKind == "strikethrough"
            ? MarkdownExtension.Strikethrough
            : MarkdownExtension.None;

        // Act
        var inline = new MarkdownDocumentReader(new MarkdownOptions { Extensions = extensions }).Read(source)
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem();

        // Assert
        var inlines = expectedKind switch
        {
            "emphasis" => inline.ShouldBeOfType<DocumentEmphasis>().Inlines,
            "strong" => inline.ShouldBeOfType<DocumentStrong>().Inlines,
            "strikethrough" => inline.ShouldBeOfType<DocumentStrikethrough>().Inlines,
            _ => throw new InvalidOperationException($"Unexpected inline kind '{expectedKind}'.")
        };
        inlines.Count.ShouldBe(3);
        inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("foo");
        _ = inlines[1].ShouldBeOfType<DocumentSoftBreak>();
        inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("bar");
    }

    /// <summary>Verifies both hard-break marker forms remain semantic line breaks when their
    /// physical line boundary occurs inside an inline container.</summary>
    [Theory]
    [InlineData("*foo  \nbar*")]
    [InlineData("*foo\\\nbar*")]
    public void Read_WhenEmphasisSpansHardBreak_PreservesHardBreakInsideContainer(string source)
    {
        // Arrange and act
        var emphasis = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentEmphasis>();

        // Assert
        emphasis.Inlines.Count.ShouldBe(3);
        emphasis.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("foo");
        _ = emphasis.Inlines[1].ShouldBeOfType<DocumentLineBreak>();
        emphasis.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("bar");
    }

    /// <summary>Verifies a multiline code span normalizes its line ending to one literal space
    /// rather than exposing a document break inside code.</summary>
    [Fact]
    public void Read_WhenCodeSpanSpansSoftBreak_CreatesOneNormalizedCodeSpan()
    {
        // Arrange and act
        var code = new MarkdownDocumentReader().Read("`foo\nbar`").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentCodeSpan>();

        // Assert
        code.Text.ShouldBe("foo bar");
    }

    /// <summary>Verifies a link label may span a soft line ending and retains that break inside
    /// the link's own semantic inline collection.</summary>
    [Fact]
    public void Read_WhenLinkLabelSpansSoftBreak_CreatesOneLink()
    {
        // Arrange and act
        var link = new MarkdownDocumentReader().Read("[foo\nbar](https://example.invalid)")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentLink>();

        // Assert
        link.Target.ShouldBe("https://example.invalid");
        link.Inlines.Count.ShouldBe(3);
        link.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("foo");
        _ = link.Inlines[1].ShouldBeOfType<DocumentSoftBreak>();
        link.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("bar");
    }

    /// <summary>Verifies escaping an opener before a soft line ending keeps both delimiter
    /// characters literal instead of allowing the later closer to create emphasis.</summary>
    [Fact]
    public void Read_WhenMultilineEmphasisOpenerIsEscaped_PreservesLiteralDelimiters()
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read("\\*foo\nbar*").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.Count.ShouldBe(3);
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("*foo");
        _ = paragraph.Inlines[1].ShouldBeOfType<DocumentSoftBreak>();
        paragraph.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("bar*");
    }

    /// <summary>Verifies both baseline hard-break forms remove their source markers.</summary>
    [Theory]
    [InlineData("alpha  \nbeta")]
    [InlineData("alpha\\\nbeta")]
    public void Read_WhenLineEndsInHardBreakMarker_RemovesMarkerAndCreatesHardBreak(string source)
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read(source)
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.Count.ShouldBe(3);
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("alpha");
        _ = paragraph.Inlines[1].ShouldBeOfType<DocumentLineBreak>();
        paragraph.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("beta");
    }

    /// <summary>Verifies spaces and tabs at the outer boundaries of paragraph raw content are
    /// removed before inline parsing.</summary>
    [Theory]
    [InlineData("   paragraph   ")]
    [InlineData("paragraph\t")]
    [InlineData(" paragraph \t ")]
    public void Read_WhenParagraphHasBoundaryWhitespace_RemovesBoundaryWhitespace(string source)
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("paragraph");
    }

    /// <summary>Verifies spaces and tabs adjacent to a soft line break are structural rather than
    /// visible or selectable paragraph content.</summary>
    [Fact]
    public void Read_WhenSoftBreakHasAdjacentWhitespace_RemovesBoundaryWhitespace()
    {
        // Arrange
        const string source = "  alpha \t\n \t beta\t ";

        // Act
        var paragraph = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.Count.ShouldBe(3);
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("alpha");
        _ = paragraph.Inlines[1].ShouldBeOfType<DocumentSoftBreak>();
        paragraph.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("beta");
    }

    /// <summary>Verifies trimming keeps both hard-break marker forms on interior line boundaries,
    /// removes indentation from the following line, and strips whitespace from the paragraph end.</summary>
    [Theory]
    [InlineData(" alpha  \n \tbeta  ")]
    [InlineData(" alpha\\\n \tbeta\t")]
    public void Read_WhenHardBreakHasParagraphBoundaryWhitespace_PreservesBreakOnly(string source)
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.Count.ShouldBe(3);
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("alpha");
        _ = paragraph.Inlines[1].ShouldBeOfType<DocumentLineBreak>();
        paragraph.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("beta");
    }

    /// <summary>Verifies the shared paragraph normalization also applies inside list items and
    /// block quotes rather than only at the document root.</summary>
    [Fact]
    public void Read_WhenNestedParagraphsHaveTrailingWhitespace_RemovesBoundaryWhitespace()
    {
        // Arrange and act
        var listParagraph = new MarkdownDocumentReader().Read("- item \t").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem().Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();
        var quoteParagraph = new MarkdownDocumentReader().Read("> quote \t").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentBlockQuote>().Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        listParagraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("item");
        quoteParagraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("quote");
    }

    /// <summary>Verifies table recognition never truncates a wider authored header or data row.</summary>
    [Fact]
    public void Read_WhenTableHeaderAndDelimiterWidthsDiffer_DoesNotDiscardCells()
    {
        // Arrange
        const string source = "| A | B |\n| --- |\n| x | y |";
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.Tables });

        // Act
        var result = reader.Read(source);

        // Assert
        result.Blocks.ShouldNotContain(static block => block is DocumentTable);
        VisibleText(result).ShouldContain("B");
        VisibleText(result).ShouldContain("y");
    }

    /// <summary>Verifies escaped and code-span pipes remain inside their table cells.</summary>
    [Fact]
    public void Read_WhenTableCellsContainProtectedPipes_PreservesTwoColumns()
    {
        // Arrange
        const string source = "| Left | Right |\n| --- | --- |\n| a \\| b | `c|d` |";
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.Tables });

        // Act
        var body = reader.Read(source).Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentTable>().Rows[1];

        // Assert
        body.Cells.Count.ShouldBe(2);
        body.Cells[0].Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("a | b");
        body.Cells[1].Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentCodeSpan>().Text.ShouldBe("c|d");
    }

    /// <summary>Verifies a GFM table data row may omit pipes and receives empty cells for
    /// missing trailing columns.</summary>
    [Fact]
    public void Read_WhenTableBodyRowOmitsPipes_PreservesRowAndMissingCell()
    {
        // Arrange
        const string source = "| A | B |\n| --- | --- |\nvalue";
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.Tables });

        // Act
        var table = reader.Read(source).Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentTable>();

        // Assert
        table.Rows.Count.ShouldBe(2);
        table.Rows[1].Cells[0].Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("value");
        table.Rows[1].Cells[1].Inlines.ShouldBeEmpty();
    }

    /// <summary>Verifies every implemented block family ends a GFM table even when its source
    /// line contains a pipe that could otherwise resemble a data row.</summary>
    [Theory]
    [InlineData("# heading | text", "heading")]
    [InlineData("> quote | text", "quote")]
    [InlineData("- item | text", "list")]
    [InlineData("```text | metadata\ncode\n```", "code")]
    public void Read_WhenBlockStartsAfterTable_DoesNotConsumeBlockAsDataRow(string tail, string blockKind)
    {
        // Arrange
        var source = $"| A | B |\n| --- | --- |\n{tail}";
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.Tables });

        // Act
        var result = reader.Read(source);

        // Assert
        var table = result.Blocks[0].ShouldBeOfType<DocumentTable>();
        table.Rows.Count.ShouldBe(1);

        _ = blockKind switch
        {
            "heading" => (DocumentBlock) result.Blocks[1].ShouldBeOfType<DocumentHeading>(),
            "quote" => result.Blocks[1].ShouldBeOfType<DocumentBlockQuote>(),
            "list" => result.Blocks[1].ShouldBeOfType<DocumentList>(),
            "code" => result.Blocks[1].ShouldBeOfType<DocumentCodeBlock>(),
            _ => throw new InvalidOperationException($"Unexpected block kind '{blockKind}'.")
        };
    }

    /// <summary>Verifies nested list indentation creates item-owned structure instead of root siblings.</summary>
    [Fact]
    public void Read_WhenListContainsNestedList_PreservesItemOwnedStructure()
    {
        // Arrange
        const string source = "- parent\n  - child\n- sibling";

        // Act
        var list = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentList>();

        // Assert
        list.Items.Count.ShouldBe(2);
        list.Items[0].Blocks.Count.ShouldBe(2);
        _ = list.Items[0].Blocks[1].ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem();
    }

    /// <summary>Verifies balanced parentheses remain part of an inline-link destination.</summary>
    [Fact]
    public void Read_WhenLinkTargetContainsBalancedParentheses_PreservesCompleteTarget()
    {
        // Arrange and act
        var link = new MarkdownDocumentReader().Read("[docs](https://example.invalid/a_(b))")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentLink>();

        // Assert
        link.Target.ShouldBe("https://example.invalid/a_(b)");
    }

    /// <summary>Verifies angle destinations remove their delimiters, preserve allowed spaces,
    /// and apply punctuation escapes.</summary>
    [Theory]
    [InlineData("[link](<a b>)", "a b")]
    [InlineData("[link](</a\\>b>)", "/a>b")]
    [InlineData("[link](<>)", null)]
    public void Read_WhenLinkUsesAngleDestination_ProducesDecodedTarget(string source, string? expected)
    {
        // Arrange and act
        var link = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentLink>();

        // Assert
        link.Target.ShouldBe(expected);
    }

    /// <summary>Verifies every CommonMark title delimiter is parsed separately from the terminal
    /// hyperlink target.</summary>
    [Theory]
    [InlineData("[link](/uri \"title\")")]
    [InlineData("[link](/uri 'title')")]
    [InlineData("[link](/uri (title))")]
    public void Read_WhenLinkHasTitle_PreservesOnlyDestinationAsTarget(string source)
    {
        // Arrange and act
        var link = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentLink>();

        // Assert
        link.Target.ShouldBe("/uri");
    }

    /// <summary>Verifies whitespace may surround a destination and escaped parentheses remain
    /// literal target characters.</summary>
    [Theory]
    [InlineData("[link]( /uri )", "/uri")]
    [InlineData("[link](/a\\(b\\))", "/a(b)")]
    public void Read_WhenLinkDestinationUsesAllowedSpacingOrEscapes_ProducesDecodedTarget(
        string source,
        string expected)
    {
        // Arrange and act
        var link = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentLink>();

        // Assert
        link.Target.ShouldBe(expected);
    }

    /// <summary>Verifies malformed destination and title grammar remains literal text instead of
    /// creating a corrupted terminal hyperlink target.</summary>
    [Theory]
    [InlineData("[link](/my uri)")]
    [InlineData("[link](/uri\u0001tail)")]
    [InlineData("[link](<a<b>)")]
    [InlineData("[link](<a>b>)")]
    [InlineData("[link](<bar>(title))")]
    [InlineData("[link](/uri \"ti\"tle\")")]
    public void Read_WhenLinkDestinationOrTitleIsInvalid_PreservesLiteralSource(string source)
    {
        // Arrange and act
        var result = new MarkdownDocumentReader().Read(source);
        var paragraph = result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldNotContain(static inline => inline is DocumentLink);
        VisibleText(result).ShouldBe(source);
    }

    /// <summary>Verifies a link label containing its own literal, balanced <c>[...]</c> - a
    /// "citation-style" reference marker - still resolves to one link over the complete label,
    /// rather than failing at the first, inner "]".</summary>
    [Fact]
    public void Read_WhenLinkLabelContainsALiteralBracketPair_ResolvesOverTheCompleteLabel()
    {
        // Arrange and act
        var link = new MarkdownDocumentReader().Read("[See [1]](http://example.invalid)")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentLink>();

        // Assert
        link.Target.ShouldBe("http://example.invalid");
        link.Text.ShouldBe("See [1]");
    }

    /// <summary>Verifies the common "linked image" markup shape - a nested "[x](y)"-looking
    /// sequence inside an outer link's own label, the pattern Markdown authors use to link an
    /// image - resolves the outer boundary correctly instead of mistaking the nested sequence's
    /// own "](" for the outer link's separator, and that the nested sequence itself degrades to
    /// literal text rather than becoming a second, nested link: this reader has no separate image
    /// node, and CommonMark forbids a link from containing another link at any nesting depth.</summary>
    [Fact]
    public void Read_WhenLinkLabelContainsALinkShapedNestedSequence_ResolvesTheOuterLinkAndFlattensTheNestedOne()
    {
        // Arrange and act
        var link = new MarkdownDocumentReader().Read("[![alt](img.png)](http://example.invalid)")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentLink>();

        // Assert
        link.Target.ShouldBe("http://example.invalid");
        link.Text.ShouldBe("![alt](img.png)");
    }

    /// <summary>Verifies the "no nested link" rule propagates through an intermediate inline
    /// container - not just when the link-shaped sequence sits directly in the label - since a
    /// link's own label is free to contain emphasis or strong text that itself wraps further
    /// content.</summary>
    [Fact]
    public void Read_WhenLinkLabelContainsALinkShapedSequenceInsideStrong_FlattensTheNestedOne()
    {
        // Arrange and act
        var link = new MarkdownDocumentReader().Read("[**[inner](x)**](http://example.invalid)")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentLink>();

        // Assert
        link.Target.ShouldBe("http://example.invalid");
        var strong = link.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentStrong>();
        strong.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("[inner](x)");
    }

    /// <summary>Verifies trailing hashes close a heading only when whitespace separates them.</summary>
    [Theory]
    [InlineData("# value###", "value###")]
    [InlineData("# value ###", "value")]
    [InlineData("#", "")]
    public void Read_WhenAtxHeadingUsesClosingHashes_PreservesOnlyAuthoredContent(string source, string expected)
    {
        // Arrange and act
        var heading = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentHeading>();

        // Assert
        heading.Inlines.Count.ShouldBe(expected.Length == 0 ? 0 : 1);

        if (expected.Length > 0)
        {
            heading.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe(expected);
        }
    }

    /// <summary>Verifies every space or tab at the raw ATX content boundaries is structural while
    /// whitespace inside the heading remains authored inline content.</summary>
    [Theory]
    [InlineData("#                  foo", "foo")]
    [InlineData("#\t\tfoo\t\t", "foo")]
    [InlineData("## \tfoo  bar\t ## \t", "foo  bar")]
    [InlineData("### \t ", "")]
    public void Read_WhenAtxHeadingHasBoundaryWhitespace_RemovesOnlyBoundaryWhitespace(
        string source,
        string expected)
    {
        // Arrange and act
        var heading = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentHeading>();

        // Assert
        heading.Inlines.Count.ShouldBe(expected.Length == 0 ? 0 : 1);

        if (expected.Length > 0)
        {
            heading.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe(expected);
        }
    }

    /// <summary>Verifies quote nesting has a deterministic structural ceiling below hostile stack depth.</summary>
    [Fact]
    public void Read_WhenBlockQuoteNestingIsHostile_RemainsBoundedAndDeterministic()
    {
        // Arrange
        var source = string.Concat(Enumerable.Repeat("> ", 512)) + "body";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        var depth = 0;
        var block = result.Blocks.ShouldHaveSingleItem();

        while (block is DocumentBlockQuote quote)
        {
            depth++;
            block = quote.Blocks.ShouldHaveSingleItem();
        }

        depth.ShouldBeLessThanOrEqualTo(64);
        _ = block.ShouldBeOfType<DocumentParagraph>();
    }

    /// <summary>Verifies newline spellings normalize to an identical semantic tree.</summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\r\n")]
    public void Read_WhenNewlineEncodingVaries_ProducesEquivalentBlocks(string newline)
    {
        // Arrange and act
        var result = new MarkdownDocumentReader().Read($"first{newline}second");

        // Assert
        var paragraph = result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();
        paragraph.Inlines.Count.ShouldBe(3);
        _ = paragraph.Inlines[1].ShouldBeOfType<DocumentSoftBreak>();
    }

    /// <summary>Verifies Setext headings and zero-based ordered markers remain valid baseline Markdown.</summary>
    [Fact]
    public void Read_WhenSetextHeadingAndZeroOrdinalAreUsed_ProducesSemanticBlocks()
    {
        // Arrange
        const string source = "Title\n---\n\n0. Zero";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks[0].ShouldBeOfType<DocumentHeading>().Level.ShouldBe(2);
        result.Blocks[1].ShouldBeOfType<DocumentList>().Start.ShouldBe(0);
    }

    /// <summary>Verifies a Setext underline converts every accumulated paragraph line into one
    /// heading with the original soft line boundary.</summary>
    [Theory]
    [InlineData("Foo\nbar\n===", 1)]
    [InlineData("Foo\nbar\n---", 2)]
    public void Read_WhenSetextHeadingSpansMultipleLines_ProducesOneHeading(string source, int expectedLevel)
    {
        // Arrange and act
        var heading = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentHeading>();

        // Assert
        heading.Level.ShouldBe(expectedLevel);
        heading.Inlines.Count.ShouldBe(3);
        heading.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("Foo");
        _ = heading.Inlines[1].ShouldBeOfType<DocumentSoftBreak>();
        heading.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("bar");
    }

    /// <summary>Verifies a blank line closes the paragraph before a later underline, leaving that
    /// underline to retain its ordinary thematic-break meaning.</summary>
    [Fact]
    public void Read_WhenBlankLinePrecedesSetextUnderline_DoesNotConvertEarlierParagraph()
    {
        // Arrange and act
        var result = new MarkdownDocumentReader().Read("Foo\nbar\n\n---");

        // Assert
        result.Blocks.Count.ShouldBe(2);
        _ = result.Blocks[0].ShouldBeOfType<DocumentParagraph>();
        _ = result.Blocks[1].ShouldBeOfType<DocumentSeparator>();
    }

    /// <summary>Verifies four-space-indented hashes are not mistaken for ATX headings.</summary>
    [Fact]
    public void Read_WhenAtxMarkerHasFourSpaceIndent_PreservesParagraphText()
    {
        // Arrange and act
        var block = new MarkdownDocumentReader().Read("    # literal").Blocks.ShouldHaveSingleItem();

        // Assert
        block.ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("    # literal");
    }

    /// <summary>Verifies a large unmatched opener run remains a bounded literal parse.</summary>
    [Fact]
    public void Read_WhenManyInlineOpenersAreUnmatched_CompletesAsLiteralText()
    {
        // Arrange
        var source = new string('[', 32_768);

        // Act
        var paragraph = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe(source);
    }

    /// <summary>Verifies extended autolinks trim surrounding punctuation but retain balanced URL punctuation.</summary>
    [Fact]
    public void Read_WhenExtendedAutolinkContainsBalancedParentheses_PreservesBalancedTarget()
    {
        // Arrange
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.Autolinks });

        // Act
        var paragraph = reader.Read("(https://example.invalid/a(b)).").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.OfType<DocumentLink>().ShouldHaveSingleItem().Target
            .ShouldBe("https://example.invalid/a(b)");
    }

    /// <summary>Verifies continuation lines remain inside the list item that introduced them.</summary>
    [Fact]
    public void Read_WhenListItemHasContinuationLine_PreservesOneItemParagraph()
    {
        // Arrange and act
        var item = new MarkdownDocumentReader().Read("- first\n  continued").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem();

        // Assert
        var paragraph = item.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();
        paragraph.Inlines.Count.ShouldBe(3);
        _ = paragraph.Inlines[1].ShouldBeOfType<DocumentSoftBreak>();
        paragraph.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("continued");
    }

    /// <summary>Verifies a numbered-list continuation line strips exactly its own marker's width -
    /// digit count plus the punctuation and required space - rather than a bullet's fixed
    /// two-column width, for every digit count from one through three.</summary>
    [Theory]
    [InlineData("1. first\n   continued", "continued")]
    [InlineData("10. first\n    continued", "continued")]
    [InlineData("100. first\n     continued", "continued")]
    public void Read_WhenNumberedListItemHasContinuationLine_StripsExactlyItsOwnMarkerWidth(
        string source,
        string expected)
    {
        // Arrange and act
        var item = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem();

        // Assert
        var paragraph = item.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();
        paragraph.Inlines.Count.ShouldBe(3);
        _ = paragraph.Inlines[1].ShouldBeOfType<DocumentSoftBreak>();
        paragraph.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe(expected);
    }

    /// <summary>Verifies one through four spaces after a bullet marker establish structural
    /// content indentation instead of becoming visible text.</summary>
    [Theory]
    [InlineData("- first\n  continued")]
    [InlineData("-  first\n   continued")]
    [InlineData("-   first\n    continued")]
    [InlineData("-    first\n     continued")]
    public void Read_WhenBulletMarkerUsesOneThroughFourSpaces_StripsStructuralIndentation(string source)
    {
        // Arrange and act
        var item = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem();

        // Assert
        var paragraph = item.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();
        paragraph.Inlines.Count.ShouldBe(3);
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("first");
        _ = paragraph.Inlines[1].ShouldBeOfType<DocumentSoftBreak>();
        paragraph.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("continued");
    }

    /// <summary>Verifies ordered marker width includes both its digit run and all one-through-four
    /// structural spaces.</summary>
    [Theory]
    [InlineData("1.  first\n    continued")]
    [InlineData("10)   first\n      continued")]
    [InlineData("100.    first\n        continued")]
    public void Read_WhenOrderedMarkerUsesMultipleSpaces_StripsCompleteStructuralIndentation(string source)
    {
        // Arrange and act
        var item = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem();

        // Assert
        var paragraph = item.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();
        paragraph.Inlines.Count.ShouldBe(3);
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("first");
        _ = paragraph.Inlines[1].ShouldBeOfType<DocumentSoftBreak>();
        paragraph.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("continued");
    }

    /// <summary>Verifies each peer item uses its own marker spacing when removing continuation
    /// indentation.</summary>
    [Fact]
    public void Read_WhenPeerItemsUseDifferentMarkerSpacing_StripsEachItemsOwnIndentation()
    {
        // Arrange
        const string source = "- first\n-   second\n    continued";

        // Act
        var list = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentList>();

        // Assert
        list.Items.Count.ShouldBe(2);
        var paragraph = list.Items[1].Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();
        paragraph.Inlines.Count.ShouldBe(3);
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("second");
        _ = paragraph.Inlines[1].ShouldBeOfType<DocumentSoftBreak>();
        paragraph.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("continued");
    }

    /// <summary>Verifies marker spacing establishes the indentation boundary for a nested list
    /// without leaking spaces into the parent text.</summary>
    [Fact]
    public void Read_WhenSpacedMarkerContainsNestedList_PreservesParentAndNestedBlocks()
    {
        // Arrange
        const string source = "-   parent\n    - child";

        // Act
        var item = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem();

        // Assert
        item.Blocks.Count.ShouldBe(2);
        item.Blocks[0].ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("parent");
        item.Blocks[1].ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem().Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("child");
    }

    /// <summary>Verifies changing the bullet character starts a distinct list for every bullet
    /// marker type.</summary>
    [Fact]
    public void Read_WhenBulletCharacterChanges_ProducesSeparateLists()
    {
        // Arrange
        const string source = "- first\n+ second\n* third";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks.Count.ShouldBe(3);
        result.Blocks.ShouldAllBe(static block => block is DocumentList);
        result.Blocks.Cast<DocumentList>().ShouldAllBe(static list => list.Items.Count == 1);
    }

    /// <summary>Verifies changing between period and parenthesis ordered delimiters starts a
    /// distinct list and preserves each new list's authored start.</summary>
    [Fact]
    public void Read_WhenOrderedDelimiterChanges_ProducesSeparateLists()
    {
        // Arrange
        const string source = "1. first\n2) second\n3. third";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks.Count.ShouldBe(3);
        result.Blocks[0].ShouldBeOfType<DocumentList>().Start.ShouldBe(1);
        result.Blocks[1].ShouldBeOfType<DocumentList>().Start.ShouldBe(2);
        result.Blocks[2].ShouldBeOfType<DocumentList>().Start.ShouldBe(3);
        result.Blocks.Cast<DocumentList>().ShouldAllBe(static list => list.Items.Count == 1);
    }

    /// <summary>Verifies radio items separated by a bullet-character change receive independent
    /// generated groups and cannot deselect each other during parsing.</summary>
    [Fact]
    public void Read_WhenRadioListBulletChanges_KeepsGeneratedGroupsIndependent()
    {
        // Arrange
        var reader = new MarkdownDocumentReader(new MarkdownOptions
        {
            Extensions = MarkdownExtension.RadioLists
        });

        // Act
        var result = reader.Read("- (x) First\n+ (x) Second");

        // Assert
        result.Blocks.Count.ShouldBe(2);
        var first = result.Blocks[0].ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem()
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentBlockControl>().Control
            .ShouldBeOfType<RadioButton>();
        var second = result.Blocks[1].ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem()
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentBlockControl>().Control
            .ShouldBeOfType<RadioButton>();
        first.GroupName.ShouldNotBe(second.GroupName);
        first.IsChecked.ShouldBeTrue();
        second.IsChecked.ShouldBeTrue();
    }

    /// <summary>Verifies a blank line between peer items makes one list loose instead of splitting
    /// it into unrelated list blocks.</summary>
    [Fact]
    public void Read_WhenPeerListItemsAreBlankSeparated_ProducesOneLooseList()
    {
        // Arrange and act
        var list = new MarkdownDocumentReader().Read("- one\n\n- two").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentList>();

        // Assert
        list.IsLoose.ShouldBeTrue();
        list.Items.Count.ShouldBe(2);
    }

    private static string VisibleText(DocumentReadResult result)
    {
        var text = new StringBuilder();

        foreach (var block in result.Blocks)
        {
            if (block is DocumentParagraph paragraph)
            {
                foreach (var inline in paragraph.Inlines.OfType<DocumentTextRun>())
                {
                    _ = TextMarkup.Parse(inline.Text.AsSpan(), out var display);
                    _ = text.Append(display);
                }
            }
        }

        return text.ToString();
    }
}
