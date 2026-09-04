// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

// The project's own namespace, SharpVision.Document.Tests, nests textually under the SharpVision.Document
// segment, so an unqualified "Document" would otherwise resolve to that segment (as a namespace)
// rather than the Document control - this in-namespace alias, unlike a global one, takes priority
// over that enclosing-segment lookup in every position, including local-variable and return types.
using Document = Controls.Document.Document;

/// <summary>Verifies every <see cref="DocumentInline"/> node's construction and validated state, and
/// the notification <see cref="Document.LinkClicked"/> carries.</summary>
public sealed class DocumentInlineTests
{
    /// <summary>Verifies an empty text run starts with empty text.</summary>
    [Fact]
    public void Constructor_WhenTextRunIsEmpty_StartsWithEmptyText()
    {
        // Arrange and act
        var run = new DocumentTextRun();

        // Assert
        run.Text.ShouldBe(string.Empty);
    }

    /// <summary>Verifies a text run stores its markup source unparsed.</summary>
    [Fact]
    public void Constructor_WhenTextRunTakesText_StoresTheMarkupSource()
    {
        // Arrange and act
        var run = new DocumentTextRun("a <b>bold</b> word");

        // Assert
        run.Text.ShouldBe("a <b>bold</b> word");
    }

    /// <summary>Verifies text run text rejects null on construction and on assignment.</summary>
    [Fact]
    public void Text_WhenTextRunTextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var run = new DocumentTextRun("x");

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentTextRun(null!));
        _ = Should.Throw<ArgumentNullException>(() => run.Text = null!);
        run.Text.ShouldBe("x");
    }

    /// <summary>Verifies an empty link starts enabled with empty text and no target.</summary>
    [Fact]
    public void Constructor_WhenLinkIsEmpty_UsesDocumentedDefaults()
    {
        // Arrange and act
        var link = new DocumentLink();

        // Assert
        link.Text.ShouldBe(string.Empty);
        link.Target.ShouldBeNull();
        link.IsEnabled.ShouldBeTrue();
        link.Emphasis.ShouldBe(DocumentLinkEmphasis.Standard);
    }

    /// <summary>Verifies an unknown emphasis is rejected, and a genuine change is observable and
    /// idempotent for a repeated identical assignment.</summary>
    [Fact]
    public void Emphasis_WhenAssigned_ValidatesAndTracksTheCurrentValue()
    {
        // Arrange
        var link = new DocumentLink();

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => link.Emphasis = (DocumentLinkEmphasis) 99);
        link.Emphasis.ShouldBe(DocumentLinkEmphasis.Standard);

        // Act
        link.Emphasis = DocumentLinkEmphasis.Action;

        // Assert
        link.Emphasis.ShouldBe(DocumentLinkEmphasis.Action);
    }

    /// <summary>Verifies the text and target constructor records both.</summary>
    [Fact]
    public void Constructor_WhenLinkTakesTextAndTarget_RecordsBoth()
    {
        // Arrange and act
        var link = new DocumentLink("docs", "https://example.invalid/docs");

        // Assert
        link.Text.ShouldBe("docs");
        link.Target.ShouldBe("https://example.invalid/docs");
    }

    /// <summary>Verifies link construction rejects targets that cannot be represented by the
    /// terminal cell style.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("\0")]
    [InlineData("\u001b")]
    public void Constructor_WhenLinkTargetCannotBeEmitted_ThrowsArgumentException(string target) =>
        Should.Throw<ArgumentException>(() => new DocumentLink("docs", target));

    /// <summary>Verifies link text rejects null on construction and on assignment, and that the
    /// target constructor rejects a null target.</summary>
    [Fact]
    public void Text_WhenLinkArgumentIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var link = new DocumentLink("x");

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentLink(null!));
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentLink("x", null!));
        _ = Should.Throw<ArgumentNullException>(() => link.Text = null!);
        link.Text.ShouldBe("x");
    }

    /// <summary>Verifies the convenience label is total over every permitted inline node.</summary>
    [Fact]
    public void Text_WhenLinkContainsInlineControl_FlattensWithoutThrowing()
    {
        // Arrange
        var link = new DocumentLink();
        link.Inlines.Add(new DocumentTextRun("before"));
        link.Inlines.Add(new DocumentInlineControl(new CheckBox("choice")));
        link.Inlines.Add(new DocumentTextRun("after"));

        // Act
        var text = link.Text;

        // Assert
        text.ShouldBe("beforeafter");
    }

    /// <summary>Verifies assignment detaches - rather than disposes - an embedded control the
    /// current label held, leaving it reusable elsewhere.</summary>
    [Fact]
    public void Text_WhenLinkContainsInlineControl_DetachesWithoutDisposingIt()
    {
        // Arrange
        var control = new CheckBox("choice");
        var link = new DocumentLink();
        link.Inlines.Add(new DocumentInlineControl(control));

        // Act
        link.Text = "replacement";

        // Assert
        link.Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("replacement");
        control.IsDisposed.ShouldBeFalse();
        control.Parent.ShouldBeNull();

        // The control must be genuinely reusable, not merely un-disposed: wrapping it in a fresh
        // node must not throw the "already has an owner" ArgumentException a still-attached
        // control would trigger.
        _ = new DocumentInlineControl(control);
    }

    /// <summary>Verifies assignment replaces structured content even when its visible label is unchanged.</summary>
    [Fact]
    public void Text_WhenStructuredLabelHasSameVisibleText_ReplacesItWithPlainTextRun()
    {
        // Arrange
        var link = new DocumentLink();
        var strong = new DocumentStrong();
        strong.Inlines.Add(new DocumentTextRun("same"));
        link.Inlines.Add(strong);

        // Act
        link.Text = "same";

        // Assert
        link.Inlines.Count.ShouldBe(1);
        var run = link.Inlines[0].ShouldBeOfType<DocumentTextRun>();
        run.Text.ShouldBe("same");
    }

    /// <summary>Verifies hard-break flattening is deterministic on every host platform.</summary>
    [Fact]
    public void Text_WhenLabelContainsHardBreak_UsesLineFeed()
    {
        // Arrange
        var link = new DocumentLink();
        link.Inlines.Add(new DocumentTextRun("first"));
        link.Inlines.Add(new DocumentLineBreak());
        link.Inlines.Add(new DocumentTextRun("second"));

        // Act and assert
        link.Text.ShouldBe("first\nsecond");
    }

    /// <summary>Verifies attached scalar mutation checks dispatcher access before committing.</summary>
    [Fact]
    public async Task Text_WhenAttachedNodeIsMutatedOffDispatcher_ThrowsBeforeMutationAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var run = new DocumentTextRun("before");
        var document = new Document { Blocks = { new DocumentParagraph { Inlines = { run } } } };
        await dispatcher.InvokeAsync(
            () => document.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        // Act
        var action = () => run.Text = "after";

        // Assert
        _ = action.ShouldThrow<InvalidOperationException>();
        run.Text.ShouldBe("before");
    }

    /// <summary>Verifies a link's target is clearable back to null so a caller can stop emitting the
    /// terminal hyperlink without replacing the node.</summary>
    [Fact]
    public void Target_WhenClearedToNull_StopsCarryingATarget()
    {
        // Arrange
        var link = new DocumentLink("docs", "https://example.invalid/docs");
        link.Target.ShouldBe("https://example.invalid/docs");

        // Act
        link.Target = null;

        // Assert
        link.Target.ShouldBeNull();
    }

    /// <summary>Verifies assigning a target that cannot be represented by the terminal cell style
    /// fails before replacing the current valid target.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("\0")]
    [InlineData("\u001b")]
    public void Target_WhenValueCannotBeEmitted_ThrowsWithoutChangingState(string target)
    {
        // Arrange
        var link = new DocumentLink("docs", "https://example.invalid/docs");

        // Act
        void Act() => link.Target = target;

        // Assert
        _ = Should.Throw<ArgumentException>(Act);
        link.Target.ShouldBe("https://example.invalid/docs");
    }

    /// <summary>Verifies a link event notification exposes exactly the activated link.</summary>
    [Fact]
    public void Constructor_WhenLinkEventArgsTakesALink_ExposesIt()
    {
        // Arrange
        var link = new DocumentLink("docs");

        // Act
        var eventArgs = new DocumentLinkEventArgs(link);

        // Assert
        eventArgs.Link.ShouldBeSameAs(link);
    }

    /// <summary>Verifies a link event notification rejects a null link.</summary>
    [Fact]
    public void Constructor_WhenLinkEventArgsLinkIsNull_ThrowsArgumentNullException() =>
        // Arrange, act, and assert
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentLinkEventArgs(null!));

    /// <summary>Verifies a line break is a plain inline node carrying no state of its own, so two
    /// breaks are independent nodes rather than one shared marker.</summary>
    [Fact]
    public void Constructor_WhenLineBreaksAreCreated_ProducesIndependentNodes()
    {
        // Arrange and act
        var first = new DocumentLineBreak();
        var second = new DocumentLineBreak();

        // Assert
        first.ShouldNotBeSameAs(second);
    }

    /// <summary>Verifies inline-code construction and mutation preserve literal non-null text.</summary>
    [Fact]
    public void Text_WhenCodeSpanIsConstructedAndMutated_UsesLiteralValidatedText()
    {
        // Arrange and act
        var empty = new DocumentCodeSpan();
        var code = new DocumentCodeSpan("<b>x</b>");

        // Assert
        empty.Text.ShouldBe(string.Empty);
        code.Text.ShouldBe("<b>x</b>");
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentCodeSpan(null!));
        _ = Should.Throw<ArgumentNullException>(() => code.Text = null!);
        code.Text.ShouldBe("<b>x</b>");

        // Act and assert mutation
        code.Text = "next";
        code.Text.ShouldBe("next");
    }
}
