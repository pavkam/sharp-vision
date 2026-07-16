// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies the NavigationView showcase page and representative retained sidebar cells.</summary>
public sealed class NavigationViewPaneTests
{
    /// <summary>Verifies basic, grouped, disabled, Unicode, footer, and overflow specimens render valid geometry.</summary>
    [Fact]
    public void Render_WhenNavigationViewPageBuilds_ShowsRepresentativeSections()
    {
        // Arrange
        using var page = new NavigationViewPane();
        var size = new Size(120, 360);
        new Engine().Layout(page, size);
        using Frame frame = new(size);
        var views = ControlTree.FindAll<NavigationView>(page);
        var basic = views.Single(value => value.Header == "MY APP");
        var grouped = views.Single(value => value.Header == "PROJECT");
        var footer = views.Single(value => value.Header == "SETTINGS");
        var overflow = views.Single(value => value.Header == "LONG");
        var home = basic.Items[0].ShouldBeOfType<NavigationViewItem>();
        var disabled = basic.Items[2].ShouldBeOfType<NavigationViewItem>();
        var group = grouped.Items[0].ShouldBeOfType<NavigationViewGroup>();
        var child = group.ItemAt(0);
        var about = footer.FooterItems[1].ShouldBeOfType<NavigationViewItem>();

        // Act
        page.Render(frame.Canvas);

        // Assert
        views.Count.ShouldBeGreaterThanOrEqualTo(4);
        Grapheme(frame, new Point(home.Bounds.X + 3, home.Bounds.Y)).ShouldBe("界");
        frame.GetCell(new Point(home.Bounds.X + 4, home.Bounds.Y)).IsContinuation.ShouldBeTrue();
        disabled.EffectiveIsEnabled.ShouldBeFalse();
        Grapheme(frame, new Point(group.Bounds.X + 1, group.Bounds.Y)).ShouldBe("▼");
        child.Bounds.X.ShouldBe(group.Bounds.X + 2);
        about.Bounds.Bottom.ShouldBe(footer.Bounds.Bottom - 1);
        overflow.Items.Count.ShouldBe(8);
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
