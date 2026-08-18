// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Verifies shortcut text measurement, rendering position, and dim attributes on menu items.</summary>
public sealed class MenuItemTests
{
    /// <summary>Verifies the shortcut text renders right-aligned with dim attributes within the item bounds.</summary>
    [Fact]
    public void Render_WhenShortcutTextIsSet_DrawsDimTextAtRightEdge()
    {
        var item = new MenuItem
        {
            Text = "Save",
            ShortcutText = "Ctrl+S",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var content = item.TextControl!;
        var size = new Size(20, 1);
        new LayoutEngine().Layout(item, size);
        using Frame frame = new(size);

        item.Render(frame.Canvas);

        // "Ctrl+S" is 6 chars, right-aligned in 20-cell bounds starts at column 14.
        FrameOracle.Get(frame, new Point(14, 0)).ShouldBe("C");
        FrameOracle.Get(frame, new Point(15, 0)).ShouldBe("t");
        FrameOracle.Get(frame, new Point(19, 0)).ShouldBe("S");
        content.Bounds.Right.ShouldBe(12);
        FrameOracle.Get(frame, new Point(12, 0)).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(13, 0)).ShouldBeEmpty();
        (frame.GetCell(new Point(14, 0)).Style.Attributes & TerminalAttributes.Dim).ShouldBe(TerminalAttributes.Dim);
    }

    /// <summary>Verifies desired size includes shortcut text width plus spacing.</summary>
    [Fact]
    public void Measure_WhenShortcutTextIsSet_IncludesShortcutWidthPlusSpacing()
    {
        var item = new MenuItem { Text = "Open", ShortcutText = "Ctrl+O" };

        new LayoutEngine().Layout(item, new Size(100, 1));

        // "Open" = 4 chars, "Ctrl+O" = 6 chars + 2 spacing = 8 extra.
        // Command items have PrefixWidth = 0, so desired = 4 + 8 = 12.
        item.DesiredSize.Width.ShouldBe(12);
    }

    /// <summary>Verifies null shortcut text does not affect measurement or rendering.</summary>
    [Fact]
    public void Render_WhenShortcutTextIsNull_RendersNormally()
    {
        var item = new MenuItem { Text = "Exit" };
        var size = new Size(10, 1);
        new LayoutEngine().Layout(item, size);
        using Frame frame = new(size);

        item.Render(frame.Canvas);

        item.ShortcutText.ShouldBeNull();
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("E");
        item.DesiredSize.Width.ShouldBe(4);
    }

    /// <summary>Verifies shortcut geometry uses terminal cells for wide Unicode rather than UTF-16 length.</summary>
    [Fact]
    public void Render_WhenShortcutContainsWideUnicode_AlignsItsFinalCellToTheRightEdge()
    {
        // Arrange
        var item = new MenuItem
        {
            Text = "Open",
            ShortcutText = "界",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var size = new Size(10, 1);
        new LayoutEngine().Layout(item, size);
        using Frame frame = new(size);

        // Act
        item.Render(frame.Canvas);

        // Assert
        item.DesiredSize.Width.ShouldBe(8);
        FrameOracle.Get(frame, new Point(8, 0)).ShouldBe("界");
        frame.GetCell(new Point(9, 0)).Continuation.ShouldBeTrue();
    }

    /// <summary>Verifies desired width grows by exactly one reserved column per set affix, plus the
    /// shared theme gap, matching Button's own affix-measurement contract, on a standalone
    /// command item with a zero PrefixWidth and no shortcut.</summary>
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 4)]
    public void Measure_WhenAffixesAreSet_ReservesCellsPerAffixPlusGap(
        bool hasStart,
        bool hasEnd,
        int expectedWidth)
    {
        var item = new MenuItem
        {
            StartAffix = hasStart ? new Affix("!") : null,
            EndAffix = hasEnd ? new Affix("!") : null
        };

        new LayoutEngine().Layout(item, new Size(20, 1));

        item.DesiredSize.Width.ShouldBe(expectedWidth);
    }

    /// <summary>Verifies null-to-set and set-to-null affix assignment requires Measure.</summary>
    [Fact]
    public void StartAffix_WhenAssignedOrCleared_InvalidatesMeasure()
    {
        using var item = new MenuItem { Text = "Save" };
        item.Clear(Invalidation.All);

        item.StartAffix = new Affix("!");

        item.Pending.ShouldBe(Invalidation.All);
        item.Clear(Invalidation.All);

        item.StartAffix = null;

        item.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a same-resolved-width content or color swap invalidates rendering only, the
    /// exact grading an animated affix (a spinner swapping frames) depends on.</summary>
    [Fact]
    public void StartAffix_WhenContentOrColorChangesAtTheSameResolvedWidth_InvalidatesRenderOnly()
    {
        using var item = new MenuItem { Text = "Save", StartAffix = new Affix("|") };
        item.Clear(Invalidation.All);

        item.StartAffix = new Affix("/");

        item.Pending.ShouldBe(Invalidation.Render);
        item.Clear(Invalidation.All);

        item.StartAffix = new Affix("/", "?", SemanticColor.Warning);

        item.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a resolved-width change (one cell to two cells) invalidates Measure again,
    /// not just Render, even though both values are non-null.</summary>
    [Fact]
    public void EndAffix_WhenResolvedWidthChanges_InvalidatesMeasure()
    {
        using var item = new MenuItem { Text = "Save", EndAffix = new Affix("!") };
        item.Clear(Invalidation.All);

        // U+4E16 '世' is a wide CJK ideograph (two cells wide), unlike the one-cell '!' above.
        item.EndAffix = new Affix("世");

        item.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies reassigning the identical affix value is a no-op, matching every other
    /// SetProperty-backed member.</summary>
    [Fact]
    public void StartAffix_WhenReassignedTheSameValue_DoesNotInvalidate()
    {
        var affix = new Affix("!");
        using var item = new MenuItem { Text = "Save", StartAffix = affix };
        item.Clear(Invalidation.All);

        item.StartAffix = affix;

        item.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies both affixes reserve their own cell column pinned flush against the row's
    /// own leading and trailing edges - the start affix beside the (empty, command-item) marker
    /// column, the end affix beside the (empty, no-shortcut) trailing edge - with the caption
    /// deflated into the remaining middle box.</summary>
    [Fact]
    public void Render_WhenItemHasBothAffixes_PinsThemToTheRowEdges()
    {
        // Arrange
        var item = new MenuItem
        {
            Text = "Go",
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var size = new Size(10, 1);
        new LayoutEngine().Layout(item, size);
        using Frame frame = new(size);

        // Act
        item.Render(frame.Canvas);

        // Assert - command items have PrefixWidth 0, so the start affix sits flush at column 0.
        // The end affix sits flush at the row's own right edge (column 9): its reserved two-cell
        // zone is columns 8-9, gap then glyph, exactly mirroring how Button pins EndAffix to its
        // face's right edge rather than to wherever the caption's own text happens to end.
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe(">");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("G");
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe("o");
        FrameOracle.Get(frame, new Point(9, 0)).ShouldBe("<");
    }

    /// <summary>Verifies Padding shifts the check-kind marker glyph and the caption together
    /// against the deflated content box, matching the box-model contract - only the whole-Bounds
    /// body fill is allowed to paint across the raw border box, everything else must respect
    /// border and padding deflation.</summary>
    [Fact]
    public void Render_WhenPaddingIsSetOnACheckKindItem_ShiftsMarkerAndCaptionTogether()
    {
        // Arrange
        var item = new MenuItem
        {
            Text = "Save",
            Kind = MenuItemKind.Check,
            IsChecked = true,
            Padding = new Thickness(2, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var size = new Size(20, 1);
        new LayoutEngine().Layout(item, size);
        using Frame frame = new(size);

        // Act
        item.Render(frame.Canvas);

        // Assert - the "[✓] " marker starts at column 2 (Padding.Left), not column 0, and the
        // caption follows immediately after the reserved 4-cell marker column.
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("[");
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe("✓");
        FrameOracle.Get(frame, new Point(4, 0)).ShouldBe("]");
        FrameOracle.Get(frame, new Point(6, 0)).ShouldBe("S");
    }
}
