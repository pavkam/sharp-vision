// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies mounted GroupBox behavior: caption clipping inside the top border with the
/// corners and spacing intact, Tab traversal entering the content while the frame itself never
/// takes focus, and tiny hosts keeping the frame corners.</summary>
public sealed class GroupBoxInteractionTests
{
    /// <summary>Verifies a caption longer than the interior span is clipped inside the top border,
    /// keeping both corners and the leading gap, and regains its trailing gap once it fits.</summary>
    [Fact]
    public async Task Render_WhenCaptionExceedsTheInteriorSpan_ClipsInsideTheTopBorderAsync()
    {
        // Arrange
        var group = new GroupBox
        {
            HeaderText = "LongTitle",
            Content = new ControlText("c"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert
        var topLeft = surface.Cell(new Point(0, 0)).Text;
        var topRight = surface.Cell(new Point(7, 0)).Text;
        topLeft.ShouldNotBe(" ");
        topRight.ShouldNotBe(" ");
        char.IsLetter(topLeft[0]).ShouldBeFalse();
        char.IsLetter(topRight[0]).ShouldBeFalse();
        surface.Cell(new Point(1, 0)).Text.ShouldBe(" ");
        string.Concat(Enumerable.Range(2, 5).Select(x => surface.Cell(new Point(x, 0)).Text)).ShouldBe("LongT");
        surface.Cell(new Point(1, 1)).Text.ShouldBe("c");

        // Act widen so the whole caption fits
        await surface.ResizeAsync(new Size(14, 3));

        // Assert
        string.Concat(Enumerable.Range(2, 9).Select(x => surface.Cell(new Point(x, 0)).Text)).ShouldBe("LongTitle");
        surface.Cell(new Point(11, 0)).Text.ShouldBe(" ");
        char.IsLetter(surface.Cell(new Point(12, 0)).Text[0]).ShouldBeFalse();
    }

    /// <summary>Verifies Tab traversal enters the content directly and a press on the frame never
    /// gives the group itself focus.</summary>
    [Fact]
    public async Task Focus_WhenTabTraversesOrFrameIsPressed_NeverFocusesTheFrameAsync()
    {
        // Arrange
        var first = new CheckBox { Text = "One" };
        var second = new CheckBox { Text = "Two" };
        var group = new GroupBox
        {
            HeaderText = "G",
            Content = new Stack { Children = { first, second } },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(10, 4),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(second);
        await surface.Pointer.ClickAsync(group, new Point(0, 0));

        // Assert the frame press moved focus off the content without focusing the frame
        group.IsFocused.ShouldBeFalse();
        second.IsFocused.ShouldBeFalse();
        first.IsFocused.ShouldBeFalse();
        surface.ShouldHaveState(group, VisualState.IsPointerOver);

        // Act clicking the content focuses it
        await surface.Pointer.ClickAsync(first);
        surface.ShouldHaveFocus(first);
    }

    /// <summary>Verifies two-cell and one-cell hosts keep the frame corners without drawing any
    /// caption or content, and the full frame returns on growth.</summary>
    [Fact]
    public async Task ResizeAsync_WhenHostShrinksToTinySizes_KeepsCornersAndRecoversAsync()
    {
        // Arrange
        var group = new GroupBox
        {
            HeaderText = "Title",
            Content = new ControlText("c"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(9, 3),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(2, 0)).Text.ShouldBe("T");
        surface.Cell(new Point(1, 1)).Text.ShouldBe("c");

        // Act two cells
        await surface.ResizeAsync(new Size(2, 2));

        // Assert
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                var text = surface.Cell(new Point(x, y)).Text;
                text.ShouldNotBe(" ");
                char.IsLetter(text[0]).ShouldBeFalse();
            }
        }

        // Act one cell
        await surface.ResizeAsync(new Size(1, 1));

        // Assert
        surface.Cell(default).Text.ShouldNotBe("c");
        surface.Cell(default).Text.ShouldNotBe("T");

        // Act
        await surface.ResizeAsync(new Size(9, 3));

        // Assert
        surface.Cell(new Point(2, 0)).Text.ShouldBe("T");
        surface.Cell(new Point(1, 1)).Text.ShouldBe("c");
    }
}
