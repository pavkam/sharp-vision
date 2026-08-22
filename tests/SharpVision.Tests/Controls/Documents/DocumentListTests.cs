// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Documents;

/// <summary>Verifies <see cref="DocumentList"/>'s validated state and the marker gutter, bullet
/// rotation, derived nesting depth, and tight or loose item spacing a document projects for it.</summary>
public sealed class DocumentListTests
{
    /// <summary>Verifies a list defaults to a tight bulleted list with no items.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        // Arrange and act
        var list = new DocumentList();

        // Assert
        list.Kind.ShouldBe(DocumentListKind.Bulleted);
        list.Start.ShouldBe(1);
        list.IsLoose.ShouldBeFalse();
        list.Items.Count.ShouldBe(0);
    }

    /// <summary>Verifies numbered-list ordinals are positive, preserved after failure, and rendered.</summary>
    [Fact]
    public void Start_WhenAssigned_ValidatesAndControlsTheFirstRenderedOrdinal()
    {
        // Arrange
        var list = new DocumentList(DocumentListKind.Numbered)
        {
            Start = 4,
            Items = { new DocumentListItem("Four"), new DocumentListItem("Five") }
        };
        var document = new Document { Blocks = { list } };

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => list.Start = -1);
        list.Start.ShouldBe(4);
        using var probe = new DocumentRenderProbe(document, new Size(12, 2));
        probe.Rows().ShouldBe(["4. Four", "5. Five"]);
    }

    /// <summary>Verifies the displayed ordinal uses widened arithmetic at the end of the public
    /// integer range.</summary>
    [Fact]
    public void Render_WhenNumberingPassesIntMaxValue_DoesNotWrapToANegativeMarker()
    {
        // Arrange
        var list = new DocumentList(DocumentListKind.Numbered)
        {
            Start = int.MaxValue,
            Items = { new DocumentListItem("A"), new DocumentListItem("B") }
        };

        // Act
        using var probe = new DocumentRenderProbe(
            new Document { Blocks = { list } },
            new Size(24, 2));

        // Assert
        probe.Rows().ShouldBe(["2147483647. A", "2147483648. B"]);
    }

    /// <summary>Verifies the marker style constructor records the requested kind.</summary>
    [Fact]
    public void Constructor_WhenKindIsSupplied_RecordsIt()
    {
        // Arrange and act
        var list = new DocumentList(DocumentListKind.Numbered);

        // Assert
        list.Kind.ShouldBe(DocumentListKind.Numbered);
    }

    /// <summary>Verifies an undefined marker style is rejected on construction and on assignment.</summary>
    [Fact]
    public void Kind_WhenValueIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var list = new DocumentList(DocumentListKind.Numbered);

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new DocumentList((DocumentListKind) 7));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => list.Kind = (DocumentListKind) 7);
        list.Kind.ShouldBe(DocumentListKind.Numbered);
    }

    /// <summary>Verifies a bulleted list reserves a gutter one cell wider than its bullet and starts
    /// every item's content at the same column.</summary>
    [Fact]
    public void Render_WhenListIsBulleted_ReservesTheBulletGutterOnEveryItem()
    {
        // Arrange
        var list = new DocumentList(DocumentListKind.Bulleted)
        {
            Items = { new DocumentListItem("First"), new DocumentListItem("Second") }
        };
        var document = new Document { Blocks = { list } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 2));

        // Assert - one bullet cell plus one gap puts content at column 2.
        probe.Row(0).ShouldBe("\u2022 First");
        probe.Row(1).ShouldBe("\u2022 Second");
    }

    /// <summary>Verifies a numbered list past nine reserves a gutter measured from its widest marker,
    /// so the two-digit marker neither collides with nor shifts any item's content.</summary>
    [Fact]
    public void Render_WhenNumberedListReachesTenItems_AlignsEveryItemBehindTheWidestMarker()
    {
        // Arrange
        var list = new DocumentList(DocumentListKind.Numbered);

        for (var number = 1; number <= 10; number++)
        {
            list.Items.Add(new DocumentListItem(FormattableString.Invariant($"Item{number}")));
        }

        var document = new Document { Blocks = { list } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(14, 10));

        // Assert - "10." is three cells, so the gutter is four and every item starts at column 4.
        probe.Row(0).ShouldBe("1.  Item1");
        probe.Row(8).ShouldBe("9.  Item9");
        probe.Row(9).ShouldBe("10. Item10");

        for (var line = 0; line < 10; line++)
        {
            probe.Text(4, line).ShouldBe("I");
        }
    }

    /// <summary>Verifies bullets rotate through the first, second, and third glyph by nesting depth
    /// and start over at the fourth level.</summary>
    [Fact]
    public void Render_WhenListsNestFourDeep_RotatesBulletsByDepthModuloThree()
    {
        // Arrange
        var fourth = new DocumentList { Items = { new DocumentListItem("D") } };
        var third = new DocumentList { Items = { new DocumentListItem("C", fourth) } };
        var second = new DocumentList { Items = { new DocumentListItem("B", third) } };
        var first = new DocumentList { Items = { new DocumentListItem("A", second) } };
        var document = new Document { Blocks = { first } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(16, 4));

        // Assert
        probe.Rows().ShouldBe([
            "\u2022 A",
            "  \u25E6 B",
            "    \u25AA C",
            "      \u2022 D"
        ]);
    }

    /// <summary>Verifies nesting depth is derived from the tree at layout rather than stored, so
    /// moving a once-nested list out to the document's own blocks renders it at depth zero with the
    /// top-level bullet instead of a stale nested one.</summary>
    [Fact]
    public void Render_WhenNestedListIsMovedToTheDocumentRoot_RendersItAtDepthZero()
    {
        // Arrange
        var nested = new DocumentList { Items = { new DocumentListItem("Moved") } };
        var outerItem = new DocumentListItem("Outer", nested);
        var outer = new DocumentList { Items = { outerItem } };
        var document = new Document { Blocks = { outer } };

        using (var before = new DocumentRenderProbe(document, new Size(16, 2)))
        {
            before.Rows().ShouldBe(["\u2022 Outer", "  \u25E6 Moved"]);
        }

        // Act
        outerItem.Blocks.Remove(nested).ShouldBeTrue();
        document.Blocks.Add(nested);

        // Assert
        using var after = new DocumentRenderProbe(document, new Size(16, 3));
        after.Rows().ShouldBe(["\u2022 Outer", string.Empty, "\u2022 Moved"]);
    }

    /// <summary>Verifies a tight list packs its items with no blank line while still keeping an
    /// item's own paragraph and nested list adjacent.</summary>
    [Fact]
    public void Render_WhenListIsTight_PacksItemsWithoutBlankLines()
    {
        // Arrange
        var nested = new DocumentList { Items = { new DocumentListItem("Sub") } };
        var list = new DocumentList
        {
            Items = { new DocumentListItem("First", nested), new DocumentListItem("Second") }
        };
        var document = new Document { Blocks = { list } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(16, 4));

        // Assert
        probe.Rows().ShouldBe([
            "\u2022 First",
            "  \u25E6 Sub",
            "\u2022 Second",
            string.Empty
        ]);
    }

    /// <summary>Verifies a loose list separates its items with exactly one blank line yet still keeps
    /// each item's own blocks tight, matching CommonMark's loose-list model.</summary>
    [Fact]
    public void IsLoose_WhenTrue_SeparatesItemsButKeepsEachItemTight()
    {
        // Arrange
        var nested = new DocumentList { Items = { new DocumentListItem("Sub") } };
        var list = new DocumentList
        {
            IsLoose = true,
            Items = { new DocumentListItem("First", nested), new DocumentListItem("Second") }
        };
        var document = new Document { Blocks = { list } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(16, 5));

        // Assert
        probe.Rows().ShouldBe([
            "\u2022 First",
            "  \u25E6 Sub",
            string.Empty,
            "\u2022 Second",
            string.Empty
        ]);
    }

    /// <summary>Verifies an empty item still occupies one marked line, so removing an item's content
    /// never silently drops its marker.</summary>
    [Fact]
    public void Render_WhenItemHasNoBlocks_StillOccupiesOneMarkedLine()
    {
        // Arrange
        var list = new DocumentList
        {
            Items = { new DocumentListItem(), new DocumentListItem("Second") }
        };
        var document = new Document { Blocks = { list } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 2));

        // Assert
        probe.Rows().ShouldBe(["\u2022", "\u2022 Second"]);
        document.Extent.Width.ShouldBeGreaterThanOrEqualTo(2);
    }
}
