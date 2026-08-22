// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Documents;

/// <summary>Verifies every <see cref="DocumentBlock"/> node's construction, validated state, and
/// owned child collections, plus <see cref="DocumentListItem"/>, which is a node but deliberately
/// not a block.</summary>
public sealed class DocumentBlockTests
{
    /// <summary>Verifies an empty paragraph owns an empty inline collection.</summary>
    [Fact]
    public void Constructor_WhenParagraphIsEmpty_OwnsAnEmptyInlineCollection()
    {
        // Arrange and act
        var paragraph = new DocumentParagraph();

        // Assert
        paragraph.Inlines.Count.ShouldBe(0);
    }

    /// <summary>Verifies the text paragraph constructor adds exactly one markup text run.</summary>
    [Fact]
    public void Constructor_WhenParagraphTakesText_AddsOneOwnedTextRun()
    {
        // Arrange and act
        var paragraph = new DocumentParagraph("a <b>bold</b> word");

        // Assert
        paragraph.Inlines.Count.ShouldBe(1);
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("a <b>bold</b> word");
    }

    /// <summary>Verifies the text paragraph constructor rejects null text.</summary>
    [Fact]
    public void Constructor_WhenParagraphTextIsNull_ThrowsArgumentNullException() =>
        // Arrange, act, and assert
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentParagraph(null!));

    /// <summary>Verifies a heading records its level and owns an empty inline collection.</summary>
    [Fact]
    public void Constructor_WhenHeadingTakesLevel_RecordsItAndOwnsAnEmptyInlineCollection()
    {
        // Arrange and act
        var heading = new DocumentHeading(DocumentHeading.MaximumLevel);

        // Assert
        heading.Level.ShouldBe(6);
        heading.Inlines.Count.ShouldBe(0);
    }

    /// <summary>Verifies the text heading constructor records the level and adds one text run.</summary>
    [Fact]
    public void Constructor_WhenHeadingTakesLevelAndText_RecordsBoth()
    {
        // Arrange and act
        var heading = new DocumentHeading(2, "Title");

        // Assert
        heading.Level.ShouldBe(2);
        heading.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("Title");
    }

    /// <summary>Verifies a heading level outside one through six is rejected on construction.</summary>
    /// <param name="level">The out-of-range level.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Constructor_WhenHeadingLevelIsOutsideTheValidRange_ThrowsArgumentOutOfRangeException(int level) =>
        // Arrange, act, and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new DocumentHeading(level));

    /// <summary>Verifies a heading level outside one through six is rejected on assignment.</summary>
    /// <param name="level">The out-of-range level.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Level_WhenAssignedOutsideTheValidRange_ThrowsArgumentOutOfRangeException(int level)
    {
        // Arrange
        var heading = new DocumentHeading(1);

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => heading.Level = level);
        heading.Level.ShouldBe(1);
    }

    /// <summary>Verifies the text heading constructor rejects null text.</summary>
    [Fact]
    public void Constructor_WhenHeadingTextIsNull_ThrowsArgumentNullException() =>
        // Arrange, act, and assert
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentHeading(1, null!));

    /// <summary>Verifies an empty block quote owns an empty block collection.</summary>
    [Fact]
    public void Constructor_WhenBlockQuoteIsEmpty_OwnsAnEmptyBlockCollection()
    {
        // Arrange and act
        var quote = new DocumentBlockQuote();

        // Assert
        quote.Blocks.Count.ShouldBe(0);
    }

    /// <summary>Verifies the block constructor adopts the supplied detached block.</summary>
    [Fact]
    public void Constructor_WhenBlockQuoteTakesABlock_AdoptsIt()
    {
        // Arrange
        var paragraph = new DocumentParagraph("Quoted");

        // Act
        var quote = new DocumentBlockQuote(paragraph);

        // Assert
        quote.Blocks.Count.ShouldBe(1);
        quote.Blocks[0].ShouldBeSameAs(paragraph);
    }

    /// <summary>Verifies the text constructor wraps the markup in one owned paragraph.</summary>
    [Fact]
    public void Constructor_WhenBlockQuoteTakesText_WrapsItInOneParagraph()
    {
        // Arrange and act
        var quote = new DocumentBlockQuote("Quoted");

        // Assert
        quote.Blocks[0].ShouldBeOfType<DocumentParagraph>()
            .Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("Quoted");
    }

    /// <summary>Verifies both block quote convenience constructors reject null arguments.</summary>
    [Fact]
    public void Constructor_WhenBlockQuoteArgumentIsNull_ThrowsArgumentNullException()
    {
        // Arrange, act, and assert
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentBlockQuote((DocumentBlock) null!));
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentBlockQuote((string) null!));
    }

    /// <summary>Verifies a block quote refuses a block that already belongs to a tree.</summary>
    [Fact]
    public void Constructor_WhenBlockQuoteBlockIsAlreadyOwned_ThrowsArgumentException()
    {
        // Arrange
        var paragraph = new DocumentParagraph("Quoted");
        _ = new DocumentBlockQuote(paragraph);

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => new DocumentBlockQuote(paragraph));
    }

    /// <summary>Verifies an empty code block starts with empty text.</summary>
    [Fact]
    public void Constructor_WhenCodeBlockIsEmpty_StartsWithEmptyText()
    {
        // Arrange and act
        var code = new DocumentCodeBlock();

        // Assert
        code.Text.ShouldBe(string.Empty);
    }

    /// <summary>Verifies a code block stores its literal text verbatim, markup included.</summary>
    [Fact]
    public void Constructor_WhenCodeBlockTakesText_StoresItVerbatim()
    {
        // Arrange and act
        var code = new DocumentCodeBlock("<b>x</b>");

        // Assert
        code.Text.ShouldBe("<b>x</b>");
    }

    /// <summary>Verifies code block text rejects null on construction and on assignment.</summary>
    [Fact]
    public void Text_WhenCodeBlockTextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var code = new DocumentCodeBlock("x");

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentCodeBlock(null!));
        _ = Should.Throw<ArgumentNullException>(() => code.Text = null!);
        code.Text.ShouldBe("x");
    }

    /// <summary>Verifies code-block language metadata is optional, mutable, and null-safe.</summary>
    [Fact]
    public void Language_WhenAssigned_TracksNonNullMetadataAndPreservesItAfterFailure()
    {
        // Arrange
        var code = new DocumentCodeBlock("x");
        code.Language.ShouldBe(string.Empty);

        // Act
        code.Language = "csharp";

        // Assert
        code.Language.ShouldBe("csharp");
        _ = Should.Throw<ArgumentNullException>(() => code.Language = null!);
        code.Language.ShouldBe("csharp");
    }

    /// <summary>Verifies callouts own typed, titled, nested semantic content with validated state.</summary>
    [Fact]
    public void Callout_WhenConstructedAndMutated_UsesDefaultsAndPreservesStateAfterInvalidValues()
    {
        // Arrange
        var callout = new DocumentCallout();
        callout.Kind.ShouldBe("NOTE");
        callout.Title.ShouldBe(string.Empty);
        callout.Blocks.Count.ShouldBe(0);

        // Act
        callout.Kind = "WARNING";
        callout.Title = "Careful";
        callout.Blocks.Add(new DocumentParagraph("Body"));

        // Assert
        callout.Kind.ShouldBe("WARNING");
        callout.Title.ShouldBe("Careful");
        callout.Blocks.Count.ShouldBe(1);
        _ = Should.Throw<ArgumentException>(() => callout.Kind = " ");
        _ = Should.Throw<ArgumentNullException>(() => callout.Title = null!);
        callout.Kind.ShouldBe("WARNING");
        callout.Title.ShouldBe("Careful");
    }

    /// <summary>Verifies an empty list item owns an empty block collection.</summary>
    [Fact]
    public void Constructor_WhenListItemIsEmpty_OwnsAnEmptyBlockCollection()
    {
        // Arrange and act
        var item = new DocumentListItem();

        // Assert
        item.Blocks.Count.ShouldBe(0);
    }

    /// <summary>Verifies the block list-item constructor adopts the supplied detached block.</summary>
    [Fact]
    public void Constructor_WhenListItemTakesABlock_AdoptsIt()
    {
        // Arrange
        var paragraph = new DocumentParagraph("First");

        // Act
        var item = new DocumentListItem(paragraph);

        // Assert
        item.Blocks[0].ShouldBeSameAs(paragraph);
    }

    /// <summary>Verifies the text-and-nested-list constructor adds the paragraph before the list.</summary>
    [Fact]
    public void Constructor_WhenListItemTakesTextAndNestedList_AddsParagraphThenList()
    {
        // Arrange
        var nested = new DocumentList();

        // Act
        var item = new DocumentListItem("First", nested);

        // Assert
        item.Blocks.Count.ShouldBe(2);
        _ = item.Blocks[0].ShouldBeOfType<DocumentParagraph>();
        item.Blocks[1].ShouldBeSameAs(nested);
    }

    /// <summary>Verifies every list-item convenience constructor rejects a null argument.</summary>
    [Fact]
    public void Constructor_WhenListItemArgumentIsNull_ThrowsArgumentNullException()
    {
        // Arrange, act, and assert
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentListItem((DocumentBlock) null!));
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentListItem((string) null!));
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentListItem("First", null!));
    }

    /// <summary>Verifies a list item refuses a nested list that already belongs to a tree.</summary>
    [Fact]
    public void Constructor_WhenNestedListIsAlreadyOwned_ThrowsArgumentException()
    {
        // Arrange
        var nested = new DocumentList();
        _ = new DocumentListItem("Owner", nested);

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => new DocumentListItem("Second", nested));
    }
}
