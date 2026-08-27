// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Proves navigation-group rendering and pointer interaction on a mounted surface.</summary>
public sealed class NavigationViewGroupSurfaceTests
{
    /// <summary>Verifies pointer activation toggles retained descendants while keyboard focus stays
    /// on the owning navigation view.</summary>
    [Fact]
    public async Task Pointer_WhenHeaderIsActivated_TogglesChildrenAndFocusesOwnerAsync()
    {
        // Arrange
        var child = new NavigationViewItem { Text = "Child" };
        var group = new NavigationViewGroup { Header = "Group" };
        group.Items.Add(child);
        var view = new NavigationView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        view.Items.Add(group);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender(" ▼ Group    \n   · Child  \n            ");

        // Act
        await surface.Pointer.ClickAsync(group, new Point(1, 0));

        // Assert
        group.IsExpanded.ShouldBeFalse();
        child.EffectiveIsVisible.ShouldBeFalse();
        surface.ShouldHaveFocus(view);
        surface.ShouldRender(" ▶ Group    \n            \n            ");
    }
}
