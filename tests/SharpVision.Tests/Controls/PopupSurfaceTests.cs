// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Proves popup promotion and dismissal through mounted terminal surfaces.</summary>
public sealed class PopupSurfaceTests
{
    /// <summary>Verifies open popup composition, descendant hover, focus transfer, and Escape closure.</summary>
    [ComponentBehaviorEvidence(
        typeof(Popup),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Transient |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Input_WhenPopupOpensAndEscapes_TracksTransientCompositionAsync()
    {
        // Arrange
        var anchor = new Button
        {
            Content = new ControlText("Anchor"),
            Width = Length.Cells(8),
            Height = Length.Cells(3),
        };
        var action = new Button
        {
            Content = new ControlText("Action"),
            Width = Length.Cells(8),
            Height = Length.Cells(3),
        };
        var popup = new Popup { Anchor = anchor, Content = action };
        var root = new Overlay { Children = { anchor, popup } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(18, 7),
            TestContext.Current.CancellationToken);

        // Act open and hover composed content
        await surface.UpdateAsync(() => popup.IsOpen = true, "open Popup");
        await surface.Pointer.MoveToAsync(action);

        // Assert transient ancestry and focus transfer
        popup.IsOpen.ShouldBeTrue();
        popup.SurfaceBounds.Width.ShouldBeGreaterThan(0);
        popup.IsPointerOver.ShouldBeTrue();
        popup.IsPointerDirectlyOver.ShouldBeFalse();
        popup.IsFocused.ShouldBeFalse();
        popup.IsPressed.ShouldBeFalse();
        action.Parent.ShouldBeSameAs(popup);
        surface.ShouldHaveFocus(action);

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        popup.IsOpen.ShouldBeFalse();
        popup.SurfaceBounds.ShouldBe(default);
        action.Bounds.ShouldBe(default);
    }
}
