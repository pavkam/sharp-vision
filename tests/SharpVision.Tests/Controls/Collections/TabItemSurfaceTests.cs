// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Proves a tab page's retained content and inherited availability on a mounted surface.</summary>
public sealed class TabItemSurfaceTests
{
    /// <summary>Verifies a standalone page presents its caller-owned content without becoming a
    /// focus target of its own.</summary>
    [Fact]
    public async Task Render_WhenContentIsMounted_PresentsContentAndRemainsNonFocusableAsync()
    {
        // Arrange
        var content = new ControlText("Page body");
        var item = new TabItem
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            item,
            new Size(12, 2),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("Page body   \n            ");
        content.Parent.ShouldBeSameAs(item);
        item.IsFocusable.ShouldBeFalse();
        item.CanTabStop.ShouldBeFalse();
    }

    /// <summary>Verifies inherited visibility removes retained page content from the mounted frame
    /// while preserving ownership for a later reveal.</summary>
    [Fact]
    public async Task Visibility_WhenPageIsCollapsed_ClearsContentWithoutDetachingItAsync()
    {
        // Arrange
        var content = new ControlText("Visible");
        var item = new TabItem { Content = content };
        await using var surface = await ComponentSurface.MountAsync(
            item,
            new Size(8, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("Visible ");

        // Act
        await surface.UpdateAsync(() => item.Visibility = Visibility.Collapsed, "collapse tab item");

        // Assert
        surface.ShouldRender("        ");
        content.Parent.ShouldBeSameAs(item);
    }
}
