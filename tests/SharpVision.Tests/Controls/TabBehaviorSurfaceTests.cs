// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Proves tab selection and navigation through mounted terminal surfaces.</summary>
public sealed class TabBehaviorSurfaceTests
{
    /// <summary>Verifies keyboard and pointer header behavior commit selection only on completed input.</summary>
    [ComponentBehaviorEvidence(
        typeof(TabControl),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.Directional |
        ComponentBehavior.PressRelease |
        ComponentBehavior.Activation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Composition)]
    [ComponentBehaviorEvidence(
        typeof(TabItem),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Input_WhenHeadersNavigateAndPress_CommitsReleasedSelectionAndCleanupAsync()
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
        await using var surface = await ComponentSurface.MountAsync(
            tabs,
            new Size(30, 4),
            TestContext.Current.CancellationToken);

        // Act and assert keyboard navigation
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(tabs);
        await surface.Keyboard.PressAsync(Code.Right);
        tabs.SelectedIndex.ShouldBe(1);
        await surface.Keyboard.PressAsync(Code.Left);
        tabs.SelectedIndex.ShouldBe(0);

        // Act held pointer over the second direct-rendered header
        await surface.Pointer.MoveToAsync(tabs, new Point(11, 0));
        await surface.Pointer.PressAsync();

        // Assert held state does not activate
        tabs.SelectedIndex.ShouldBe(0);
        tabs.IsPressed.ShouldBeTrue();
        surface.ShouldHaveCapture(tabs);

        // Act release
        await surface.Pointer.ReleaseAsync();

        // Assert released selection and retained page composition
        tabs.SelectedIndex.ShouldBe(1);
        tabs.IsPressed.ShouldBeFalse();
        _ = second.Parent.ShouldNotBeNull();
        second.Content.ShouldNotBeNull().Parent.ShouldBeSameAs(second);
        second.IsFocused.ShouldBeFalse();
        second.IsPressed.ShouldBeFalse();
        await surface.Pointer.MoveToAsync(second);
        second.IsPointerOver.ShouldBeTrue();

        // Act unavailable while another header press is held
        await surface.Pointer.MoveToAsync(tabs, new Point(2, 0));
        await surface.Pointer.PressAsync();
        await surface.UpdateAsync(() => tabs.IsEnabled = false, "disable held TabControl");

        // Assert cleanup preserves the completed selection
        tabs.SelectedIndex.ShouldBe(1);
        tabs.IsPressed.ShouldBeFalse();
        tabs.IsFocused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
    }
}
