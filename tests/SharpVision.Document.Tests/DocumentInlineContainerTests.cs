// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

// The project's own namespace, SharpVision.Document.Tests, nests textually under the SharpVision.Document
// segment, so an unqualified "Document" would otherwise resolve to that segment (as a namespace)
// rather than the Document control - this in-namespace alias, unlike a global one, takes priority
// over that enclosing-segment lookup in every position, including local-variable and return types.
using Document = Controls.Documents.Document;

/// <summary>Verifies semantic inline nodes participate in shared flow and final cell styling.</summary>
public sealed class DocumentInlineContainerTests
{
    /// <summary>Verifies nested strong, emphasis, and strikethrough nodes compose their attributes.</summary>
    [Fact]
    public void Render_WhenSemanticInlinesAreNested_ComposesAttributesOnTheVisibleText()
    {
        // Arrange
        var strike = new DocumentStrikethrough { Inlines = { new DocumentTextRun("styled") } };
        var emphasis = new DocumentEmphasis { Inlines = { strike } };
        var strong = new DocumentStrong { Inlines = { emphasis } };
        var document = new Document
        {
            Blocks = { new DocumentParagraph { Inlines = { strong } } }
        };

        // Act
        using var render = new DocumentRenderProbe(document, new Size(12, 1));

        // Assert
        render.Row(0).ShouldBe("styled");
        var attributes = render.Cell(0, 0).Style.Attributes;
        (attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.Bold);
        (attributes & TerminalAttributes.Italic).ShouldBe(TerminalAttributes.Italic);
        (attributes & TerminalAttributes.Strike).ShouldBe(TerminalAttributes.Strike);
    }

    /// <summary>Verifies a semantic inline group remains part of one greedy wrapping flow.</summary>
    [Fact]
    public void Layout_WhenSemanticBoundaryFallsBetweenWords_WrapsAcrossTheBoundary()
    {
        // Arrange
        var paragraph = new DocumentParagraph
        {
            Inlines =
            {
                new DocumentTextRun("one "),
                new DocumentStrong { Inlines = { new DocumentTextRun("two three") } }
            }
        };
        var document = new Document { Blocks = { paragraph } };

        // Act
        using var render = new DocumentRenderProbe(document, new Size(7, 2));

        // Assert
        render.Rows().ShouldBe(["one two", "three"]);
    }

    /// <summary>Verifies links may own semantic inline content while retaining one activation region.</summary>
    [Fact]
    public void Render_WhenLinkContainsSemanticInlines_PreservesTextStylingAndOneLinkIdentity()
    {
        // Arrange
        var link = new DocumentLink
        {
            Target = "https://example.invalid",
            Inlines =
            {
                new DocumentTextRun("read "),
                new DocumentStrong { Inlines = { new DocumentTextRun("this") } }
            }
        };
        var document = new Document
        {
            Blocks = { new DocumentParagraph { Inlines = { link } } }
        };

        // Act
        using var render = new DocumentRenderProbe(document, new Size(12, 1));

        // Assert
        render.Row(0).ShouldBe("read this");
        (render.Cell(5, 0).Style.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.Bold);
        render.Cell(0, 0).Style.Hyperlink.ShouldBe("https://example.invalid");
        document.ActiveLink = link;
        document.ActiveLink.ShouldBeSameAs(link);
    }

    /// <summary>Verifies code spans are literal and soft breaks participate in ordinary whitespace flow.</summary>
    [Fact]
    public void Render_WhenCodeAndSoftBreakAreUsed_PreservesLiteralTextAndFlow()
    {
        // Arrange
        var document = new Document
        {
            Blocks =
            {
                new DocumentParagraph
                {
                    Inlines =
                    {
                        new DocumentCodeSpan("<b>x</b>"),
                        new DocumentSoftBreak(),
                        new DocumentTextRun("tail")
                    }
                }
            }
        };

        // Act
        using var render = new DocumentRenderProbe(document, new Size(14, 1));

        // Assert
        render.Row(0).ShouldBe("<b>x</b> tail");
    }

    /// <summary>Verifies the semantic model rejects nested activation targets before link indices
    /// can become ambiguous during layout.</summary>
    [Fact]
    public void Add_WhenLinkWouldContainAnotherLink_ThrowsBeforeMutation()
    {
        // Arrange
        var outer = new DocumentLink("outer");
        var inner = new DocumentLink("inner");

        // Act
        var action = () => outer.Inlines.Add(inner);

        // Assert
        _ = action.ShouldThrow<ArgumentException>();
        outer.Text.ShouldBe("outer");
        inner.IsAttached.ShouldBeFalse();
    }
}
