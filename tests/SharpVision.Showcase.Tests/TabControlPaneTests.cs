// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies the TabControl showcase page and representative retained header cells.</summary>
public sealed class TabControlPaneTests
{
    /// <summary>Verifies basic, disabled, Unicode, overflow, and replacement specimens render valid pages.</summary>
    [Fact]
    public void Render_WhenTabControlPageBuilds_ShowsRepresentativeHeadersAndContent()
    {
        // Arrange
        using var page = new TabControlPane();
        var size = new Size(120, 320);
        new Engine().Layout(page, size);
        using Frame frame = new(size);
        var controls = ControlTree.FindAll<TabControl>(page);
        var unicode = controls.Single(value => value.Items.Any(item => item.Header == "界 Tools"));
        var overflow = controls.Single(value => value.Width == Length.Cells(14));
        var disabled = controls.Single(value => value.Items.Any(item => item.Header == "Unavailable"));
        var replacement = controls.Single(value => value.Items.Any(item => item.Header == "Replacement"));
        var unicodeHeader = unicode.Items.Single(item => item.Header == "界 Tools").HeaderPart;

        // Act
        page.Render(frame.Canvas);

        // Assert
        controls.Count.ShouldBeGreaterThanOrEqualTo(5);
        Grapheme(frame, new Point(unicodeHeader.Bounds.X + 1, unicodeHeader.Bounds.Y)).ShouldBe("界");
        frame.GetCell(new Point(unicodeHeader.Bounds.X + 2, unicodeHeader.Bounds.Y)).IsContinuation.ShouldBeTrue();
        Grapheme(frame, new Point(unicode.Bounds.X, unicode.Bounds.Y + 1)).ShouldBe("─");
        overflow.SelectedIndex.ShouldBe(2);
        overflow.Bounds.Width.ShouldBe(14);
        overflow.Items[2].HeaderPart.Bounds.Width.ShouldBe(4);
        overflow.HeaderOffset.ShouldBeGreaterThan(0);
        disabled.Items[1].EffectiveIsEnabled.ShouldBeFalse();
        replacement.Items[0].Content.ShouldBeOfType<ControlText>().Content.ShouldStartWith("Caller content");
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
