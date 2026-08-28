// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

using SharpVision.Documents.Markdown;

// The project's own namespace, SharpVision.Document.Tests, nests textually under the SharpVision.Document
// segment, so an unqualified "Document" would otherwise resolve to that segment (as a namespace)
// rather than the Document control - this in-namespace alias, unlike a global one, takes priority
// over that enclosing-segment lookup in every position, including local-variable and return types.
using Document = Controls.Documents.Document;

/// <summary>Verifies Markdown-created form controls retain ordinary mounted behavior.</summary>
public sealed class MarkdownDocumentReaderSurfaceTests
{
    /// <summary>Verifies parser-created inline form controls contribute semantic text before the
    /// document has ever been measured or mounted.</summary>
    [Fact]
    public void GetSelectableTextSnapshot_WhenTaskListHasNotBeenLaidOut_ReturnsTaskText()
    {
        // Arrange
        var document = new Document();
        _ = document.Load(
            "- [ ] task",
            new MarkdownDocumentReader(new MarkdownOptions
            {
                Extensions = MarkdownExtension.TaskLists
            }));

        // Act
        var snapshot = document.GetSelectableTextSnapshot();

        // Assert
        snapshot.Text.ShouldBe("-  task");
    }

    /// <summary>Verifies parsed radio items become one mutually exclusive retained group.</summary>
    [Fact]
    public void Load_WhenRadioListIsParsed_MountsOneInteractiveExclusiveGroup()
    {
        // Arrange
        var document = new Document();
        _ = document.Load(
            "- ( ) Alpha\n- (x) Beta",
            new MarkdownDocumentReader(new MarkdownOptions
            {
                Extensions = MarkdownExtension.RadioLists
            }));
        var first = RadioAt(document, 0);
        var second = RadioAt(document, 1);
        using var render = new DocumentRenderProbe(document, new Size(24, 2));

        // Act
        first.PerformClick();

        // Assert
        _ = first.Parent.ShouldNotBeNull();
        first.IsChecked.ShouldBeTrue();
        second.IsChecked.ShouldBeFalse();
    }

    /// <summary>Verifies Markdown table parsing reaches the rich table projection without losing
    /// inline formatting or link identity.</summary>
    [Fact]
    public void Load_WhenTableContainsRichMarkdown_PreservesFormattingAndHyperlinkCells()
    {
        // Arrange
        var document = new Document();
        _ = document.Load(
            "| Value |\n| --- |\n| **Bold** [docs](https://example.test) |",
            new MarkdownDocumentReader(new MarkdownOptions
            {
                Extensions = MarkdownExtension.Tables
            }));

        // Act
        using var render = new DocumentRenderProbe(document, new Size(20, 2));

        // Assert
        render.Rows().ShouldBe(["| Value     |", "| Bold docs |"]);
        (render.Cell(2, 1).Style.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.Bold);
        render.Cell(7, 1).Style.Hyperlink.ShouldBe("https://example.test");
    }

    /// <summary>Verifies CommonMark's valid empty link destination remains a semantic link while
    /// rendering without an invalid OSC 8 target.</summary>
    [Fact]
    public void Load_WhenInlineLinkDestinationIsEmpty_RendersWithoutAHyperlinkTarget()
    {
        // Arrange
        var document = new Document();
        _ = document.Load("[x]()", new MarkdownDocumentReader());
        var link = document.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>()
            .Inlines.ShouldHaveSingleItem().ShouldBeOfType<DocumentLink>();

        // Act
        using var render = new DocumentRenderProbe(document, new Size(4, 1));

        // Assert
        link.Target.ShouldBeNull();
        render.Rows().ShouldBe(["x"]);
        render.Cell(0, 0).Style.Hyperlink.ShouldBeNull();
    }

    /// <summary>Verifies generated radio names are scoped per mounted document even when both
    /// readers assign the same source-list ordinal.</summary>
    [Fact]
    public async Task Load_WhenSiblingDocumentsContainRadioLists_KeepsTheirGroupsIndependentAsync()
    {
        // Arrange
        var options = new MarkdownOptions { Extensions = MarkdownExtension.RadioLists };
        var firstDocument = new Document();
        var secondDocument = new Document();
        _ = firstDocument.Load("- (x) A1\n- ( ) A2", new MarkdownDocumentReader(options));
        _ = secondDocument.Load("- (x) B1\n- ( ) B2", new MarkdownDocumentReader(options));
        var first = RadioAt(firstDocument, item: 0);
        var second = RadioAt(secondDocument, item: 1);
        await using var surface = await ComponentSurface.MountAsync(
            new Stack { Children = { firstDocument, secondDocument } },
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(second.PerformClick, "select second document radio");

        // Assert
        first.IsChecked.ShouldBeTrue();
        second.IsChecked.ShouldBeTrue();
        first.GroupName.ShouldNotBe(second.GroupName);
    }

    /// <summary>Verifies ordinary application radios are never mistaken for parser-generated radio
    /// groups merely because their caller-authored name shares a textual prefix.</summary>
    [Fact]
    public async Task Layout_WhenCallerRadioNameStartsWithMarkdownPrefix_PreservesTheNameAsync()
    {
        // Arrange
        var radio = new RadioButton("Choice") { GroupName = "markdown-radio-custom" };
        var document = new Document { Blocks = { new DocumentBlockControl(radio) } };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(20, 2),
            TestContext.Current.CancellationToken);

        // Assert
        radio.GroupName.ShouldBe("markdown-radio-custom");
    }

    private static RadioButton RadioAt(Document document, int item) =>
        document.Blocks[0].ShouldBeOfType<DocumentList>().Items[item].Blocks[0]
            .ShouldBeOfType<DocumentParagraph>().Inlines[0].ShouldBeOfType<DocumentInlineControl>()
            .Control.ShouldBeOfType<RadioButton>();
}
