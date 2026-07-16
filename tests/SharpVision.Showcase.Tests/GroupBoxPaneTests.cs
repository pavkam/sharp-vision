// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies the GroupBox showcase page and representative rendered cells.</summary>
public sealed class GroupBoxPaneTests
{
    /// <summary>Verifies titled, empty, Unicode, styled, ASCII, and tiny specimens render valid frames.</summary>
    [Fact]
    public void Render_WhenGroupBoxPageBuilds_ShowsRepresentativeFrames()
    {
        // Arrange
        using var page = new GroupBoxPane();
        var size = new Size(120, 160);
        new Engine().Layout(page, size);
        using Frame frame = new(size);
        var groups = ControlTree.FindAll<GroupBox>(page);
        var empty = groups.Single(value => value.Header.Length == 0);
        var unicode = groups.Single(value => value.Header == "界 Tools");
        var ascii = groups.Single(value => value.Glyphs == Glyphs.Ascii);
        var tiny = groups.Single(value => value.Width == Length.Cells(5));

        // Act
        page.Render(frame.Canvas);

        // Assert
        groups.Count.ShouldBeGreaterThanOrEqualTo(6);
        Grapheme(frame, new Point(empty.Bounds.X, empty.Bounds.Y)).ShouldBe("╭");
        Grapheme(frame, new Point(empty.Bounds.X + 1, empty.Bounds.Y)).ShouldBe("─");
        Grapheme(frame, new Point(unicode.Bounds.X + 2, unicode.Bounds.Y)).ShouldBe("界");
        frame.GetCell(new Point(unicode.Bounds.X + 3, unicode.Bounds.Y)).IsContinuation.ShouldBeTrue();
        Grapheme(frame, new Point(ascii.Bounds.X, ascii.Bounds.Y)).ShouldBe("+");
        Grapheme(frame, new Point(tiny.Bounds.Right - 1, tiny.Bounds.Y)).ShouldBe("╮");
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
