// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

/// <summary>Verifies format-reader options, spans, diagnostics, and immutable results.</summary>
public sealed class DocumentReadOptionsTests
{
    /// <summary>Verifies reader entry points reject null arguments and accept empty input.</summary>
    [Fact]
    public void ConstructorAndRead_WhenArgumentsAreNullOrEmpty_EnforceTheirContracts()
    {
        // Arrange and act
        var reader = new MarkdownDocumentReader();

        // Assert
        _ = Should.Throw<ArgumentNullException>(static () => new MarkdownDocumentReader(null!));
        _ = Should.Throw<ArgumentNullException>(() => reader.Read(null!));
        reader.Read(string.Empty).Blocks.ShouldBeEmpty();
    }

    /// <summary>Verifies the format-independent source limit is positive and preserves state after failure.</summary>
    [Fact]
    public void MaximumCharacters_WhenAssigned_ValidatesAndPreservesThePreviousValue()
    {
        // Arrange
        var options = new DocumentReadOptions();
        options.MaximumCharacters.ShouldBe(4 * 1024 * 1024);

        // Act
        options.MaximumCharacters = 7;

        // Assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => options.MaximumCharacters = 0);
        options.MaximumCharacters.ShouldBe(7);
    }

    /// <summary>Verifies Markdown extension flags are copied, composable, and unknown-bit safe.</summary>
    [Fact]
    public void Extensions_WhenAssigned_ValidatesAndReaderCopiesTheSelection()
    {
        // Arrange
        var options = new MarkdownOptions { Extensions = MarkdownExtension.WikiLinks };
        var reader = new MarkdownDocumentReader(options);

        // Act
        options.Extensions = MarkdownExtension.None;

        // Assert
        _ = reader.Read("[[target]]").Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>()
            .Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentLink>();
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => options.Extensions = (MarkdownExtension) (1 << 20));
        options.Extensions.ShouldBe(MarkdownExtension.None);
    }

    /// <summary>Verifies default, GitHub-flavored, and all-extension profiles change parsing behavior.</summary>
    [Fact]
    public void Extensions_WhenCompositeProfilesAreUsed_EnableTheirSyntaxFamilies()
    {
        // Arrange
        var options = new MarkdownOptions();
        var baseline = new MarkdownDocumentReader(options);
        var github = new MarkdownDocumentReader(new MarkdownOptions
        {
            Extensions = MarkdownExtension.GitHubFlavored
        });
        var all = new MarkdownDocumentReader(new MarkdownOptions { Extensions = MarkdownExtension.All });

        // Act and assert
        options.Extensions.ShouldBe(MarkdownExtension.None);
        baseline.Read("~~x~~").Blocks[0].ShouldBeOfType<DocumentParagraph>().Inlines
            .ShouldNotContain(static inline => inline is DocumentStrikethrough);
        github.Read("~~x~~").Blocks[0].ShouldBeOfType<DocumentParagraph>().Inlines
            .ShouldContain(static inline => inline is DocumentStrikethrough);
        all.Read("[[target]]").Blocks[0].ShouldBeOfType<DocumentParagraph>().Inlines
            .ShouldContain(static inline => inline is DocumentLink);
    }

    /// <summary>Verifies source spans store non-negative UTF-16 coordinates.</summary>
    [Fact]
    public void Constructor_WhenSourceSpanIsCreated_ValidatesAndStoresCoordinates()
    {
        // Arrange and act
        var span = new DocumentSourceSpan(3, 5);

        // Assert
        span.Offset.ShouldBe(3);
        span.Length.ShouldBe(5);
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new DocumentSourceSpan(-1, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new DocumentSourceSpan(0, -1));
    }

    /// <summary>Verifies diagnostics retain validated messages and source spans.</summary>
    [Fact]
    public void Constructor_WhenDiagnosticIsCreated_ValidatesAndStoresValues()
    {
        // Arrange
        var span = new DocumentSourceSpan(2, 4);

        // Act
        var diagnostic = new DocumentDiagnostic("message", span);

        // Assert
        diagnostic.Message.ShouldBe("message");
        diagnostic.Span.ShouldBe(span);
        _ = Should.Throw<ArgumentException>(() => new DocumentDiagnostic(" ", span));
        _ = Should.Throw<ArgumentNullException>(() => new DocumentDiagnostic(null!, span));
    }

    /// <summary>Verifies result diagnostics cannot contain null entries.</summary>
    [Fact]
    public void Constructor_WhenDiagnosticsContainNull_ThrowsArgumentNullException() =>
        // Arrange, act, and assert
        _ = Should.Throw<ArgumentNullException>(
            static () => new DocumentReadResult([], [null!]));

    /// <summary>Verifies results snapshot detached blocks and diagnostics in source order.</summary>
    [Fact]
    public void Constructor_WhenResultIsValid_SnapshotsBlocksAndDiagnostics()
    {
        // Arrange
        var block = new DocumentParagraph("body");
        var diagnostic = new DocumentDiagnostic("note", new DocumentSourceSpan(0, 1));
        var blocks = new List<DocumentBlock> { block };
        var diagnostics = new List<DocumentDiagnostic> { diagnostic };

        // Act
        var result = new DocumentReadResult(blocks, diagnostics);
        blocks.Clear();
        diagnostics.Clear();

        // Assert
        result.Blocks.ShouldBe([block]);
        result.Diagnostics.ShouldBe([diagnostic]);
        block.IsAttached.ShouldBeFalse();
    }

    /// <summary>Verifies the published snapshots cannot be cast back to mutable arrays and changed
    /// after constructor validation.</summary>
    [Fact]
    public void Constructor_WhenResultIsPublished_ExposesReadOnlySnapshots()
    {
        // Arrange
        var block = new DocumentParagraph("body");
        var diagnostic = new DocumentDiagnostic("note", new DocumentSourceSpan(0, 1));
        var result = new DocumentReadResult([block], [diagnostic]);

        // Act and assert
        _ = Should.Throw<NotSupportedException>(
            () => ((IList<DocumentBlock>) result.Blocks)[0] = new DocumentParagraph("replacement"));
        _ = Should.Throw<NotSupportedException>(
            () => ((IList<DocumentDiagnostic>) result.Diagnostics)[0] =
                new DocumentDiagnostic("replacement", new DocumentSourceSpan(0, 1)));
        result.Blocks[0].ShouldBeSameAs(block);
        result.Diagnostics[0].ShouldBeSameAs(diagnostic);
    }

    /// <summary>Verifies result block inputs reject null collections, entries, and duplicate roots.</summary>
    [Fact]
    public void Constructor_WhenResultBlocksAreInvalid_ThrowsBeforePublishingAResult()
    {
        // Arrange
        var block = new DocumentParagraph("body");

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentReadResult(null!));
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentReadResult([null!]));
        _ = Should.Throw<ArgumentException>(() => new DocumentReadResult([block, block]));
        block.IsAttached.ShouldBeFalse();
    }

    /// <summary>Verifies Markdown enforces the exact configured UTF-16 boundary.</summary>
    [Fact]
    public void Read_WhenSourceReachesCharacterBoundary_AcceptsExactAndRejectsExcess()
    {
        // Arrange
        var reader = new MarkdownDocumentReader();
        var options = new DocumentReadOptions { MaximumCharacters = 4 };

        // Act and assert
        _ = reader.Read("1234", options).Blocks.ShouldHaveSingleItem();
        _ = Should.Throw<ArgumentOutOfRangeException>(() => reader.Read("12345", options));
    }
}
