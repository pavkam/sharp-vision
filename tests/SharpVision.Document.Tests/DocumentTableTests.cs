// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

// The project's own namespace, SharpVision.Document.Tests, nests textually under the SharpVision.Document
// segment, so an unqualified "Document" would otherwise resolve to that segment (as a namespace)
// rather than the Document control - this in-namespace alias, unlike a global one, takes priority
// over that enclosing-segment lookup in every position, including local-variable and return types.
using Document = Controls.Document.Document;

/// <summary>Verifies table column measurement, alignment, and header presentation.</summary>
public sealed class DocumentTableTests
{
    /// <summary>Verifies the semantic table hierarchy owns empty collections and documented defaults.</summary>
    [Fact]
    public void Constructor_WhenTableHierarchyIsEmpty_UsesDocumentedDefaults()
    {
        // Arrange and act
        var table = new DocumentTable();
        var row = new DocumentTableRow();
        var cell = new DocumentTableCell();

        // Assert
        table.Rows.Count.ShouldBe(0);
        row.IsHeader.ShouldBeFalse();
        row.Cells.Count.ShouldBe(0);
        cell.Alignment.ShouldBe(DocumentTableCellAlignment.Left);
        cell.Inlines.Count.ShouldBe(0);
    }

    /// <summary>Verifies cell construction and alignment validate without corrupting prior state.</summary>
    [Fact]
    public void Alignment_WhenCellIsConstructedAndMutated_ValidatesAndPreservesContent()
    {
        // Arrange
        var cell = new DocumentTableCell("value");
        cell.Alignment.ShouldBe(DocumentTableCellAlignment.Left);

        // Act
        cell.Alignment = DocumentTableCellAlignment.Center;

        // Assert
        cell.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("value");
        cell.Alignment.ShouldBe(DocumentTableCellAlignment.Center);
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => cell.Alignment = (DocumentTableCellAlignment) 99);
        _ = Should.Throw<ArgumentNullException>(static () => new DocumentTableCell(null!));
        cell.Alignment.ShouldBe(DocumentTableCellAlignment.Center);
    }

    /// <summary>Verifies concrete row and cell collections detach nodes for immediate reuse.</summary>
    [Fact]
    public void Remove_WhenRowsAndCellsAreOwned_DetachesThemForReuse()
    {
        // Arrange
        var cell = new DocumentTableCell("value");
        var row = new DocumentTableRow { Cells = { cell } };
        var table = new DocumentTable { Rows = { row } };

        // Act
        row.Cells.Remove(cell).ShouldBeTrue();
        table.Rows.Remove(row).ShouldBeTrue();
        var replacementRow = new DocumentTableRow { Cells = { cell } };
        var replacement = new DocumentTable { Rows = { row, replacementRow } };

        // Assert
        replacement.Rows.Count.ShouldBe(2);
        replacementRow.Cells[0].ShouldBeSameAs(cell);
    }

    /// <summary>Verifies all rows share widths and cells honor their declared alignment.</summary>
    [Fact]
    public void Render_WhenTableHasAlignedColumns_UsesSharedMeasuredWidths()
    {
        // Arrange
        var header = new DocumentTableRow
        {
            IsHeader = true,
            Cells =
            {
                new DocumentTableCell("Name"),
                new DocumentTableCell("N") { Alignment = DocumentTableCellAlignment.Right }
            }
        };
        var body = new DocumentTableRow
        {
            Cells =
            {
                new DocumentTableCell("A"),
                new DocumentTableCell("12") { Alignment = DocumentTableCellAlignment.Right }
            }
        };
        var document = new Document
        {
            Blocks = { new DocumentTable { Rows = { header, body } } }
        };

        // Act
        using var render = new DocumentRenderProbe(document, new Size(20, 2));

        // Assert
        render.Rows().ShouldBe(["| Name |  N |", "| A    | 12 |"]);
        (render.Cell(0, 0).Style.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.Bold);
    }

    /// <summary>Verifies table projection preserves semantic inline attributes and hyperlinks
    /// instead of flattening a cell to inert text.</summary>
    [Fact]
    public void Render_WhenTableCellContainsRichInlines_PreservesStylingAndLinkIdentity()
    {
        // Arrange
        var strong = new DocumentStrong { Inlines = { new DocumentTextRun("Bold") } };
        var link = new DocumentLink("docs", "https://example.test/docs");
        var cell = new DocumentTableCell { Inlines = { strong, new DocumentTextRun(" "), link } };
        var document = new Document
        {
            Blocks = { new DocumentTable { Rows = { new DocumentTableRow { Cells = { cell } } } } }
        };

        // Act
        using var render = new DocumentRenderProbe(document, new Size(20, 1));

        // Assert
        render.Row(0).ShouldBe("| Bold docs |");
        (render.Cell(2, 0).Style.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.Bold);
        render.Cell(7, 0).Style.Hyperlink.ShouldBe("https://example.test/docs");
        document.ActiveLink = link;
        document.ActiveLink.ShouldBeSameAs(link);
    }

    /// <summary>Verifies a retained inline control in a table contributes to column measurement and
    /// receives the projected cell position.</summary>
    [Fact]
    public void Layout_WhenTableCellContainsInlineControl_ArrangesItInsideTheCell()
    {
        // Arrange
        var checkBox = new CheckBox("Ready");
        var cell = new DocumentTableCell
        {
            Inlines = { new DocumentInlineControl(checkBox) }
        };
        var document = new Document
        {
            Blocks = { new DocumentTable { Rows = { new DocumentTableRow { Cells = { cell } } } } }
        };

        // Act
        using var render = new DocumentRenderProbe(document, new Size(20, 1));

        // Assert
        checkBox.Bounds.X.ShouldBe(2);
        checkBox.Bounds.Y.ShouldBe(0);
        checkBox.Bounds.Width.ShouldBeGreaterThan(0);
        render.Row(0).ShouldContain("Ready");
    }

    /// <summary>Verifies an extreme measured column followed by empty right-aligned content uses
    /// saturated geometry without materializing impossible padding.</summary>
    [Fact]
    public void Layout_WhenTableColumnHasExtremeControlWidth_SaturatesPaddingAndExtent()
    {
        // Arrange
        var checkBox = new CheckBox("wide") { Width = Length.Cells(int.MaxValue) };
        var table = new DocumentTable
        {
            Rows =
            {
                new DocumentTableRow
                {
                    Cells = { new DocumentTableCell { Inlines = { new DocumentInlineControl(checkBox) } } }
                },
                new DocumentTableRow
                {
                    Cells = { new DocumentTableCell { Alignment = DocumentTableCellAlignment.Right } }
                }
            }
        };
        var document = new Document { Blocks = { table } };

        // Act
        using var render = new DocumentRenderProbe(document, new Size(12, 2));

        // Assert
        document.Extent.Width.ShouldBe(int.MaxValue);
        checkBox.Bounds.Width.ShouldBe(int.MaxValue);
        render.Row(1).ShouldStartWith("|");
    }

    /// <summary>Verifies a row can mix an extreme-width embedded control with a following cell's
    /// ordinary content without overflowing table layout arithmetic during rendering.</summary>
    [Fact]
    public void Layout_WhenRowMixesExtremeControlCellWithFollowingCell_DoesNotThrow()
    {
        // Arrange
        var checkBox = new CheckBox("wide") { Width = Length.Cells(int.MaxValue) };
        var row = new DocumentTableRow
        {
            Cells =
            {
                new DocumentTableCell { Inlines = { new DocumentInlineControl(checkBox) } },
                new DocumentTableCell("next")
            }
        };
        var document = new Document { Blocks = { new DocumentTable { Rows = { row } } } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 1));

        // Assert
        probe.Row(0).ShouldStartWith("|");
    }

    /// <summary>Verifies a cell mixing an extreme-width embedded control with trailing text content
    /// does not overflow table layout arithmetic during rendering.</summary>
    [Fact]
    public void Layout_WhenCellMixesExtremeControlWithTrailingText_DoesNotThrow()
    {
        // Arrange
        var checkBox = new CheckBox("wide") { Width = Length.Cells(int.MaxValue) };
        var row = new DocumentTableRow
        {
            Cells =
            {
                new DocumentTableCell
                {
                    Inlines = { new DocumentInlineControl(checkBox), new DocumentTextRun("tail") }
                }
            }
        };
        var document = new Document { Blocks = { new DocumentTable { Rows = { row } } } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 1));

        // Assert
        probe.Row(0).ShouldStartWith("|");
    }

    /// <summary>Verifies literal line endings inside a cell normalize to a space in the semantic
    /// stream while the table's genuine row boundary remains one LF.</summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\r\n")]
    public void SelectionMap_WhenCellTextContainsLineBreak_PreservesOnlyRowSeparator(string lineBreak)
    {
        var table = new DocumentTable
        {
            Rows =
            {
                new DocumentTableRow
                {
                    Cells = { new DocumentTableCell($"foo{lineBreak}bar"), new DocumentTableCell("baz") }
                },
                new DocumentTableRow
                {
                    Cells = { new DocumentTableCell("next"), new DocumentTableCell("row") }
                }
            }
        };
        var document = new Document { Blocks = { table } };

        using var probe = new DocumentRenderProbe(document, new Size(30, 6));

        document.SelectionMap.Text.ShouldBe("foo bar\tbaz\nnext\trow");
    }

    /// <summary>Verifies an explicit hard-break inline inside a cell cannot impersonate a table row boundary.</summary>
    [Fact]
    public void SelectionMap_WhenCellContainsLineBreakInline_PreservesOnlyRowSeparator()
    {
        var first = new DocumentTableCell
        {
            Inlines = { new DocumentTextRun("foo"), new DocumentLineBreak(), new DocumentTextRun("bar") }
        };
        var table = new DocumentTable
        {
            Rows =
            {
                new DocumentTableRow { Cells = { first, new DocumentTableCell("baz") } },
                new DocumentTableRow { Cells = { new DocumentTableCell("next"), new DocumentTableCell("row") } }
            }
        };
        var document = new Document { Blocks = { table } };

        using var probe = new DocumentRenderProbe(document, new Size(30, 6));

        document.SelectionMap.Text.ShouldBe("foo bar\tbaz\nnext\trow");
    }
}
