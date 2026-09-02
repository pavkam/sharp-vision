// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Tests.Support;

/// <summary>Verifies a multi-click on the trailing cell of a wide glyph sitting at the viewport's
/// right edge addresses that glyph. A single-click caret would land on the boundary after the
/// glyph, which is out of view, and the press's own caret reveal would scroll the editor by one
/// column before the release is hit tested - so a release resolved against the scrolled content
/// would address the neighbour that scrolled in under the pointer.</summary>
public sealed class TextInputEdgeGlyphMultiClickTests
{
    private static TextInput Editor(string text, int width) => new()
    {
        Text = text,
        ScrollBars = ScrollBars.None,
        Width = Length.Cells(width),
        Height = Length.Cells(1)
    };

    /// <summary>Verifies double-clicking the trailing cell of a wide glyph at the viewport's right
    /// edge selects that glyph rather than the word the first click's caret reveal scrolled in
    /// underneath the pointer.</summary>
    [Fact]
    public async Task Pointer_WhenWideGlyphAtRightEdgeIsDoubleClickedOnTrailingCell_SelectsThatGlyphAsync()
    {
        // Arrange - "a 界" fills the four-cell viewport; " cd" is scrolled off to the right
        var input = Editor("a 界 cd", 4);
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(4, 1),
            time,
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        surface.ShouldRender("a 界");

        // Act - the first click places the caret after the glyph, which reveals the neighbour
        await surface.Pointer.ClickAsync(input, new Point(3, 0));
        input.CaretIndex.ShouldBe(3);
        input.HorizontalOffset.ShouldBe(1);
        await surface.Pointer.ClickAsync(input, new Point(3, 0));

        // Assert
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe("界");
    }

    /// <summary>Verifies a second click on the same cell after the text changed between the two
    /// clicks addresses the glyph now under the pointer, so the remembered first-press glyph is
    /// only reused while the content it was resolved against is unchanged.</summary>
    [Fact]
    public async Task Pointer_WhenTextChangesBetweenClicks_SelectsGlyphUnderSecondClickAsync()
    {
        // Arrange - every cell is in view, so no reveal scroll is involved
        var input = Editor("ab 界-c", 8);
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(8, 1),
            time,
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(input, new Point(3, 0));

        // Act - the text is replaced so cell 3 now shows a different word
        await surface.UpdateAsync(() => input.Text = "xy word", "replace the text between clicks");
        await surface.Pointer.ClickAsync(input, new Point(3, 0));

        // Assert
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe("word");
    }

    /// <summary>Verifies triple-clicking the same edge cell selects the whole line, proving the
    /// press-time glyph endpoint feeds every multi-click command and not only word selection.</summary>
    [Fact]
    public async Task Pointer_WhenWideGlyphAtRightEdgeIsTripleClickedOnTrailingCell_SelectsLineAsync()
    {
        // Arrange
        var input = Editor("a 界 cd", 4);
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(4, 1),
            time,
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(input, new Point(3, 0));
        await surface.Pointer.ClickAsync(input, new Point(3, 0));
        await surface.Pointer.ClickAsync(input, new Point(3, 0));

        // Assert
        (await surface.ReadAsync(() => input.SelectedText)).ShouldBe("a 界 cd");
    }

}
