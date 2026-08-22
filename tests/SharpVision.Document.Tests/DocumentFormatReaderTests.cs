// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

using DocumentControl = Controls.Documents.Document;

/// <summary>Verifies the format abstraction, bounded input, and stream-loading surface.</summary>
public sealed class DocumentFormatReaderTests
{
    /// <summary>Verifies a non-Markdown reader can supply the structure consumed by Document.</summary>
    [Fact]
    public void Load_WhenCustomFormatReaderIsUsed_AppliesItsDetachedTree()
    {
        // Arrange
        var document = new DocumentControl();

        // Act
        _ = document.Load("plain", new PlainTextDocumentReaderProbe());

        // Assert
        document.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("plain");
    }

    /// <summary>Verifies an asynchronous stream load observes the same character bound before replacement.</summary>
    [Fact]
    public async Task LoadAsync_WhenStreamExceedsLimit_ThrowsAndPreservesExistingBlocksAsync()
    {
        // Arrange
        var document = new DocumentControl
        {
            Blocks = { new DocumentParagraph("old") }
        };
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("12345"));
        var options = new DocumentReadOptions { MaximumCharacters = 4 };

        // Act
        var action = async () => await document.LoadAsync(
            stream,
            new PlainTextDocumentReaderProbe(),
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = await action.ShouldThrowAsync<ArgumentOutOfRangeException>();
        document.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("old");
    }

    /// <summary>Verifies a reader result reused after attachment is rejected before replacement begins.</summary>
    [Fact]
    public void Load_WhenReaderReturnsAnAttachedResult_ThrowsAndPreservesExistingBlocks()
    {
        // Arrange
        var result = new DocumentReadResult([new DocumentParagraph("parsed")]);
        var reader = new StaticDocumentFormatReaderProbe(result);
        var first = new DocumentControl();
        _ = first.Load("first", reader);
        var destination = new DocumentControl
        {
            Blocks = { new DocumentParagraph("old") }
        };

        // Act
        var action = () => destination.Load("second", reader);

        // Assert
        _ = action.ShouldThrow<ArgumentException>();
        destination.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("old");
    }

    /// <summary>Verifies document lifecycle validation runs before arbitrary reader code.</summary>
    [Fact]
    public void Load_WhenDocumentIsDisposed_DoesNotInvokeReader()
    {
        // Arrange
        var document = new DocumentControl();
        var reader = new StaticDocumentFormatReaderProbe(
            new DocumentReadResult([new DocumentParagraph("parsed")]));
        document.Dispose();

        // Act
        var action = () => document.Load("source", reader);

        // Assert
        _ = action.ShouldThrow<ObjectDisposedException>();
        reader.ReadCalls.ShouldBe(0);
    }

    /// <summary>Verifies a format result cannot expose the same physical embedded control twice.</summary>
    [Fact]
    public void Constructor_WhenResultDuplicatesEmbeddedControl_ThrowsBeforeOwningEitherBlock()
    {
        // Arrange
        var control = new CheckBox("shared");
        var first = new DocumentParagraph { Inlines = { new DocumentInlineControl(control) } };
        var second = new DocumentParagraph { Inlines = { new DocumentInlineControl(control) } };
        // Act
        var action = () => new DocumentReadResult([first, second]);

        // Assert
        _ = action.ShouldThrow<ArgumentException>();
        first.IsAttached.ShouldBeFalse();
        second.IsAttached.ShouldBeFalse();
    }

    /// <summary>Verifies a control disposed after wrapper creation cannot enter a format result.</summary>
    [Fact]
    public void Constructor_WhenResultContainsDisposedEmbeddedControl_ThrowsObjectDisposedException()
    {
        // Arrange
        var control = new CheckBox("disposed");
        var paragraph = new DocumentParagraph
        {
            Inlines = { new DocumentInlineControl(control) }
        };
        control.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(() => new DocumentReadResult([paragraph]));
        paragraph.IsAttached.ShouldBeFalse();
    }
}
