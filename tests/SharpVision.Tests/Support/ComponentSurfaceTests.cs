// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Verifies reusable mounted-surface mutation, state, and keyboard contracts.</summary>
public sealed class ComponentSurfaceTests
{
    /// <summary>Verifies dispatcher-affine mutation settles layout, rendering, and terminal output.</summary>
    [Fact]
    public async Task UpdateAsync_WhenMountedControlChanges_SettlesTheRenderedMutationAsync()
    {
        // Arrange
        var text = new ControlText("Before")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(6, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("Before");

        // Act
        await surface.UpdateAsync(() => text.Content = "After", "change Text content");

        // Assert
        surface.ShouldRender("After");
    }

    /// <summary>Verifies state assertions accept descendants owned by the mounted component.</summary>
    [Fact]
    public async Task ShouldHaveState_WhenOwnedDescendantIsPassed_ObservesItsStateAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Content = new ControlText("Choice") };
        var stack = new Stack { Children = { checkBox } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        checkBox.IsFocused.ShouldBeTrue();
        surface.ShouldHaveState(checkBox, State.Focused);
    }

    /// <summary>Verifies a complete Kitty character action drives Pressable press and release behavior.</summary>
    [Fact]
    public async Task Keyboard_WhenSpaceCompletes_EmitsPressAndReleaseAsync()
    {
        // Arrange
        ActivationCause? cause = null;
        var checkBox = new CheckBox { Content = new ControlText("Choice") };
        checkBox.Checked += (_, eventArgs) => cause = eventArgs.Cause;
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        // Assert
        checkBox.IsChecked.ShouldBe(true);
        cause.ShouldBe(ActivationCause.Keyboard);
        surface.ShouldHaveState(checkBox, State.Focused);
    }

    /// <summary>Verifies terminal resize commits new geometry before the settled frame is exposed.</summary>
    [Fact]
    public async Task ResizeAsync_WhenSurfaceChanges_ReflowsMountedTextAsync()
    {
        // Arrange
        var text = new ControlText("abcd")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Overflow = Overflow.WrapAnywhere,
        };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(2, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
            ab
            cd
            """);

        // Act
        await surface.ResizeAsync(new Size(4, 1));

        // Assert
        text.Bounds.ShouldBe(new Rect(0, 0, 4, 1));
        surface.ShouldRender("abcd");
    }
}
