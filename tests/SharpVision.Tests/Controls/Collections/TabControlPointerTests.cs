// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies retained tab headers through mounted pointer input.</summary>
public sealed class TabControlPointerTests
{
    /// <summary>Verifies selected state belongs to the active header instead of recoloring its page.</summary>
    [Fact]
    public async Task Render_WhenMounted_StylesOnlySelectedHeaderAsync()
    {
        // Arrange
        var first = new TabItem { HeaderText = "General", Content = new ControlText("General body") };
        var second = new TabItem { HeaderText = "Advanced", Content = new ControlText("Advanced body") };
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        tabs.Items.Add(first);
        tabs.Items.Add(second);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(30, 4),
            TestContext.Current.CancellationToken);
        var theme = tabs.Theme.ShouldNotBeNull();
        var selection = ThemeColorHelper.SelectionBackground(theme);
        var containingBackground = ReferenceColors.Get(0);

        // Assert
        tabs.GetResolvedAppearance(tabs.GetAppearanceState()).BackgroundMode.ShouldBe(BackgroundMode.Opaque);
        first.GetResolvedAppearance(first.GetAppearanceState()).BackgroundMode.ShouldBe(BackgroundMode.Opaque);

        // The page's Text content never paints its own background; the opaque page fill below
        // comes from the TabItem itself, verified against the rendered page cell further down.
        first.Content.ShouldNotBeNull().GetResolvedAppearance(first.Content.GetAppearanceState()).BackgroundMode
            .ShouldBe(BackgroundMode.Transparent);
        surface.Cell(new Point(1, 0)).Style.Background.IsRgb.ShouldBeTrue();
        surface.Cell(new Point(11, 0)).Style.Background.IsRgb.ShouldBeTrue();
        surface.Cell(new Point(0, 2)).Style.Background.ShouldBe(ReferenceColors.Get(0));
        surface.Cell(new Point(0, 2)).Text.ShouldBe("G");
    }

    /// <summary>Verifies nested pointer input hovers and selects only the targeted retained header.</summary>
    [Fact]
    public async Task Pointer_WhenNestedHeaderIsHoveredAndClicked_UsesHeaderStateAndSelectsAsync()
    {
        // Arrange
        var first = new TabItem { HeaderText = "General", Content = new ControlText("General body") };
        var second = new TabItem { HeaderText = "Advanced", Content = new ControlText("Advanced body") };
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        var host = new Dock
        {
            Padding = new Thickness(3, 2),
            Children = { tabs },
        };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(36, 8),
            TestContext.Current.CancellationToken);
        var theme = tabs.Theme.ShouldNotBeNull();
        var selection = ThemeColorHelper.SelectionBackground(theme);
        var hoveredForeground = TerminalPalette.Project(ThemeColorHelper.HoveredForeground(theme), ColorDepth.Basic16);
        var inactiveForeground = ThemeColorHelper.InactiveForeground(theme);
        var containingBackground = ReferenceColors.Get(0);

        // Act: hovering the page must not hover every header through the focus-owning parent.
        await surface.Pointer.MoveToAsync(first.Content.ShouldNotBeNull());

        // Assert
        surface.Cell(new Point(tabs.Bounds.X + 11, tabs.Bounds.Y)).Style.Foreground.ShouldBe(inactiveForeground);
        surface.Cell(new Point(tabs.Bounds.X, tabs.Bounds.Y + 2)).Style.Background.ShouldBe(ReferenceColors.Get(0));

        // Act: hover and click the Advanced header at a non-zero parent offset.
        await surface.Pointer.MoveToAsync(tabs, new Point(11, 0));

        // Assert hover remains local to that header.
        surface.Cell(new Point(tabs.Bounds.X + 11, tabs.Bounds.Y)).Style.Foreground.ShouldBe(hoveredForeground);
        surface.Cell(new Point(tabs.Bounds.X + 1, tabs.Bounds.Y)).Style.Background.IsRgb.ShouldBeTrue();

        // Act
        await surface.Pointer.ClickAsync(tabs, new Point(11, 0));

        // Assert
        tabs.SelectedIndex.ShouldBe(1);
        surface.Cell(new Point(tabs.Bounds.X + 11, tabs.Bounds.Y)).Style.Background.ShouldBe(selection);
        surface.Cell(new Point(tabs.Bounds.X + 1, tabs.Bounds.Y)).Style.Background.ShouldBe(containingBackground);
        surface.Cell(new Point(tabs.Bounds.X, tabs.Bounds.Y + 2)).Text.ShouldBe("A");
        surface.Cell(new Point(tabs.Bounds.X, tabs.Bounds.Y + 2)).Style.Background.ShouldBe(ReferenceColors.Get(0));
    }

    /// <summary>Verifies primary release inside a rendered header commits that page selection.</summary>
    [Fact]
    public async Task Pointer_WhenHeaderIsClicked_SelectsReleasedPageAsync()
    {
        // Arrange
        var first = new TabItem { HeaderText = "General", Content = new ControlText("General body") };
        var second = new TabItem { HeaderText = "Advanced", Content = new ControlText("Advanced body") };
        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
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
