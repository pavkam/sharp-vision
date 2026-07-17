// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies direct-rendered tab headers through mounted pointer input.</summary>
public sealed class TabControlPointerTests
{
    /// <summary>Verifies primary release inside a rendered header commits that page selection.</summary>
    [Fact]
    public async Task Pointer_WhenHeaderIsClicked_SelectsReleasedPageAsync()
    {
        // Arrange
        var first = new TabItem { Header = "General", Content = new ControlText("General body") };
        var second = new TabItem { Header = "Advanced", Content = new ControlText("Advanced body") };
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        var changes = 0;
        tabs.SelectionChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(30, 4),
            TestContext.Current.CancellationToken);

        // Act press the Advanced header, which starts after " General │".
        await surface.Pointer.MoveToAsync(tabs, new Point(11, 0));
        await surface.Pointer.PressAsync();

        // Assert press does not commit selection before release.
        tabs.SelectedIndex.ShouldBe(0);

        // Act release on the same header.
        await surface.Pointer.ReleaseAsync();

        // Assert
        tabs.SelectedIndex.ShouldBe(1);
        changes.ShouldBe(1);
        second.Content.ShouldNotBeNull().Bounds.Y.ShouldBe(2);
        second.Content.ShouldNotBeNull().Bounds.Width.ShouldBeGreaterThan(0);
        surface.Cell(new Point(11, 0)).Text.ShouldBe("A");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("A");
    }
}
