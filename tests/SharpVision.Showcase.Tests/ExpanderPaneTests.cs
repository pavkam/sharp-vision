// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies the Expander showcase page and representative rendered cells.</summary>
public sealed class ExpanderPaneTests
{
    /// <summary>Verifies expanded, collapsed, nested, disabled, Unicode, and replacement specimens render valid headers.</summary>
    [Fact]
    public void Render_WhenExpanderPageBuilds_ShowsRepresentativeHeadersAndContent()
    {
        // Arrange
        using var page = new ExpanderPane();
        var size = new Size(120, 180);
        new Engine().Layout(page, size);
        using Frame frame = new(size);
        var expanders = ControlTree.FindAll<Expander>(page);
        var expanded = expanders.Single(value => value.Header == "Details");
        var collapsed = expanders.Single(value => value.Header == "Advanced");
        var disabled = expanders.Single(value => value.Header == "Unavailable");
        var unicode = expanders.Single(value => value.Header == "界 Tools");
        var replacement = expanders.Single(value => value.Header == "Replacement");
        var collapsedContent = collapsed.Content.ShouldNotBeNull();
        var replacementContent = replacement.Content.ShouldBeOfType<ControlText>();

        // Act
        page.Render(frame.Canvas);

        // Assert
        expanders.Count.ShouldBeGreaterThanOrEqualTo(7);
        Grapheme(frame, new Point(expanded.Bounds.X, expanded.Bounds.Y)).ShouldBe("▼");
        Grapheme(frame, new Point(collapsed.Bounds.X, collapsed.Bounds.Y)).ShouldBe("▶");
        collapsedContent.Bounds.ShouldBe(default);
        disabled.HeaderPart.EffectiveIsEnabled.ShouldBeFalse();
        Grapheme(frame, new Point(unicode.Bounds.X + 2, unicode.Bounds.Y)).ShouldBe("界");
        frame.GetCell(new Point(unicode.Bounds.X + 3, unicode.Bounds.Y)).IsContinuation.ShouldBeTrue();
        replacementContent.Content.ShouldStartWith("The replacement");
        replacementContent.Bounds.ShouldBe(default);
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
