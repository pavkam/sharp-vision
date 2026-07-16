// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies shortcut text measurement, rendering position, and dim attributes on menu items.</summary>
public sealed class MenuItemShortcutTests
{
    /// <summary>Verifies the shortcut text renders right-aligned with dim attributes within the item bounds.</summary>
    [Fact]
    public void Render_WhenShortcutTextIsSet_DrawsDimTextAtRightEdge()
    {
        var item = new MenuItem
        {
            Content = new ControlText("Save"),
            ShortcutText = "Ctrl+S",
        };
        var size = new Size(20, 1);
        new Engine().Layout(item, size);
        using Frame frame = new(size);

        item.Render(frame.Canvas);

        // "Ctrl+S" is 6 chars, so it should start at column 20 - 6 = 14.
        FrameOracle.Get(frame, new Point(14, 0)).ShouldBe("C");
        FrameOracle.Get(frame, new Point(15, 0)).ShouldBe("t");
        FrameOracle.Get(frame, new Point(19, 0)).ShouldBe("S");
        (frame.GetCell(new Point(14, 0)).Style.Attributes & Attributes.Dim).ShouldBe(Attributes.Dim);
    }

    /// <summary>Verifies desired size includes shortcut text width plus spacing.</summary>
    [Fact]
    public void Measure_WhenShortcutTextIsSet_IncludesShortcutWidthPlusSpacing()
    {
        var item = new MenuItem
        {
            Content = new ControlText("Open"),
            ShortcutText = "Ctrl+O",
        };

        new Engine().Layout(item, new Size(100, 1));

        // "Open" = 4 chars, "Ctrl+O" = 6 chars + 2 spacing = 8 extra.
        // Command items have PrefixWidth = 0, so desired = 4 + 8 = 12.
        item.DesiredSize.Width.ShouldBe(12);
    }

    /// <summary>Verifies null shortcut text does not affect measurement or rendering.</summary>
    [Fact]
    public void Render_WhenShortcutTextIsNull_RendersNormally()
    {
        var item = new MenuItem
        {
            Content = new ControlText("Exit"),
        };
        var size = new Size(10, 1);
        new Engine().Layout(item, size);
        using Frame frame = new(size);

        item.Render(frame.Canvas);

        item.ShortcutText.ShouldBeNull();
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("E");
        item.DesiredSize.Width.ShouldBe(4);
    }
}
