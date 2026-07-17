// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Proves window focus and interaction through mounted terminal surfaces.</summary>
public sealed class WindowSurfaceTests
{
    /// <summary>Verifies retained content focus, fallback activation, hover ancestry, and unavailable cleanup.</summary>
    [ComponentBehaviorEvidence(
        typeof(Window),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Input_WhenWindowHostsButtons_RoutesFallbacksAndCleansFocusAsync()
    {
        // Arrange
        var accepted = 0;
        var cancelled = 0;
        var accept = new Button { Content = new ControlText("OK"), IsDefault = true };
        var cancel = new Button { Content = new ControlText("Cancel"), IsCancel = true };
        accept.Click += (_, _) => accepted++;
        cancel.Click += (_, _) => cancelled++;
        var content = new Stack { Children = { accept, cancel } };
        var window = new Window
        {
            Title = "Dialog",
            Content = content,
            Width = Length.Cells(14),
            Height = Length.Cells(8),
            Glyphs = Glyphs.Rounded,
        };
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(18, 10),
            TestContext.Current.CancellationToken);

        // Act focus and hover nested content
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Pointer.MoveToAsync(accept);

        // Assert composition and focus ancestry
        content.Parent.ShouldBeSameAs(window);
        accept.Parent.ShouldBeSameAs(content);
        window.IsPointerOver.ShouldBeTrue();
        window.IsPointerDirectlyOver.ShouldBeFalse();
        window.IsFocused.ShouldBeFalse();
        window.IsPressed.ShouldBeFalse();
        surface.ShouldHaveFocus(accept);

        // Act fallback activation through bubbling
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        accepted.ShouldBe(1);
        cancelled.ShouldBe(1);
        window.IsPressed.ShouldBeFalse();

        // Act unavailable
        await surface.UpdateAsync(() => window.IsEnabled = false, "disable focused Window");

        // Assert cleanup
        accept.IsFocused.ShouldBeFalse();
        window.IsPressed.ShouldBeFalse();
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveCapture(null);
    }
}
