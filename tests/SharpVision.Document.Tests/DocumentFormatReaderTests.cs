// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

using DocumentControl = Controls.Documents.Document;

/// <summary>Verifies the format abstraction, bounded input, and stream-loading surface.</summary>
public sealed class DocumentFormatReaderTests
{
    /// <summary>A deliberately distinct exception type simulating an application-level failure from a
    /// focus callback, so tests can assert the original exception propagates without conflating it with
    /// a library-raised exception such as <see cref="InvalidOperationException"/>.</summary>
    private sealed class SimulatedCallbackFailureException: Exception
    {
        public SimulatedCallbackFailureException(string message) : base(message)
        {
        }
    }

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
        stream.Position.ShouldBe(0);
        document.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("old");
    }

    /// <summary>Verifies a failed bounded read restores the exact starting byte position even when
    /// the decoded characters use multiple UTF-8 bytes.</summary>
    [Fact]
    public async Task LoadAsync_WhenMultibyteTextExceedsLimit_RestoresSeekableSourcePositionAsync()
    {
        // Arrange
        var document = new DocumentControl();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("xxéé"));
        stream.Position = 2;
        var options = new DocumentReadOptions { MaximumCharacters = 1 };

        // Act
        var action = async () => await document.LoadAsync(
            stream,
            new PlainTextDocumentReaderProbe(),
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = await action.ShouldThrowAsync<ArgumentOutOfRangeException>();
        stream.Position.ShouldBe(2);
    }

    /// <summary>Verifies strict decoding failure restores a seekable source to its original byte
    /// position.</summary>
    [Fact]
    public async Task LoadAsync_WhenUtf8IsMalformed_RestoresSeekableSourcePositionAsync()
    {
        // Arrange
        var document = new DocumentControl();
        await using var stream = new MemoryStream([0xc3, 0x28]);

        // Act
        var action = async () => await document.LoadAsync(
            stream,
            new PlainTextDocumentReaderProbe(),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = await action.ShouldThrowAsync<DecoderFallbackException>();
        stream.Position.ShouldBe(0);
    }

    /// <summary>Verifies BOM detection preserves strict decoding for every Unicode encoding it can
    /// select instead of silently installing replacement fallback.</summary>
    [Fact]
    public async Task LoadAsync_WhenBomPrecedesMalformedUnicode_ThrowsAndPreservesExistingBlocksAsync()
    {
        // Arrange
        var cases = new byte[][]
        {
            [0xef, 0xbb, 0xbf, 0xc3, 0x28],
            [0xff, 0xfe, 0x00, 0xd8],
            [0xfe, 0xff, 0xd8, 0x00],
            [0xff, 0xfe, 0x00, 0x00, 0x00, 0x00, 0x11, 0x00],
            [0x00, 0x00, 0xfe, 0xff, 0x00, 0x11, 0x00, 0x00]
        };

        foreach (var bytes in cases)
        {
            var document = new DocumentControl { Blocks = { new DocumentParagraph("old") } };
            await using var stream = new MemoryStream(bytes);

            // Act
            var action = async () => await document.LoadAsync(
                stream,
                new PlainTextDocumentReaderProbe(),
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            _ = await action.ShouldThrowAsync<DecoderFallbackException>();
            stream.Position.ShouldBe(0);
            document.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
                .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("old");
        }
    }

    /// <summary>Verifies a pre-canceled token is observed before any block replaces the current tree.</summary>
    [Fact]
    public async Task LoadAsync_WhenTokenIsAlreadyCanceled_ThrowsAndPreservesExistingBlocksAsync()
    {
        // Arrange
        var document = new DocumentControl
        {
            Blocks = { new DocumentParagraph("old") }
        };
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("new text"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var action = async () => await document.LoadAsync(
            stream,
            new PlainTextDocumentReaderProbe(),
            cancellationToken: cancellation.Token);

        // Assert
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        stream.Position.ShouldBe(0);
        document.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("old");
    }

    /// <summary>Verifies cancellation delivered by the EOF read is observed before parsing or
    /// replacing the current tree.</summary>
    [Fact]
    public async Task LoadAsync_WhenEofReadCancelsToken_ThrowsAndPreservesExistingBlocksAsync()
    {
        // Arrange
        var document = new DocumentControl
        {
            Blocks = { new DocumentParagraph("old") }
        };
        using var cancellation = new CancellationTokenSource();
        await using var stream = new CancelAtEndStream(Encoding.UTF8.GetBytes("new"), cancellation);

        // Act
        var action = async () => await document.LoadAsync(
            stream,
            new PlainTextDocumentReaderProbe(),
            cancellationToken: cancellation.Token);

        // Assert
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        stream.Position.ShouldBe(0);
        document.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("old");
    }

    /// <summary>Verifies cancellation observed after format parsing still wins before tree
    /// validation and replacement.</summary>
    [Fact]
    public async Task LoadAsync_WhenReaderCancelsToken_ThrowsAndPreservesExistingBlocksAsync()
    {
        // Arrange
        var document = new DocumentControl
        {
            Blocks = { new DocumentParagraph("old") }
        };
        using var cancellation = new CancellationTokenSource();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("new"));

        // Act
        var action = async () => await document.LoadAsync(
            stream,
            new CancelingDocumentFormatReaderProbe(cancellation),
            cancellationToken: cancellation.Token);

        // Assert
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        stream.Position.ShouldBe(0);
        document.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("old");
    }

    /// <summary>Verifies a format-reader failure after decoding restores a seekable source to its
    /// original byte position.</summary>
    [Fact]
    public async Task LoadAsync_WhenFormatReaderFails_RestoresSeekableSourcePositionAsync()
    {
        // Arrange
        var document = new DocumentControl();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("text"));

        // Act
        var action = async () => await document.LoadAsync(
            stream,
            new ThrowingDocumentFormatReaderProbe(),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = await action.ShouldThrowAsync<InvalidDataException>();
        stream.Position.ShouldBe(0);
    }

    /// <summary>Verifies a disposed document rejects asynchronous loading before reading or invoking
    /// the format reader.</summary>
    [Fact]
    public async Task LoadAsync_WhenDocumentIsDisposed_DoesNotConsumeOrParseSourceAsync()
    {
        // Arrange
        var document = new DocumentControl();
        var reader = new StaticDocumentFormatReaderProbe(
            new DocumentReadResult([new DocumentParagraph("parsed")]));
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("text"));
        document.Dispose();

        // Act
        var action = async () => await document.LoadAsync(
            stream,
            reader,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = await action.ShouldThrowAsync<ObjectDisposedException>();
        stream.Position.ShouldBe(0);
        reader.ReadCalls.ShouldBe(0);
    }

    /// <summary>Verifies an attached document rejects off-dispatcher asynchronous loading before
    /// reading or invoking the format reader.</summary>
    [Fact]
    public async Task LoadAsync_WhenCalledOffDispatcher_DoesNotConsumeOrParseSourceAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var document = new DocumentControl();
        var reader = new StaticDocumentFormatReaderProbe(
            new DocumentReadResult([new DocumentParagraph("parsed")]));
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("text"));
        await dispatcher.InvokeAsync(
            () => document.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        // Act
        var action = async () => await document.LoadAsync(
            stream,
            reader,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _ = await action.ShouldThrowAsync<InvalidOperationException>();
        stream.Position.ShouldBe(0);
        reader.ReadCalls.ShouldBe(0);
    }

    /// <summary>Verifies an explicit non-default encoding decodes the stream instead of the UTF-8 default.</summary>
    [Fact]
    public async Task LoadAsync_WhenEncodingIsSupplied_DecodesTheStreamWithItAsync()
    {
        // Arrange
        var document = new DocumentControl();
        var text = "café";
        await using var stream = new MemoryStream(Encoding.Latin1.GetBytes(text));

        // Act
        _ = await document.LoadAsync(
            stream,
            new PlainTextDocumentReaderProbe(),
            encoding: Encoding.Latin1,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        document.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe(text);
    }

    /// <summary>Verifies the source stream stays open and readable after a successful load, matching
    /// this control's documented "the host owns the stream" contract.</summary>
    [Fact]
    public async Task LoadAsync_WhenLoadCompletes_LeavesTheSourceStreamOpenAsync()
    {
        // Arrange
        var document = new DocumentControl();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("text"));

        // Act
        _ = await document.LoadAsync(
            stream,
            new PlainTextDocumentReaderProbe(),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        stream.CanRead.ShouldBeTrue();
    }

    /// <summary>Verifies loading consumes the exact detached result tree and rejects a later reuse
    /// before another document replacement begins.</summary>
    [Fact]
    public void Load_WhenReaderReturnsPreviouslyConsumedResult_ThrowsAndPreservesExistingBlocks()
    {
        // Arrange
        var result = new DocumentReadResult([new DocumentParagraph("parsed")]);
        var reader = new StaticDocumentFormatReaderProbe(result);
        var first = new DocumentControl();
        var applied = first.Load("first", reader);
        var destination = new DocumentControl
        {
            Blocks = { new DocumentParagraph("old") }
        };

        applied.ShouldBeSameAs(result);
        first.Blocks[0].ShouldBeSameAs(result.Blocks[0]);
        result.Blocks[0].IsAttached.ShouldBeTrue();

        // Act
        var action = () => destination.Load("second", reader);

        // Assert
        _ = action.ShouldThrow<ArgumentException>();
        destination.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("old");
    }

    /// <summary>Verifies cross-root mutations made after result construction are rejected as one
    /// tree before the destination loses any existing content.</summary>
    [Fact]
    public void Load_WhenReaderResultMutatesToDuplicateControl_ThrowsAndPreservesExistingBlocks()
    {
        // Arrange
        var shared = new CheckBox("shared");
        var first = new DocumentParagraph();
        var second = new DocumentParagraph();
        var result = new DocumentReadResult([first, second]);
        first.Inlines.Add(new DocumentInlineControl(shared));
        second.Inlines.Add(new DocumentInlineControl(shared));
        var document = new DocumentControl
        {
            Blocks = { new DocumentParagraph("old") }
        };

        // Act
        var action = () => document.Load("source", new StaticDocumentFormatReaderProbe(result));

        // Assert
        _ = action.ShouldThrow<ArgumentException>();
        document.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("old");
        first.IsAttached.ShouldBeFalse();
        second.IsAttached.ShouldBeFalse();
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

    /// <summary>Verifies a synchronous load whose focused embedded control's FocusLeft handler throws
    /// while the tree is being replaced restores the exact original blocks instead of losing content.</summary>
    [Fact]
    public async Task Load_WhenFocusedControlFocusLeftThrowsDuringReplacement_RestoresOriginalBlocksAsync()
    {
        // Arrange
        var button = new Button("focused");
        var original = new DocumentBlockControl(button);
        var document = new DocumentControl { Blocks = { original } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(30, 5),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => button.Focus().ShouldBeTrue(), "focus embedded control");
        button.FocusLeft += (_, _) => throw new SimulatedCallbackFailureException("focus callback failed");

        // Act
        var action = async () => await surface.UpdateAsync(
            () => document.Load("replacement", new MarkdownDocumentReader()),
            "load replacement");

        // Assert
        _ = await action.ShouldThrowAsync<SimulatedCallbackFailureException>();
        document.Blocks.ShouldHaveSingleItem().ShouldBeSameAs(original);
        _ = button.Parent.ShouldNotBeNull();
    }

    /// <summary>Verifies a synchronous load whose focused embedded control's FocusLeft handler
    /// reenters with its own Blocks mutation trips the owned-control reentrancy guard and still
    /// restores the exact original blocks instead of leaving the reentrant item as the sole root.</summary>
    [Fact]
    public async Task Load_WhenFocusLeftReentersWithBlocksMutation_RestoresOriginalBlocksAsync()
    {
        // Arrange
        var button = new Button("focused");
        var original = new DocumentBlockControl(button);
        var document = new DocumentControl { Blocks = { original } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(30, 5),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => button.Focus().ShouldBeTrue(), "focus embedded control");
        button.FocusLeft += (_, _) => document.Blocks.Add(new DocumentParagraph("reentrant"));

        // Act
        var action = async () => await surface.UpdateAsync(
            () => document.Load("replacement", new MarkdownDocumentReader()),
            "load replacement");

        // Assert
        _ = await action.ShouldThrowAsync<InvalidOperationException>();
        document.Blocks.ShouldHaveSingleItem().ShouldBeSameAs(original);
        _ = button.Parent.ShouldNotBeNull();
    }

    /// <summary>Verifies an asynchronous stream load whose focused embedded control's FocusLeft
    /// handler throws while the tree is being replaced restores the exact original blocks.</summary>
    [Fact]
    public async Task LoadAsync_WhenFocusedControlFocusLeftThrowsDuringReplacement_RestoresOriginalBlocksAsync()
    {
        // Arrange
        var button = new Button("focused");
        var original = new DocumentBlockControl(button);
        var document = new DocumentControl { Blocks = { original } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(30, 5),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => button.Focus().ShouldBeTrue(), "focus embedded control");
        button.FocusLeft += (_, _) => throw new SimulatedCallbackFailureException("focus callback failed");
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("replacement"));

        // Act
        var loadTask = await surface.Application.Dispatcher.InvokeAsync(
            () => document.LoadAsync(
                stream,
                new PlainTextDocumentReaderProbe(),
                cancellationToken: TestContext.Current.CancellationToken).AsTask(),
            TestContext.Current.CancellationToken);
        var action = async () => await loadTask;

        // Assert
        _ = await action.ShouldThrowAsync<SimulatedCallbackFailureException>();
        document.Blocks.ShouldHaveSingleItem().ShouldBeSameAs(original);
        _ = button.Parent.ShouldNotBeNull();
    }

    /// <summary>Verifies an asynchronous stream load whose focused embedded control's FocusLeft
    /// handler reenters with its own Blocks mutation still restores the exact original blocks after
    /// the owned-control reentrancy guard rejects it.</summary>
    [Fact]
    public async Task LoadAsync_WhenFocusLeftReentersWithBlocksMutation_RestoresOriginalBlocksAsync()
    {
        // Arrange
        var button = new Button("focused");
        var original = new DocumentBlockControl(button);
        var document = new DocumentControl { Blocks = { original } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(30, 5),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => button.Focus().ShouldBeTrue(), "focus embedded control");
        button.FocusLeft += (_, _) => document.Blocks.Add(new DocumentParagraph("reentrant"));
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("replacement"));

        // Act
        var loadTask = await surface.Application.Dispatcher.InvokeAsync(
            () => document.LoadAsync(
                stream,
                new PlainTextDocumentReaderProbe(),
                cancellationToken: TestContext.Current.CancellationToken).AsTask(),
            TestContext.Current.CancellationToken);
        var action = async () => await loadTask;

        // Assert
        _ = await action.ShouldThrowAsync<InvalidOperationException>();
        document.Blocks.ShouldHaveSingleItem().ShouldBeSameAs(original);
        _ = button.Parent.ShouldNotBeNull();
    }

    /// <summary>Verifies a synchronous load still replaces content normally, consuming the exact
    /// result roots, when a focused embedded control's FocusLeft handler does not interfere.</summary>
    [Fact]
    public async Task Load_WhenFocusedControlLoseFocusWithoutInterference_ReplacesContentNormallyAsync()
    {
        // Arrange
        var button = new Button("focused");
        var document = new DocumentControl { Blocks = { new DocumentBlockControl(button) } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(30, 5),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => button.Focus().ShouldBeTrue(), "focus embedded control");

        // Act
        await surface.UpdateAsync(
            () => document.Load("replacement", new PlainTextDocumentReaderProbe()),
            "load replacement");

        // Assert
        document.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("replacement");
    }

    /// <summary>Verifies an asynchronous stream load still replaces content normally, consuming the
    /// exact result roots, when a focused embedded control's FocusLeft handler does not interfere.</summary>
    [Fact]
    public async Task LoadAsync_WhenFocusedControlLoseFocusWithoutInterference_ReplacesContentNormallyAsync()
    {
        // Arrange
        var button = new Button("focused");
        var document = new DocumentControl { Blocks = { new DocumentBlockControl(button) } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(30, 5),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => button.Focus().ShouldBeTrue(), "focus embedded control");
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("replacement"));

        // Act
        var loadTask = await surface.Application.Dispatcher.InvokeAsync(
            () => document.LoadAsync(
                stream,
                new PlainTextDocumentReaderProbe(),
                cancellationToken: TestContext.Current.CancellationToken).AsTask(),
            TestContext.Current.CancellationToken);
        _ = await loadTask;

        // Assert
        document.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
            .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("replacement");
    }

    /// <summary>Verifies an asynchronous load rejects replacement instead of silently discarding an
    /// already-committed structural mutation made by other dispatcher-scheduled work while this
    /// load's stream read was suspended.</summary>
    [Fact]
    public async Task LoadAsync_WhenBlocksChangeWhileSuspended_ThrowsInsteadOfDiscardingTheChangeAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var document = new DocumentControl();
        await dispatcher.InvokeAsync(
            () => document.Attach(dispatcher),
            TestContext.Current.CancellationToken);
        await using var stream = new PausableReadStream(Encoding.UTF8.GetBytes("new text"));

        // Act
        var loadTask = await dispatcher.InvokeAsync(
            () => document.LoadAsync(
                stream,
                new PlainTextDocumentReaderProbe(),
                cancellationToken: TestContext.Current.CancellationToken).AsTask(),
            TestContext.Current.CancellationToken);
        await stream.Entered.WaitAsync(TestContext.Current.CancellationToken);
        await dispatcher.InvokeAsync(
            () => document.Blocks.Add(new DocumentParagraph("interloper")),
            TestContext.Current.CancellationToken);
        stream.Release();
        var action = async () => await loadTask;

        // Assert
        _ = await action.ShouldThrowAsync<InvalidOperationException>();
        await dispatcher.InvokeAsync(
            () => document.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
                .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("interloper"),
            TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies two overlapping <c>LoadAsync</c> calls on the same document
    /// cannot silently clobber one another: whichever call's stream read resumes second discovers the
    /// other's already-committed replacement and throws instead of overwriting it.</summary>
    [Fact]
    public async Task LoadAsync_WhenTwoLoadsOverlap_SecondToResumeThrowsInsteadOfOverwritingAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var document = new DocumentControl();
        await dispatcher.InvokeAsync(
            () => document.Attach(dispatcher),
            TestContext.Current.CancellationToken);
        await using var streamA = new PausableReadStream(Encoding.UTF8.GetBytes("from A"));
        await using var streamB = new PausableReadStream(Encoding.UTF8.GetBytes("from B"));

        // Act
        var loadTaskA = await dispatcher.InvokeAsync(
            () => document.LoadAsync(
                streamA,
                new PlainTextDocumentReaderProbe(),
                cancellationToken: TestContext.Current.CancellationToken).AsTask(),
            TestContext.Current.CancellationToken);
        await streamA.Entered.WaitAsync(TestContext.Current.CancellationToken);

        var loadTaskB = await dispatcher.InvokeAsync(
            () => document.LoadAsync(
                streamB,
                new PlainTextDocumentReaderProbe(),
                cancellationToken: TestContext.Current.CancellationToken).AsTask(),
            TestContext.Current.CancellationToken);
        await streamB.Entered.WaitAsync(TestContext.Current.CancellationToken);

        // B resumes and commits first, while A is still suspended.
        streamB.Release();
        _ = await loadTaskB;

        // A resumes second, discovering B's already-committed replacement.
        streamA.Release();
        var action = async () => await loadTaskA;

        // Assert
        _ = await action.ShouldThrowAsync<InvalidOperationException>();
        await dispatcher.InvokeAsync(
            () => document.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines[0]
                .ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("from B"),
            TestContext.Current.CancellationToken);
    }
}
