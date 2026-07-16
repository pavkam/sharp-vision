// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies the Separator showcase page and representative rendered cells.</summary>
public sealed class SeparatorPaneTests
{
    /// <summary>Verifies both orientations and custom glyphs render through the page.</summary>
    [Fact]
    public void Render_WhenSeparatorPageBuilds_ShowsHorizontalVerticalAndCustomLines()
    {
        // Arrange
        using var page = new SeparatorPane();
        var size = new Size(120, 120);
        new Engine().Layout(page, size);
        using Frame frame = new(size);
        var separators = ControlTree.FindAll<Separator>(page);
        var horizontal = separators.Single(value =>
            value.Orientation == Orientation.Horizontal &&
            value.HorizontalGlyph == new Rune('─'));
        var vertical = separators.Single(value => value.Orientation == Orientation.Vertical);
        var custom = separators.Single(value => value.HorizontalGlyph == new Rune('='));

        // Act
        page.Render(frame.Canvas);

        // Assert
        separators.Count.ShouldBeGreaterThanOrEqualTo(4);
        Grapheme(frame, new Point(horizontal.Bounds.X, horizontal.Bounds.Y)).ShouldBe("─");
        Grapheme(frame, new Point(horizontal.Bounds.Right - 1, horizontal.Bounds.Y)).ShouldBe("─");
        Grapheme(frame, new Point(vertical.Bounds.X, vertical.Bounds.Y)).ShouldBe("│");
        Grapheme(frame, new Point(vertical.Bounds.X, vertical.Bounds.Bottom - 1)).ShouldBe("│");
        Grapheme(frame, new Point(custom.Bounds.X, custom.Bounds.Y)).ShouldBe("=");
        new Screen(frame).ValidateContinuations();
    }

    private static string Grapheme(Frame frame, Point point)
    {
        var length = frame.GetGraphemeByteCount(point);
        var bytes = new byte[length];
        _ = frame.CopyGrapheme(point, bytes);
        return Encoding.UTF8.GetString(bytes);
    }
}
