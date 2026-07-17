// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Proves menu entries and navigation through mounted terminal surfaces.</summary>
public sealed class MenuSurfaceTests
{
    /// <summary>Verifies root navigation, item press/release, separator exclusion, and cleanup.</summary>
    [ComponentBehaviorEvidence(
        typeof(Menu),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.Directional |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Composition)]
    [ComponentBehaviorEvidence(
        typeof(MenuItem),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressRelease |
        ComponentBehavior.Activation |
        ComponentBehavior.UnavailableCleanup)]
    [ComponentBehaviorEvidence(
        typeof(MenuSeparator),
        ComponentBehavior.Mounted |
        ComponentBehavior.HoverExcluded |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task Input_WhenMenuNavigatesAndInvokes_UsesOwnedFacesWithoutFocusTheftAsync()
    {
        // Arrange
        var first = new MenuItem { Content = new ControlText("Open") };
        var separator = new MenuSeparator();
        var second = new MenuItem { Content = new ControlText("Save") };
        var menu = new Menu { Orientation = Orientation.Vertical, Spacing = 0 };
        menu.Items.Add(first);
        menu.Items.Add(separator);
        menu.Items.Add(second);
        var invoked = 0;
        menu.ItemInvoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act and assert excluded separator hover
        await surface.Pointer.MoveToAsync(separator);
        separator.IsPointerOver.ShouldBeFalse();
        separator.IsFocused.ShouldBeFalse();

        // Act root focus and directional selection
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        menu.SelectedIndex.ShouldBe(2);
        surface.ShouldHaveFocus(menu);

        // Act held item face
        await surface.Pointer.MoveToAsync(second);
        await surface.Pointer.PressAsync();
        second.IsPressed.ShouldBeTrue();
        second.IsFocused.ShouldBeFalse();
        menu.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(second);

        // Act release
        await surface.Pointer.ReleaseAsync();

        // Assert invocation
        invoked.ShouldBe(1);
        second.IsPressed.ShouldBeFalse();
        surface.ShouldHaveFocus(menu);

        // Act unavailable while held
        await surface.Pointer.PressAsync();
        await surface.UpdateAsync(() => second.IsEnabled = false, "disable held MenuItem");
        await surface.UpdateAsync(() => menu.IsEnabled = false, "disable focused Menu");

        // Assert cleanup
        second.IsPressed.ShouldBeFalse();
        menu.IsFocused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
    }
}
