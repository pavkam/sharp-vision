// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Proves ComboBox and its transient list through mounted terminal surfaces.</summary>
public sealed class ComboBoxSurfaceTests
{
    /// <summary>Verifies field press, popup navigation, release activation, and unavailable cleanup.</summary>
    [ComponentBehaviorEvidence(
        typeof(ComboBox),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.Directional |
        ComponentBehavior.PressRelease |
        ComponentBehavior.Activation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Transient |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Input_WhenDropDownNavigates_CommitsReleasedChoiceAndCleanupAsync()
    {
        // Arrange
        var combo = new ComboBox
        {
            Items = ["One", "Two", "Three"],
            Width = Length.Cells(10),
            DropDownHeight = 3,
        };
        await using var surface = await ComponentSurface.MountAsync(
            combo,
            new Size(12, 6),
            TestContext.Current.CancellationToken);

        // Act focus, hover, and hold the field
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Pointer.MoveToAsync(combo);
        await surface.Pointer.PressAsync();

        // Assert held state before opening
        combo.IsOpen.ShouldBeFalse();
        combo.IsPressed.ShouldBeTrue();
        surface.ShouldHaveFocus(combo);
        surface.ShouldHaveCapture(combo);

        // Act release, navigate, and commit
        await surface.Pointer.ReleaseAsync();
        combo.IsOpen.ShouldBeTrue();
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert committed transient selection
        combo.SelectedIndex.ShouldBe(1);
        combo.IsOpen.ShouldBeFalse();
        combo.IsPressed.ShouldBeFalse();
        surface.ShouldHaveFocus(combo);
        surface.ShouldRender("Two      ▼");

        // Act unavailable while another field press is held
        await surface.Pointer.MoveToAsync(combo);
        await surface.Pointer.PressAsync();
        await surface.UpdateAsync(() => combo.IsEnabled = false, "disable held ComboBox");

        // Assert cleanup
        combo.IsPressed.ShouldBeFalse();
        combo.IsFocused.ShouldBeFalse();
        combo.IsOpen.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
    }
}
