// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Documents;

using SharpVision.Documents.Markdown;

/// <summary>Verifies Markdown-created form controls retain ordinary mounted behavior.</summary>
public sealed class MarkdownDocumentReaderSurfaceTests
{
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
        var list = document.Blocks[0].ShouldBeOfType<DocumentList>();
        var first = list.Items[0].Blocks[0].ShouldBeOfType<DocumentBlockControl>().Control
            .ShouldBeOfType<RadioButton>();
        var second = list.Items[1].Blocks[0].ShouldBeOfType<DocumentBlockControl>().Control
            .ShouldBeOfType<RadioButton>();
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
            .ShouldBeOfType<DocumentBlockControl>().Control.ShouldBeOfType<RadioButton>();
}
