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

    /// <summary>Verifies an over-indented marker neither interrupts a paragraph (at the document
    /// root) nor remains inside a preceding block quote once a blank line has already closed that
    /// quote's lazy-continuation eligibility.</summary>
    [Fact]
    public void Read_WhenOverIndentedBlockQuoteMarkerFollowsContent_PreservesLiteralParagraphContent()
    {
        // Arrange
        const string source = "paragraph\n    > literal\n\n> quoted\n\n    > sibling";

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

    /// <summary>Verifies CommonMark lazy continuation: a wrapped line that carries no '&gt;' marker
    /// still belongs to the block quote's open paragraph.</summary>
    [Fact]
    public void Read_WhenBlockQuoteParagraphWrapsWithoutMarker_ContinuesInsideQuote()
    {
        // Arrange
        const string source = "> wrapped\ncontinued line";

        // Act
        var quote = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentBlockQuote>();

        // Assert
        var paragraph = quote.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();
        paragraph.Inlines.Count.ShouldBe(3);
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("wrapped");
        _ = paragraph.Inlines[1].ShouldBeOfType<DocumentSoftBreak>();
        paragraph.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("continued line");
    }

    /// <summary>Verifies a blank line always closes lazy-continuation eligibility, so text following
    /// it becomes a separate block instead of joining the quote's paragraph.</summary>
    [Fact]
    public void Read_WhenBlankLineFollowsBlockQuoteParagraph_EndsLazyContinuationEligibility()
    {
        // Arrange
        const string source = "> quoted\n\nafter";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks.Count.ShouldBe(2);
        result.Blocks[0].ShouldBeOfType<DocumentBlockQuote>().Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("quoted");
        result.Blocks[1].ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("after");
    }

    /// <summary>Verifies a marker-less line that itself looks like another block's start still
    /// interrupts the quote's open paragraph instead of being absorbed as a lazy continuation.
    /// </summary>
    [Fact]
    public void Read_WhenMarkerLessLineLooksLikeHeading_InterruptsBlockQuoteParagraph()
    {
        // Arrange
        const string source = "> quoted\n# Heading";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks.Count.ShouldBe(2);
        result.Blocks[0].ShouldBeOfType<DocumentBlockQuote>().Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("quoted");
        result.Blocks[1].ShouldBeOfType<DocumentHeading>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("Heading");
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
        tasks.Items[0].Blocks[0].ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentInlineControl>().Control
            .ShouldBeOfType<CheckBox>().IsChecked.ShouldBe(true);

        var first = tasks.Items[2].Blocks[0].ShouldBeOfType<DocumentBlockControl>().Control
            .ShouldBeOfType<RadioButton>();
        var second = tasks.Items[3].Blocks[0].ShouldBeOfType<DocumentBlockControl>().Control
            .ShouldBeOfType<RadioButton>();
        first.GroupName.ShouldBe(second.GroupName);
        second.IsChecked.ShouldBeTrue();

        var callout = result.Blocks[2].ShouldBeOfType<DocumentCallout>();
        callout.Kind.ShouldBe("NOTE");
        callout.Title.ShouldBe("Read this");
        callout.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("Callout body.");
    }

    /// <summary>Verifies GFM ASCII whitespace is accepted both inside an unchecked task marker
    /// and between the marker and its label.</summary>
    [Theory]
    [InlineData("- [ ] task", false)]
    [InlineData("- [x]\ttask", true)]
    [InlineData("- [X] \t task", true)]
    [InlineData("- [\t]\t\ttask", false)]
    [InlineData("- [\v]\ftask", false)]
    public void Read_WhenTaskMarkerUsesGfmWhitespace_CreatesCheckBox(string source, bool expectedChecked)
    {
        // Arrange
        var reader = new MarkdownDocumentReader(new MarkdownOptions
        {
            Extensions = MarkdownExtension.TaskLists
        });

        // Act
        var paragraph = reader.Read(source).Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentList>()
            .Items.ShouldHaveSingleItem().Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        var checkBox = paragraph.Inlines[0].ShouldBeOfType<DocumentInlineControl>().Control
            .ShouldBeOfType<CheckBox>();
        checkBox.IsChecked.ShouldBe(expectedChecked);
        checkBox.Text.ShouldBeEmpty();
        paragraph.Inlines[^1].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("task");
    }

    /// <summary>Verifies a task marker adds an interactive checkbox without bypassing Markdown
    /// parsing for the item's authored inline label.</summary>
    [Fact]
    public void Read_WhenTaskLabelContainsInlineMarkdown_PreservesItsSemanticInlines()
    {
        // Arrange
        var reader = new MarkdownDocumentReader(new MarkdownOptions
        {
            Extensions = MarkdownExtension.TaskLists
        });

        // Act
        var paragraph = reader.Read("- [ ] **bold** and [link](target)").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem().Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines[0].ShouldBeOfType<DocumentInlineControl>().Control.ShouldBeOfType<CheckBox>()
            .Text.ShouldBeEmpty();
        paragraph.Inlines.ShouldContain(static inline => inline is DocumentStrong);
        paragraph.Inlines.OfType<DocumentLink>().ShouldContain(static link => link.Target == "target");
    }

    /// <summary>Verifies only ASCII spaces and tabs make a CommonMark blank line; broader Unicode
    /// whitespace remains authored paragraph content.</summary>
    [Theory]
    [InlineData("\u00a0")]
    [InlineData("\u000c")]
    [InlineData(" \u00a0\t")]
    public void Read_WhenLineContainsUnicodeWhitespace_PreservesItInTheParagraph(string authoredWhitespace)
    {
        // Arrange
        var source = $"before\n{authoredWhitespace}\nafter";

        // Act
        var paragraph = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.OfType<DocumentTextRun>().Select(static run => run.Text)
            .ShouldContain(authoredWhitespace.Trim(' ', '\t'));
        paragraph.Inlines.OfType<DocumentSoftBreak>().Count().ShouldBe(2);
    }

    /// <summary>Verifies CommonMark's insecure-character normalization replaces every literal NUL
    /// before semantic parsing.</summary>
    [Fact]
    public void Read_WhenSourceContainsNul_ReplacesItWithTheUnicodeReplacementCharacter()
    {
        // Arrange
        const string source = "before\0after";

        // Act
        var run = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>();

        // Assert
        run.Text.ShouldBe("before\ufffdafter");
    }

    /// <summary>Verifies missing separators and non-ASCII whitespace do not become task markers.</summary>
    [Theory]
    [InlineData("- [x]task")]
    [InlineData("- [\u00a0] task")]
    [InlineData("- [x]\u00a0task")]
    public void Read_WhenTaskMarkerDoesNotUseGfmWhitespace_PreservesParagraph(string source)
    {
        // Arrange
        var reader = new MarkdownDocumentReader(new MarkdownOptions
        {
            Extensions = MarkdownExtension.TaskLists
        });

        // Act
        var block = reader.Read(source).Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentList>()
            .Items.ShouldHaveSingleItem().Blocks.ShouldHaveSingleItem();

        // Assert
        _ = block.ShouldBeOfType<DocumentParagraph>();
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
            | :- | --: | :---: |
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

    /// <summary>Verifies every positive delimiter-hyphen count accepted by GFM creates a table,
    /// including the compact one- and two-hyphen forms.</summary>
    [Theory]
    [InlineData("-")]
    [InlineData("--")]
    [InlineData("---")]
    public void Read_WhenTableDelimiterHasPositiveHyphenCount_ProducesTable(string delimiter)
    {
        // Arrange
        var source = FormattableString.Invariant($"A | B\n{delimiter} | {delimiter}\nx | y");
        var reader = new MarkdownDocumentReader(new MarkdownOptions
        {
            Extensions = MarkdownExtension.Tables
        });

        // Act
        var table = reader.Read(source).Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentTable>();

        // Assert
        table.Rows.Count.ShouldBe(2);
        table.Rows[0].IsHeader.ShouldBeTrue();
        table.Rows[0].Cells.Count.ShouldBe(2);
        table.Rows[1].Cells.Count.ShouldBe(2);
    }

    /// <summary>Verifies a table delimiter permits only one alignment colon on either edge and
    /// still requires at least one hyphen.</summary>
    [Theory]
    [InlineData(":")]
    [InlineData("::")]
    [InlineData("::-")]
    [InlineData("-::")]
    [InlineData(":-::")]
    public void Read_WhenTableDelimiterHasNoHyphenOrRepeatedColons_LeavesLiteral(string delimiter)
    {
        // Arrange
        var source = FormattableString.Invariant($"A\n{delimiter}\nx");
        var reader = new MarkdownDocumentReader(new MarkdownOptions
        {
            Extensions = MarkdownExtension.Tables
        });

        // Act
        var result = reader.Read(source);

        // Assert
        result.Blocks.ShouldNotContain(static block => block is DocumentTable);
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

    /// <summary>Verifies fenced code preserves its literal body-line sequence even when one or
    /// every body line is empty.</summary>
    [Theory]
    [InlineData("```\n```", "")]
    [InlineData("```\n\nvalue\n```", "\nvalue")]
    [InlineData("```\n\n\nvalue\n```", "\n\nvalue")]
    [InlineData("~~~\n\n\n~~~", "\n")]
    public void Read_WhenFenceBodyContainsEmptyLines_PreservesEveryBodyLine(string source, string expected)
    {
        // Arrange and act
        var code = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentCodeBlock>();

        // Assert
        code.Text.ShouldBe(expected);
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

    /// <summary>Verifies a named HTML entity in ordinary paragraph text decodes to its character,
    /// the common case behind reports of literal <c>&amp;amp;</c> surviving into rendered text.</summary>
    [Fact]
    public void Read_WhenParagraphContainsNamedEntity_DecodesToCharacter()
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read("Fish &amp; Chips")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text
            .ShouldBe("Fish & Chips");
    }

    /// <summary>Verifies both decimal and hexadecimal numeric character references decode to the
    /// matching Unicode character.</summary>
    [Theory]
    [InlineData("&#65;")]
    [InlineData("&#x41;")]
    [InlineData("&#X41;")]
    public void Read_WhenParagraphContainsNumericEntity_DecodesToCharacter(string source)
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read(source)
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("A");
    }

    /// <summary>Verifies a numeric reference outside the valid Unicode scalar-value range - past
    /// U+10FFFF or inside the surrogate range - decodes to the replacement character rather than
    /// being rejected outright, matching the CommonMark/HTML5 numeric-reference rule.</summary>
    [Theory]
    [InlineData("&#x110000;")]
    [InlineData("&#xD800;")]
    public void Read_WhenNumericEntityIsOutOfRange_DecodesToReplacementCharacter(string source)
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read(source)
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("�");
    }

    /// <summary>Verifies an unrecognized named reference is left completely literal, including its
    /// ampersand and semicolon, rather than guessed or partially decoded.</summary>
    [Fact]
    public void Read_WhenNamedEntityIsUnknown_PreservesLiteralText()
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read("&foobar;")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("&foobar;");
    }

    /// <summary>Verifies a numeric reference that decodes to an emphasis marker does not itself
    /// trigger emphasis parsing - the decoded character is appended directly to the plain-text run
    /// and never revisited by the structural inline checks.</summary>
    [Fact]
    public void Read_WhenNumericEntityDecodesToAsterisk_DoesNotTriggerEmphasis()
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read("a&#42;b&#42;c")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("a*b*c");
    }

    /// <summary>Verifies an entity reference inside a code span is preserved completely literally;
    /// code spans are extracted before the plain-text/entity path ever runs.</summary>
    [Fact]
    public void Read_WhenEntityAppearsInsideCodeSpan_PreservesLiteralText()
    {
        // Arrange and act
        var code = new MarkdownDocumentReader().Read("`&amp;`")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentCodeSpan>();

        // Assert
        code.Text.ShouldBe("&amp;");
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

    /// <summary>Verifies a tab preceding a bullet marker is measured as CommonMark indentation - a
    /// tab expands to the next 4-column stop, clearing the three-column marker threshold on its
    /// own - so the line remains literal paragraph text instead of becoming a list.</summary>
    [Fact]
    public void Read_WhenListMarkerIsPrecededByTab_RemainsLiteralParagraphText()
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read("\t- item").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("\t- item");
    }

    /// <summary>Verifies a tab preceding a block quote marker is measured as CommonMark indentation,
    /// so the line remains literal paragraph text instead of becoming a block quote.</summary>
    [Fact]
    public void Read_WhenBlockQuoteMarkerIsPrecededByTab_RemainsLiteralParagraphText()
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read("\t> quoted").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("\t> quoted");
    }

    /// <summary>Verifies a tab preceding a fence opener is measured as CommonMark indentation, so
    /// the line remains literal paragraph text instead of opening a fenced code block.</summary>
    [Fact]
    public void Read_WhenFenceOpenerIsPrecededByTab_RemainsLiteralParagraphText()
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read("\t```").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        // Assert
        paragraph.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("\t```");
    }

    /// <summary>Verifies a fenced code body line's own leading tab is removed as part of the fence's
    /// structural indentation, the same way a leading space would be, rather than surviving as a
    /// literal character because the indent scan stopped counting at the tab.</summary>
    [Fact]
    public void Read_WhenFencedCodeBodyLineStartsWithTab_StripsStructuralTab()
    {
        // Arrange
        const string source = "  ```\n\tcode\n  ```";

        // Act
        var code = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentCodeBlock>();

        // Assert
        code.Text.ShouldBe("code");
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

    /// <summary>Verifies same-marker nested emphasis resolves via CommonMark's delimiter-stack rules:
    /// a closer always binds to the nearest still-open opener of the same marker, so the inner pair
    /// resolves before its enclosing pair instead of the outer opener capturing the inner closer and
    /// stranding the trailing text as an orphaned literal run.</summary>
    [Theory]
    [InlineData("_foo _bar_ baz_")]
    [InlineData("*foo *bar* baz*")]
    public void Read_WhenSameMarkerEmphasisIsNested_ClosesInnerPairBeforeOuterPair(string source)
    {
        // Arrange and act
        var emphasis = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentEmphasis>();

        // Assert
        emphasis.Inlines.Count.ShouldBe(3);
        emphasis.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("foo ");
        var inner = emphasis.Inlines[1].ShouldBeOfType<DocumentEmphasis>();
        inner.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("bar");
        emphasis.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe(" baz");
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

    /// <summary>Verifies an unpaired high surrogate immediately before an emphasis opener does not
    /// throw while classifying the delimiter run's flanking. Uses <c>[Fact]</c> rather than
    /// <c>[Theory]</c>/<c>[InlineData]</c> because the xUnit v3 toolchain silently repairs unpaired
    /// surrogates in inline data string arguments, which would hide the regression.</summary>
    [Fact]
    public void Read_WhenSourceHasUnpairedHighSurrogateBeforeEmphasisOpener_DoesNotThrow()
    {
        // Arrange
        var source = "\uD800*foo*";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        _ = result.ShouldNotBeNull();
        _ = result.Blocks.ShouldNotBeNull();
    }

    /// <summary>Verifies an unpaired low surrogate immediately before an emphasis opener does not
    /// throw while classifying the delimiter run's flanking. See the sibling high-surrogate fact for
    /// why this uses <c>[Fact]</c> instead of <c>[Theory]</c>.</summary>
    [Fact]
    public void Read_WhenSourceHasUnpairedLowSurrogateBeforeEmphasisOpener_DoesNotThrow()
    {
        // Arrange
        var source = "\uDC00*foo*";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        _ = result.ShouldNotBeNull();
        _ = result.Blocks.ShouldNotBeNull();
    }

    /// <summary>Verifies an unpaired low surrogate immediately before an emphasis closer does not
    /// throw while classifying the delimiter run's flanking. See the sibling high-surrogate fact for
    /// why this uses <c>[Fact]</c> instead of <c>[Theory]</c>.</summary>
    [Fact]
    public void Read_WhenSourceHasUnpairedLowSurrogateBeforeEmphasisCloser_DoesNotThrow()
    {
        // Arrange
        var source = "a*foo\uDC00*bar";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        _ = result.ShouldNotBeNull();
        _ = result.Blocks.ShouldNotBeNull();
    }

    /// <summary>Verifies an unpaired low surrogate immediately after an emphasis opener does not
    /// throw while classifying the delimiter run's flanking. See the sibling high-surrogate fact for
    /// why this uses <c>[Fact]</c> instead of <c>[Theory]</c>.</summary>
    [Fact]
    public void Read_WhenSourceHasUnpairedLowSurrogateAfterEmphasisOpener_DoesNotThrow()
    {
        // Arrange
        var source = "a*\uDC00b*";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        _ = result.ShouldNotBeNull();
        _ = result.Blocks.ShouldNotBeNull();
    }

    /// <summary>Verifies an unpaired high surrogate immediately after an emphasis opener does not
    /// throw while classifying the delimiter run's flanking. See the sibling high-surrogate fact for
    /// why this uses <c>[Fact]</c> instead of <c>[Theory]</c>.</summary>
    [Fact]
    public void Read_WhenSourceHasUnpairedHighSurrogateAfterEmphasisOpener_DoesNotThrow()
    {
        // Arrange
        var source = "a*\uD800b*";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        _ = result.ShouldNotBeNull();
        _ = result.Blocks.ShouldNotBeNull();
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
    [Theory]
    [InlineData("-")]
    [InlineData("--")]
    [InlineData("---")]
    public void Read_WhenTableHeaderAndDelimiterWidthsDiffer_DoesNotDiscardCells(string delimiter)
    {
        // Arrange
        var source = FormattableString.Invariant($"| A | B |\n| {delimiter} |\n| x | y |");
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.Tables });

        // Act
        var result = reader.Read(source);

        // Assert
        result.Blocks.ShouldNotContain(static block => block is DocumentTable);
        VisibleText(result).ShouldContain("B");
        VisibleText(result).ShouldContain("y");
    }

    /// <summary>Verifies GFM pipe escapes protect table cells and are removed before inline parsing,
    /// including inside code spans.</summary>
    [Fact]
    public void Read_WhenTableCellsContainEscapedPipes_PreservesTwoColumnsAndUnescapesContent()
    {
        // Arrange
        const string source = "| Left | Right |\n| --- | --- |\n| a \\| b | `c\\|d` |";
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.Tables });

        // Act
        var body = reader.Read(source).Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentTable>().Rows[1];

        // Assert
        body.Cells.Count.ShouldBe(2);
        body.Cells[0].Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("a | b");
        body.Cells[1].Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentCodeSpan>().Text.ShouldBe("c|d");
    }

    /// <summary>Verifies a raw pipe remains a table delimiter even when backticks surround it.</summary>
    [Fact]
    public void Read_WhenTableCodeSpanContainsRawPipe_SplitsAtRawPipe()
    {
        // Arrange
        const string source = "| A | B |\n| --- | --- |\n| first | `left|right` |";
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.Tables });

        // Act
        var body = reader.Read(source).Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentTable>().Rows[1];

        // Assert
        body.Cells.Count.ShouldBe(2);
        body.Cells[0].Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("first");
        body.Cells[1].Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("`left");
    }

    /// <summary>Verifies an unmatched backtick cannot suppress all later table delimiters.</summary>
    [Fact]
    public void Read_WhenTableCellHasUnmatchedBacktick_PreservesLaterCellBoundaries()
    {
        // Arrange
        const string source = "| A | B | C |\n| --- | --- | --- |\n| `open | middle | end |";
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.Tables });

        // Act
        var body = reader.Read(source).Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentTable>().Rows[1];

        // Assert
        body.Cells[0].Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("`open");
        body.Cells[1].Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("middle");
        body.Cells[2].Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("end");
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

    /// <summary>Verifies a tab-indented nested marker is recognized through the same structural
    /// indentation removal as a space-indented one. CommonMark expands a tab to the next 4-column
    /// stop, which alone clears the parent item's two-column marker width; before that expansion
    /// was measured, the leading tab was never stripped and the nested marker text stayed literal
    /// inside the parent's paragraph instead of starting a nested list.</summary>
    [Fact]
    public void Read_WhenNestedListMarkerIsIndentedWithTab_RecognizesNestedList()
    {
        // Arrange
        const string source = "- item\n\t- nested";

        // Act
        var item = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem();

        // Assert
        item.Blocks.Count.ShouldBe(2);
        item.Blocks[0].ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("item");
        item.Blocks[1].ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem().Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("nested");
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

    /// <summary>Verifies a bare and an angle-bracketed link destination both decode HTML entities,
    /// confirming the reader's internal punctuation-unescaping step now also decodes entities.</summary>
    [Theory]
    [InlineData("[link](/a&amp;b)", "/a&b")]
    [InlineData("[link](</a&amp;b>)", "/a&b")]
    public void Read_WhenLinkDestinationContainsEntity_ProducesDecodedTarget(string source, string expected)
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

    /// <summary>Verifies baseline angle autolinks accept the CommonMark URI-scheme and email
    /// grammars and preserve a mail address as the visible label.</summary>
    [Theory]
    [InlineData("<irc://example.org/channel>", "irc://example.org/channel", "irc://example.org/channel")]
    [InlineData("<git+ssh.2:repository>", "git+ssh.2:repository", "git+ssh.2:repository")]
    [InlineData("<foo@example.com>", "foo@example.com", "mailto:foo@example.com")]
    [InlineData("<first.last+tag@example-domain.test>", "first.last+tag@example-domain.test", "mailto:first.last+tag@example-domain.test")]
    public void Read_WhenAngleAutolinkUsesCommonMarkGrammar_CreatesLink(
        string source,
        string expectedText,
        string expectedTarget)
    {
        // Arrange and act
        var link = new MarkdownDocumentReader().Read(source).Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentLink>();

        // Assert
        link.Text.ShouldBe(expectedText);
        link.Target.ShouldBe(expectedTarget);
    }

    /// <summary>Verifies incomplete schemes, prohibited bytes, and malformed mailboxes remain
    /// literal angle-bracket source.</summary>
    [Theory]
    [InlineData("<http://>")]
    [InlineData("<x:data>")]
    [InlineData("<foo\u0001bar:baz>")]
    [InlineData("<foo bar:baz>")]
    [InlineData("<foo@example-.com>")]
    public void Read_WhenAngleAutolinkGrammarIsInvalid_PreservesLiteralSource(string source)
    {
        // Arrange and act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines
            .ShouldNotContain(static inline => inline is DocumentLink);
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

    /// <summary>Verifies brackets inside a higher-precedence code span do not close a candidate
    /// link label.</summary>
    [Theory]
    [InlineData("[not a `link](/foo`)")]
    [InlineData("[not a ``link](/foo``)")]
    public void Read_WhenCodeSpanContainsLabelBracket_DoesNotCreateSpuriousLink(string source)
    {
        // Arrange and act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines
            .ShouldNotContain(static inline => inline is DocumentLink);
    }

    /// <summary>Verifies a complete code span may contain a closing bracket inside a valid link
    /// label without hiding the label's actual close.</summary>
    [Fact]
    public void Read_WhenValidLinkLabelContainsCodeSpanBracket_UsesOuterLabelClose()
    {
        // Arrange and act
        var link = new MarkdownDocumentReader().Read("[a `]` b](target)").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentLink>();

        // Assert
        link.Target.ShouldBe("target");
        link.Inlines.OfType<DocumentCodeSpan>().ShouldHaveSingleItem().Text.ShouldBe("]");
    }

    /// <summary>Verifies the active inner link wins when linked-image-shaped source is nested inside
    /// another candidate link label.</summary>
    [Fact]
    public void Read_WhenLinkLabelContainsALinkShapedNestedSequence_ResolvesTheInnerLink()
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read("[![alt](img.png)](http://example.invalid)")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();

        // Assert
        var link = paragraph.Inlines.OfType<DocumentLink>().ShouldHaveSingleItem();
        link.Target.ShouldBe("img.png");
        link.Text.ShouldBe("alt");
    }

    /// <summary>Verifies inner-link precedence propagates through an intermediate strong
    /// container.</summary>
    [Fact]
    public void Read_WhenLinkLabelContainsALinkShapedSequenceInsideStrong_ResolvesTheInnerLink()
    {
        // Arrange and act
        var paragraph = new MarkdownDocumentReader().Read("[**[inner](x)**](http://example.invalid)")
            .Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();

        // Assert
        var strong = paragraph.Inlines.OfType<DocumentStrong>().ShouldHaveSingleItem();
        strong.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentLink>().Target.ShouldBe("x");
        paragraph.Inlines.OfType<DocumentLink>().ShouldBeEmpty();
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

        // The documented boundary allows exactly 64 full levels before degrading; the 65th quote
        // marker is itself still a real DocumentBlockQuote (matching the list-nesting path's own
        // create-then-check shape), so a hostile stack bottoms out one level past the 64-level budget.
        depth.ShouldBeLessThanOrEqualTo(65);
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

    /// <summary>Verifies GFM extended autolinks recognize URL, www, and email forms at every
    /// supported left boundary and remove trailing delimiter punctuation.</summary>
    [Theory]
    [InlineData("www.example.com", "http://www.example.com")]
    [InlineData("foo@example.com", "mailto:foo@example.com")]
    [InlineData("*https://example.com*", "https://example.com")]
    [InlineData("_www.example.com_", "http://www.example.com")]
    [InlineData("~https://example.com~", "https://example.com")]
    public void Read_WhenGfmExtendedAutolinkIsValid_CreatesExpectedTarget(string source, string expectedTarget)
    {
        // Arrange
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.Autolinks });

        // Act
        var paragraph = reader.Read(source).Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();

        // Assert
        DescendantLinks(paragraph.Inlines).ShouldHaveSingleItem().Target.ShouldBe(expectedTarget);
    }

    /// <summary>Verifies malformed domains and embedded word prefixes remain literal.</summary>
    [Theory]
    [InlineData("http://")]
    [InlineData("www.")]
    [InlineData("prefixhttps://example.com")]
    [InlineData("foo@example")]
    public void Read_WhenGfmExtendedAutolinkIsInvalid_PreservesLiteralSource(string source)
    {
        // Arrange
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.Autolinks });

        // Act
        var result = reader.Read(source);

        // Assert
        result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines
            .ShouldNotContain(static inline => inline is DocumentLink);
        VisibleText(result).ShouldBe(source);
    }

    /// <summary>Verifies GFM strikethrough accepts one- and two-tilde delimiters.</summary>
    [Theory]
    [InlineData("~one~", "one")]
    [InlineData("~~two~~", "two")]
    [InlineData("~one two~", "one two")]
    public void Read_WhenGfmStrikethroughDelimiterIsValid_CreatesSemanticInline(string source, string expected)
    {
        // Arrange
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.Strikethrough });

        // Act
        var strike = reader.Read(source).Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>()
            .Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentStrikethrough>();

        // Assert
        strike.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe(expected);
    }

    /// <summary>Verifies longer runs and whitespace-adjacent delimiters remain literal.</summary>
    [Theory]
    [InlineData("This ~~~three~~~")]
    [InlineData("~~ foo~~")]
    [InlineData("~~foo ~~")]
    [InlineData("~ foo~")]
    public void Read_WhenGfmStrikethroughDelimiterIsInvalid_PreservesLiteralSource(string source)
    {
        // Arrange
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.Strikethrough });

        // Act
        var result = reader.Read(source);

        // Assert
        result.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines
            .ShouldNotContain(static inline => inline is DocumentStrikethrough);
        VisibleText(result).ShouldBe(source);
    }

    /// <summary>Verifies recursive block nesting is bounded before the semantic tree guard and
    /// reports a deterministic non-fatal diagnostic beyond the boundary.</summary>
    [Theory]
    [InlineData(64, false)]
    [InlineData(65, true)]
    [InlineData(300, true)]
    public void Read_WhenListNestingReachesReaderLimit_ReturnsBoundedTree(int levels, bool expectsDiagnostic)
    {
        // Arrange
        var source = string.Join(
            '\n',
            Enumerable.Range(0, levels).Select(level => $"{new string(' ', level * 2)}- level {level}"));

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks.ShouldNotBeEmpty();
        result.Diagnostics.Count.ShouldBe(expectsDiagnostic ? 1 : 0);

        if (expectsDiagnostic)
        {
            result.Diagnostics[0].Message.ShouldContain("nesting");
        }
    }

    /// <summary>Verifies recursive block-quote nesting shares the exact same 64-level boundary as
    /// list nesting, with content at the boundary parsed as real blocks rather than degraded text.</summary>
    [Theory]
    [InlineData(64, false)]
    [InlineData(65, true)]
    [InlineData(300, true)]
    public void Read_WhenBlockQuoteNestingReachesReaderLimit_ReturnsBoundedTree(int levels, bool expectsDiagnostic)
    {
        // Arrange
        var source = string.Concat(Enumerable.Repeat("> ", levels)) + "# heading";

        // Act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        result.Blocks.ShouldNotBeEmpty();
        result.Diagnostics.Count.ShouldBe(expectsDiagnostic ? 1 : 0);

        if (expectsDiagnostic)
        {
            result.Diagnostics[0].Message.ShouldContain("nesting");
        }
        else
        {
            var block = result.Blocks.ShouldHaveSingleItem();

            for (var level = 0; level < levels; level++)
            {
                block = block.ShouldBeOfType<DocumentBlockQuote>().Blocks.ShouldHaveSingleItem();
            }

            _ = block.ShouldBeOfType<DocumentHeading>();
        }
    }

    /// <summary>Verifies the callout-header branch of block-quote parsing uses the same 64-level
    /// boundary as plain block quotes, without an off-by-one on the depth comparison.</summary>
    [Theory]
    [InlineData(64, false)]
    [InlineData(65, true)]
    public void Read_WhenCalloutNestingReachesReaderLimit_ReturnsBoundedTree(int levels, bool expectsDiagnostic)
    {
        // Arrange
        var markers = string.Concat(Enumerable.Repeat("> ", levels));
        var source = $"{markers}[!NOTE] Title\n{markers}Body";
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.Callouts });

        // Act
        var result = reader.Read(source);

        // Assert
        result.Diagnostics.Count.ShouldBe(expectsDiagnostic ? 1 : 0);

        var block = result.Blocks.ShouldHaveSingleItem();

        for (var level = 0; level < levels - 1; level++)
        {
            block = block.ShouldBeOfType<DocumentBlockQuote>().Blocks.ShouldHaveSingleItem();
        }

        if (expectsDiagnostic)
        {
            _ = block.ShouldBeOfType<DocumentBlockQuote>();
        }
        else
        {
            var callout = block.ShouldBeOfType<DocumentCallout>();
            callout.Kind.ShouldBe("NOTE");
            callout.Title.ShouldBe("Title");
            callout.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
                .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("Body");
        }
    }

    /// <summary>Verifies hostile link, code-span, extended-autolink, angle-autolink, strikethrough, and
    /// emphasis candidates use bounded delimiter indexing instead of rescanning their remaining suffix -
    /// or, for emphasis, the remainder of the source - at every opener.</summary>
    [Fact]
    public void Read_WhenInlineCandidatesAreHostile_ExaminesBoundedCandidateWork()
    {
        // Arrange
        var cases = new[]
        {
            (Source: new string('[', 10_000) + "](target)", Extensions: MarkdownExtension.None),
            (Source: string.Join('a', Enumerable.Range(1, 400).Select(static length => new string('`', length))),
                Extensions: MarkdownExtension.None),
            (Source: "https://example.com/" + new string(')', 10_000), Extensions: MarkdownExtension.Autolinks),
            (Source: new string('(', 10_000), Extensions: MarkdownExtension.Autolinks),
            (Source: new string('<', 10_000), Extensions: MarkdownExtension.None),
            (Source: string.Concat(Enumerable.Repeat("~a ", 4_000)), Extensions: MarkdownExtension.Strikethrough),
            (Source: string.Concat(Enumerable.Repeat(" *a", 4_000)), Extensions: MarkdownExtension.None)
        };

        foreach (var (source, extensions) in cases)
        {
            var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = extensions });

            // Act
            var result = reader.Read(source);

            // Assert - a generous linear multiplier: every added index now makes its own bounded pass
            // over the source, so the constant factor is higher than a single pass, but it stays fixed
            // regardless of source length. The old quadratic behavior this guards against would blow
            // past this bound by orders of magnitude at this input size.
            result.Blocks.ShouldNotBeEmpty();
            reader.InlineCandidateScanCount.ShouldBeLessThanOrEqualTo(source.Length * 12);
        }
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

    /// <summary>Verifies CommonMark lazy continuation: a second line starting at column zero - well
    /// under the marker's own indentation - still continues the item's open paragraph rather than
    /// ending the item.</summary>
    [Fact]
    public void Read_WhenListItemContinuationLineStartsAtColumnZero_ContinuesItem()
    {
        // Arrange and act
        var item = new MarkdownDocumentReader().Read("- item\ncontinued").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem();

        // Assert
        var paragraph = item.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();
        paragraph.Inlines.Count.ShouldBe(3);
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("item");
        _ = paragraph.Inlines[1].ShouldBeOfType<DocumentSoftBreak>();
        paragraph.Inlines[2].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("continued");
    }

    /// <summary>Verifies a marker-less line that itself looks like another block's start still
    /// interrupts a list item's open paragraph - and ends the list, since the line no longer
    /// carries any marker at all - instead of being absorbed as a lazy continuation.</summary>
    [Fact]
    public void Read_WhenMarkerLessLineLooksLikeHeading_InterruptsListItemParagraph()
    {
        // Arrange and act
        var result = new MarkdownDocumentReader().Read("- item\n# Heading");

        // Assert
        result.Blocks.Count.ShouldBe(2);
        var list = result.Blocks[0].ShouldBeOfType<DocumentList>();
        list.Items.ShouldHaveSingleItem().Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>()
            .Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("item");
        result.Blocks[1].ShouldBeOfType<DocumentHeading>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("Heading");
    }

    /// <summary>Verifies the lazy-continuation break check measures a candidate line against the
    /// item's full content column - marker indent plus marker width - rather than the bare marker
    /// indent. A three-space-indented bullet has a content column of five (indent three plus the
    /// "- " marker's own width of two); a lone leading tab always expands to column four, which
    /// clears the bare indent (three) but still falls short of the true content column (five). The
    /// item's own last block is a heading (not a paragraph), so lazy continuation can never apply,
    /// and the under-indented tab line must end the item and the list instead of being swallowed as
    /// item content.</summary>
    [Fact]
    public void Read_WhenTabIndentedLineFallsShortOfWidenedItemContentColumn_EndsListItem()
    {
        // Arrange and act
        var blocks = new MarkdownDocumentReader().Read("   - # heading\n\t***").Blocks;

        // Assert
        blocks.Count.ShouldBe(2);
        var list = blocks[0].ShouldBeOfType<DocumentList>();
        list.Items.ShouldHaveSingleItem().Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentHeading>()
            .Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("heading");
        blocks[1].ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("\t***");
    }

    /// <summary>Verifies a line that reaches the item's content column exactly always continues the
    /// item as an ordinary child block, even when the item's last block is not a paragraph - the
    /// break check must use a strict "less than" comparison against the content column, not
    /// "less than or equal to", or an exact-column match would incorrectly end the item. Mirrors
    /// CommonMark's own worked example of a heading followed by further indented item content.
    /// </summary>
    [Fact]
    public void Read_WhenLineReachesContentColumnExactly_ContinuesItemAfterNonParagraphBlock()
    {
        // Arrange and act
        var item = new MarkdownDocumentReader().Read("- # Foo\n  ## Bar\n  Baz").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem();

        // Assert
        item.Blocks.Count.ShouldBe(3);
        item.Blocks[0].ShouldBeOfType<DocumentHeading>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("Foo");
        item.Blocks[1].ShouldBeOfType<DocumentHeading>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("Bar");
        item.Blocks[2].ShouldBeOfType<DocumentParagraph>().Inlines.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("Baz");
    }

    /// <summary>Verifies lazy continuation composes across nesting levels: a block quote nested
    /// inside a list item still absorbs a bare line that carries neither the item's own indentation
    /// nor the quote's '&gt;' marker, because each level independently tracks its own open
    /// paragraph.</summary>
    [Fact]
    public void Read_WhenQuoteNestedInListHasBareContinuationLine_ContinuesQuoteParagraph()
    {
        // Arrange and act
        var item = new MarkdownDocumentReader().Read("- > quoted\n  continued").Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentList>().Items.ShouldHaveSingleItem();

        // Assert
        var paragraph = item.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentBlockQuote>().Blocks
            .ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>();
        paragraph.Inlines.Count.ShouldBe(3);
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("quoted");
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

    /// <summary>Verifies interactive item syntax does not create a separate list type across a
    /// blank separator.</summary>
    [Theory]
    [InlineData(MarkdownExtension.TaskLists, "- [ ] task\n\n- plain", true)]
    [InlineData(MarkdownExtension.TaskLists, "- plain\n\n- [ ] task", false)]
    [InlineData(MarkdownExtension.RadioLists, "- ( ) choice\n\n- plain", true)]
    [InlineData(MarkdownExtension.RadioLists, "- plain\n\n- ( ) choice", false)]
    public void Read_WhenBlankSeparatedItemsMixInteractiveAndPlainContent_ProducesOneLooseList(
        MarkdownExtension extension,
        string source,
        bool firstIsInteractive)
    {
        // Arrange
        var reader = new MarkdownDocumentReader(new MarkdownOptions { Extensions = extension });

        // Act
        var list = reader.Read(source).Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentList>();

        // Assert
        list.IsLoose.ShouldBeTrue();
        list.Items.Count.ShouldBe(2);
        var interactive = list.Items[firstIsInteractive ? 0 : 1].Blocks.ShouldHaveSingleItem();
        var plain = list.Items[firstIsInteractive ? 1 : 0].Blocks.ShouldHaveSingleItem()
            .ShouldBeOfType<DocumentParagraph>();

        if (extension == MarkdownExtension.TaskLists)
        {
            _ = interactive.ShouldBeOfType<DocumentParagraph>().Inlines[0]
                .ShouldBeOfType<DocumentInlineControl>().Control.ShouldBeOfType<CheckBox>();
        }
        else
        {
            _ = interactive.ShouldBeOfType<DocumentBlockControl>().Control.ShouldBeOfType<RadioButton>();
        }

        plain.Inlines.OfType<DocumentInlineControl>().ShouldBeEmpty();
    }

    /// <summary>Verifies blank lines that terminate a list do not retroactively loosen its items.</summary>
    [Theory]
    [InlineData("- one\n- two\n\nparagraph", typeof(DocumentParagraph))]
    [InlineData("- one\n- two\n\n# heading", typeof(DocumentHeading))]
    [InlineData("- one\n- two\n\n> quote", typeof(DocumentBlockQuote))]
    [InlineData("- one\n- two\n\n```\ncode\n```", typeof(DocumentCodeBlock))]
    [InlineData("- one\n- two\n\n", null)]
    public void Read_WhenBlankLineEndsList_KeepsListTight(string source, Type? followingBlockType)
    {
        // Arrange and act
        var result = new MarkdownDocumentReader().Read(source);

        // Assert
        var list = result.Blocks[0].ShouldBeOfType<DocumentList>();
        list.IsLoose.ShouldBeFalse();
        list.Items.Count.ShouldBe(2);
        result.Blocks.Count.ShouldBe(followingBlockType is null ? 1 : 2);

        if (followingBlockType is not null)
        {
            result.Blocks[1].GetType().ShouldBe(followingBlockType);
        }
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

    private static IEnumerable<DocumentLink> DescendantLinks(IEnumerable<DocumentInline> inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is DocumentLink link)
            {
                yield return link;
            }

            if (inline is DocumentInlineContainer container)
            {
                foreach (var descendant in DescendantLinks(container.Inlines))
                {
                    yield return descendant;
                }
            }
        }
    }
}
