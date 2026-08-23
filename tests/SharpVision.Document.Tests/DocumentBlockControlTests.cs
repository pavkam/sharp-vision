// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

// The project's own namespace, SharpVision.Document.Tests, nests textually under the SharpVision.Document
// segment, so an unqualified "Document" would otherwise resolve to that segment (as a namespace)
// rather than the Document control - this in-namespace alias, unlike a global one, takes priority
// over that enclosing-segment lookup in every position, including local-variable and return types.
using Document = Controls.Documents.Document;

/// <summary>Verifies arbitrary retained controls participate in document flow and ownership.</summary>
public sealed class DocumentBlockControlTests
{
    /// <summary>Verifies embedded form controls participate in ordinary keyboard traversal and keep
    /// their own activation behavior.</summary>
    [Fact]
    public async Task Keyboard_WhenDocumentContainsFormControl_TabsIntoAndActivatesTheControlAsync()
    {
        // Arrange
        var before = new Button("Before");
        var choice = new CheckBox("Choice");
        var document = new Document
        {
            Blocks = { new DocumentBlockControl(choice) }
        };
        var after = new Button("After");
        var host = new Stack { Children = { before, document, after } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => before.Focus().ShouldBeTrue(), "focus before document");

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(document);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(choice);
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        choice.IsChecked.ShouldBe(true);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(after);
    }

    /// <summary>Verifies semantic links and retained form controls share one source-ordered keyboard
    /// walk instead of visiting every link before every embedded control.</summary>
    [Fact]
    public async Task Keyboard_WhenLinksSurroundAFormControl_FollowsDocumentOrderAsync()
    {
        // Arrange
        var first = new DocumentLink("First");
        var choice = new CheckBox("Choice");
        var last = new DocumentLink("Last");
        var document = new Document
        {
            Blocks =
            {
                new DocumentParagraph { Inlines = { first } },
                new DocumentBlockControl(choice),
                new DocumentParagraph { Inlines = { last } }
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Tab);
        document.ActiveLink.ShouldBeSameAs(first);
        surface.ShouldHaveFocus(document);

        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(choice);

        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(document);
        document.ActiveLink.ShouldBeSameAs(last);
    }

    /// <summary>Verifies a one-line control is one atomic token in the surrounding inline flow.</summary>
    [Fact]
    public void Layout_WhenInlineControlIsBetweenText_MountsAndPositionsTheControlInTheFlow()
    {
        // Arrange
        var choice = new CheckBox("Yes");
        var document = new Document
        {
            Blocks =
            {
                new DocumentParagraph
                {
                    Inlines =
                    {
                        new DocumentTextRun("Pick "),
                        new DocumentInlineControl(choice),
                        new DocumentTextRun(" now")
                    }
                }
            }
        };

        // Act
        using var render = new DocumentRenderProbe(document, new Size(20, 2));

        // Assert
        _ = choice.Parent.ShouldNotBeNull();
        choice.Bounds.X.ShouldBe(5);
        choice.Bounds.Y.ShouldBe(0);
        render.Row(0).ShouldContain("Pick");
        render.Row(0).ShouldContain("Yes");
        render.Row(0).ShouldEndWith("now");
    }

    /// <summary>Verifies a block control receives its natural height between text blocks.</summary>
    [Fact]
    public void Layout_WhenBlockControlIsUsed_PreservesItsNaturalSizeAndBlockFlow()
    {
        // Arrange
        var button = new Button("Submit");
        var document = new Document
        {
            Blocks =
            {
                new DocumentParagraph { Inlines = { new DocumentTextRun("Before") } },
                new DocumentBlockControl(button),
                new DocumentParagraph { Inlines = { new DocumentTextRun("After") } }
            }
        };

        // Act
        using var render = new DocumentRenderProbe(document, new Size(20, 8));

        // Assert
        _ = button.Parent.ShouldNotBeNull();
        button.Bounds.Y.ShouldBeGreaterThan(0);
        button.Bounds.Height.ShouldBe(button.DesiredSize.Height);
        render.Rows().ShouldContain("Before");
        render.Rows().ShouldContain("After");
    }

    /// <summary>Verifies one physical control cannot appear twice in the same content tree.</summary>
    [Fact]
    public void Add_WhenEmbeddedControlAlreadyAppearsInTheDocument_RejectsWithoutMutatingTheTree()
    {
        // Arrange
        var checkBox = new CheckBox("One");
        var first = new DocumentParagraph
        {
            Inlines = { new DocumentInlineControl(checkBox) }
        };
        var candidate = new DocumentParagraph
        {
            Inlines = { new DocumentInlineControl(checkBox) }
        };
        var document = new Document { Blocks = { first } };

        // Act
        var action = () => document.Blocks.Add(candidate);

        // Assert
        _ = action.ShouldThrow<ArgumentException>();
        document.Blocks.Count.ShouldBe(1);
    }

    /// <summary>Verifies duplicate physical controls are rejected while a semantic tree is still detached.</summary>
    [Fact]
    public void Add_WhenEmbeddedControlAlreadyExistsInDetachedOwnerTree_ThrowsBeforeMutation()
    {
        // Arrange
        var checkBox = new CheckBox("One");
        var paragraph = new DocumentParagraph
        {
            Inlines = { new DocumentInlineControl(checkBox) }
        };
        var candidate = new DocumentInlineControl(checkBox);

        // Act
        var action = () => paragraph.Inlines.Add(candidate);

        // Assert
        _ = action.ShouldThrow<ArgumentException>();
        paragraph.Inlines.Count.ShouldBe(1);
        candidate.IsAttached.ShouldBeFalse();
    }

    /// <summary>Verifies disposed controls are rejected at the wrapper boundary.</summary>
    [Fact]
    public void Constructor_WhenEmbeddedControlIsDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var inline = new CheckBox("inline");
        var block = new Button("block");
        inline.Dispose();
        block.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(() => new DocumentInlineControl(inline));
        _ = Should.Throw<ObjectDisposedException>(() => new DocumentBlockControl(block));
    }

    /// <summary>Verifies disposing a mounted embedded control cannot leave a stale retained child
    /// that breaks the document's next layout and render pass.</summary>
    [Fact]
    public async Task Layout_WhenMountedEmbeddedControlIsDisposed_OmitsItOnTheNextLayoutAsync()
    {
        // Arrange
        var choice = new CheckBox("Choice");
        var document = new Document
        {
            Blocks =
            {
                new DocumentBlockControl(choice),
                new DocumentParagraph("After")
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        _ = choice.Parent.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(choice.Dispose, "dispose embedded control");
        await surface.ResizeAsync(new Size(12, 5));

        // Assert
        choice.IsDisposed.ShouldBeTrue();
        choice.Parent.ShouldBeNull();
        surface.Cell(new Point(0, 2)).Text.ShouldBe("A");
    }

    /// <summary>Verifies dispatcher-attached roots are not mistaken for detached controls.</summary>
    [Fact]
    public async Task Constructor_WhenEmbeddedControlIsDispatcherAttached_ThrowsArgumentExceptionAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var inline = new CheckBox("inline");
        var block = new Button("block");
        await dispatcher.InvokeAsync(
            () =>
            {
                inline.Attach(dispatcher);
                block.Attach(dispatcher);
            },
            TestContext.Current.CancellationToken);

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => new DocumentInlineControl(inline));
        _ = Should.Throw<ArgumentException>(() => new DocumentBlockControl(block));
    }

    /// <summary>Verifies a mounted control's desired-size change rebuilds surrounding flow geometry.</summary>
    [Fact]
    public void Layout_WhenInlineControlChangesWidth_ReprojectsItsAtomicFlowToken()
    {
        // Arrange
        var checkBox = new CheckBox("A");
        var document = new Document
        {
            Blocks =
            {
                new DocumentParagraph
                {
                    Inlines =
                    {
                        new DocumentInlineControl(checkBox),
                        new DocumentTextRun(" tail")
                    }
                }
            }
        };
        var engine = new LayoutEngine();
        engine.Layout(document, new Size(20, 2));
        var previousWidth = checkBox.Bounds.Width;

        // Act
        checkBox.Text = "A much longer choice";
        engine.Layout(document, new Size(20, 2));

        // Assert
        checkBox.Bounds.Width.ShouldBeGreaterThan(previousWidth);
    }
}
