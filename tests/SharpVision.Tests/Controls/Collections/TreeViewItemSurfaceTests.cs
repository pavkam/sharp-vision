// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies TreeViewItem's start/end affix rendering - pinning, overflow order, and
/// caption confinement - through a mounted terminal surface, mirroring ButtonSurfaceTests'
/// coverage of the identical affix seam.</summary>
public sealed class TreeViewItemSurfaceTests
{
    /// <summary>Verifies both affixes render strictly inboard of the disclosure/check glyphs and
    /// outboard of the header text, measured from the row's own post-chrome column rather than
    /// its raw <c>Bounds.X</c>.</summary>
    [Fact]
    public async Task Render_WhenItemHasBothAffixes_PinsThemInboardOfCheckAndOutboardOfHeaderAsync()
    {
        // Arrange
        var item = new TreeViewItem
        {
            Header = "Root",
            IsCheckable = true,
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        item.Children.Add(new TreeViewItem { Header = "Child" });
        var tree = CreateTree(24);
        tree.Items.Add(item);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(24, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Assert - disclosure, gap, and the three-cell bracket mark match the row's ordinary
        // chrome; the post-chrome column is where affix pinning must be measured from, not
        // ContentBounds.X.
        var afterCheckX = item.ContentBounds.X + 1 + 1 + item.ActualCheckMark.Width;
        surface.Cell(new Point(item.ContentBounds.X, 0)).Text.ShouldBe("▼");
        surface.Cell(new Point(item.ContentBounds.X + 2, 0)).Text.ShouldBe("[");
        surface.Cell(new Point(item.ContentBounds.X + 4, 0)).Text.ShouldBe("]");

        // Assert - the start affix sits immediately at the post-chrome column, and the header,
        // past the affix's own gap, never reaches into it: its own leading space lands two cells
        // past the affix rather than one.
        surface.Cell(new Point(afterCheckX, 0)).Text.ShouldBe(">");
        surface.Cell(new Point(afterCheckX + 2, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(afterCheckX + 3, 0)).Text.ShouldBe("R");
        surface.Cell(new Point(afterCheckX + 4, 0)).Text.ShouldBe("o");
        surface.Cell(new Point(afterCheckX + 5, 0)).Text.ShouldBe("o");
        surface.Cell(new Point(afterCheckX + 6, 0)).Text.ShouldBe("t");

        // Assert - the end affix sits flush against the row's own live right edge.
        surface.Cell(new Point(item.ContentBounds.Right - 1, 0)).Text.ShouldBe("<");
    }

    /// <summary>Verifies a header long enough to want the whole remaining width still cannot
    /// draw over a set end affix - the header's own leading space and text stay confined to the
    /// middle box <c>DeflateForAffixes</c> produces.</summary>
    [Fact]
    public async Task Render_WhenHeaderOverflowsAvailableSpace_NeverEncroachesOnTheEndAffixAsync()
    {
        // Arrange
        var item = new TreeViewItem
        {
            Header = "Supercalifragilisticexpialidocious",
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = Length.Cells(12),
            EndAffix = new Affix("$")
        };
        var tree = CreateTree(20);
        tree.Items.Add(item);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(20, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Assert - the end affix still owns the row's last cell despite an oversized caption.
        surface.Cell(new Point(item.ContentBounds.Right - 1, 0)).Text.ShouldBe("$");

        // Assert - the header's own leading space and first characters still render, confined to
        // the middle box.
        surface.Cell(new Point(item.ContentBounds.X + 1, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(item.ContentBounds.X + 2, 0)).Text.ShouldBe("S");
    }

    /// <summary>Verifies the end affix drops whole - never a partial cluster - before the start
    /// affix does when the row has room for only one, matching the priority
    /// <c>RenderAffixes</c> documents: caption first, then the end affix, then the start affix.</summary>
    [Fact]
    public async Task Render_WhenRowHasRoomForOnlyOneAffix_DropsTheEndAffixFirstAsync()
    {
        // Arrange
        var item = new TreeViewItem
        {
            Header = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = Length.Cells(2),
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        var tree = CreateTree(10);
        tree.Items.Add(item);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(10, 3),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Assert - one leaf-row leading cell, then exactly one drawable cell for affixes; the
        // start affix claims it and the end affix drops whole.
        surface.Cell(new Point(item.ContentBounds.X, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(item.ContentBounds.X + 1, 0)).Text.ShouldBe(">");
    }

    /// <summary>Verifies a nested, checkable row with an affix and a nested, checkable row
    /// without one render cell-exact side by side, proving the affix seam does not disturb the
    /// existing indent/disclosure/check math for either row.</summary>
    [Fact]
    public async Task Render_WhenNestedCheckableRowsMixAffixes_RendersEachRowExactlyAsync()
    {
        // Arrange
        var parent = new TreeViewItem { Header = "Root", IsCheckable = true, StartAffix = new Affix("*") };
        var child = new TreeViewItem { Header = "Child", IsCheckable = true };
        parent.Children.Add(child);
        var tree = CreateTree(24);
        tree.Items.Add(parent);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(24, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Assert - row 0 (depth 0, checkable, one affix, no gap reserved after it since EndAffix
        // is null): disclosure, gap, three-cell mark, then the affix immediately past the mark
        // and the header past the affix's own gap.
        var x0 = parent.ContentBounds.X;
        surface.Cell(new Point(x0, 0)).Text.ShouldBe("▼");
        surface.Cell(new Point(x0 + 2, 0)).Text.ShouldBe("[");
        surface.Cell(new Point(x0 + 4, 0)).Text.ShouldBe("]");
        surface.Cell(new Point(x0 + 5, 0)).Text.ShouldBe("*");
        surface.Cell(new Point(x0 + 8, 0)).Text.ShouldBe("R");

        // Assert - row 1 (depth 1, checkable, no affixes at all): two indent cells shift every
        // following column two cells right of the parent's, and the header sits exactly where
        // the pre-existing indent/disclosure/check math alone would put it.
        surface.Cell(new Point(x0 + 2, 1)).Text.ShouldBe(" ");
        surface.Cell(new Point(x0 + 4, 1)).Text.ShouldBe("[");
        surface.Cell(new Point(x0 + 6, 1)).Text.ShouldBe("]");
        surface.Cell(new Point(x0 + 8, 1)).Text.ShouldBe("C");
    }

    /// <summary>Verifies a row whose indent exceeds the 256-cell stack-allocation bound still
    /// positions its leading space and header exactly where the ordinary, well within-bound
    /// indent does - proving the heap-array fallback for pathologically deep nesting fills
    /// identically to the stackalloc path used below that bound.</summary>
    [Fact]
    public async Task Render_WhenIndentExceedsStackAllocLimit_StillPositionsHeaderExactlyAsync()
    {
        // Arrange
        const int indent = 300;
        var root = new TreeViewItem { Header = "Root" };
        var child = new TreeViewItem { Header = "Child" };
        root.Children.Add(child);
        var tree = CreateTree(indent + 20);
        tree.Indent = indent;
        tree.Items.Add(root);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(indent + 20, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Assert - depth 1's one indent level (300 cells, past the 256-cell stack-allocation
        // bound) shifts the leaf's own leading cell, the header's leading space, and its first
        // character exactly as the pre-existing indent/leading-space math alone would put them.
        var x0 = root.ContentBounds.X;
        surface.Cell(new Point(x0 + indent, 1)).Text.ShouldBe(" ");
        surface.Cell(new Point(x0 + indent + 1, 1)).Text.ShouldBe(" ");
        surface.Cell(new Point(x0 + indent + 2, 1)).Text.ShouldBe("C");
    }

    /// <summary>Verifies extreme tree and synthetic-row indentation saturates one shared geometry
    /// calculation and renders only the visible canvas cells.</summary>
    [Fact]
    public void Render_WhenIndentIsExtreme_ClipsWithoutOverflowOrUnboundedAllocation()
    {
        var root = new TreeViewItem { Header = "Root" };
        var child = new TreeViewItem { Header = "Child", Depth = 1 };
        root.Children.Add(child);
        var tree = CreateTree(3);
        tree.Indent = int.MaxValue;
        tree.Items.Add(root);
        child.Bounds = new Rect(0, 0, int.MaxValue, 1);
        var status = new TreeViewStatusRow(root) { Depth = 1, Bounds = child.Bounds };
        using var frame = new Frame(new Size(3, 1));

        child.Render(frame.Canvas);
        status.Render(frame.Canvas);

        TreeViewItem.ResolveIndentCells(1, int.MaxValue).ShouldBe(int.MaxValue);
        TreeViewItem.ResolveIndentedX(1, int.MaxValue, int.MaxValue).ShouldBe(int.MaxValue);
        frame.GetCell(new Point(2, 0)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>Verifies custom disclosure glyphs authored on the owning tree's
    /// <see cref="TreeViewStyle"/> render in both expanded and collapsed states, instead of the
    /// code-owned <c>ControlGlyphs.Disclosure</c> default - the same custom-glyph coverage
    /// ExpanderSurfaceTests exercises for the sibling disclosure control.</summary>
    [Fact]
    public async Task Render_WhenCustomDisclosureGlyphsAreSet_UsesOverrideGlyphsAsync()
    {
        // Arrange
        var item = new TreeViewItem { Header = "Root" };
        item.Children.Add(new TreeViewItem { Header = "Child" });
        var tree = CreateTree(24);

        // ContainerStyle.Default carries its own light all-side border; override it to
        // ControlStyle.NoBorder here so the glyph override doesn't also reintroduce a border row
        // on top of this file's other tests, which all rely on TestThemes.BorderlessContainer to
        // remove it.
        tree.Style = TreeViewStyle.Default with
        {
            Border = ControlStyle.NoBorder,
            ExpandedGlyph = new Rune('-'),
            CollapsedGlyph = new Rune('+')
        };
        tree.Items.Add(item);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            tree,
            new Size(24, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Assert - the default IsExpanded state draws the themed expanded glyph, not "▼".
        surface.Cell(new Point(item.ContentBounds.X, 0)).Text.ShouldBe("-");

        // Act
        await surface.UpdateAsync(() => item.IsExpanded = false, "collapse with custom glyph");

        // Assert - collapsing draws the themed collapsed glyph, not "▶".
        surface.Cell(new Point(item.ContentBounds.X, 0)).Text.ShouldBe("+");
    }

    private static TreeView CreateTree(int width) => new()
    {
        Width = Length.Cells(width),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };
}
