// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Proves navigation-separator rendering and hit-test exclusion on a mounted surface.</summary>
public sealed class NavigationViewSeparatorSurfaceTests
{
    /// <summary>Verifies a styled separator fills its row while pointer targeting passes through it.</summary>
    [Fact]
    public async Task Render_WhenStyledAndMounted_DrawsRuleWithoutBecomingPointerTargetAsync()
    {
        // Arrange
        var separator = new NavigationViewSeparator
        {
            Style = NavigationViewSeparatorStyle.Default with { Glyph = new Rune('=') }
        };
        var view = new NavigationView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        view.Items.Add(separator);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(8, 2),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("========\n        ");
        separator.Bounds.Height.ShouldBe(1);
        view.HitTest(new Point(separator.Bounds.X, separator.Bounds.Y)).ShouldNotBeSameAs(separator);
    }
}
