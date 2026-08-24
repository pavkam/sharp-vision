// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

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
                    _ = text.Append(inline.Text);
                }
            }
        }

        return text.ToString();
    }
}
