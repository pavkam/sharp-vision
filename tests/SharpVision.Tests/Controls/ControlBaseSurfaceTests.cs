// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies convenience CLR events on the base ControlBase class through a mounted
/// ComponentSurface, exercising ControlBase's routed-event plumbing.</summary>
public sealed class ControlBaseSurfaceTests
{
    /// <summary>Verifies PointerPressed fires when a primary press arrives.</summary>
    [Fact]
    public async Task PointerPressed_WhenPrimaryButtonPressed_FiresAsync()
    {
        // Arrange
        var fired = 0;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Text = "OK"
        };
        button.PointerPressed += (_, _) => fired++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();

        // Assert
        fired.ShouldBeGreaterThanOrEqualTo(1);
    }

    /// <summary>Verifies PointerReleased fires on release.</summary>
    [Fact]
    public async Task PointerReleased_WhenPrimaryButtonReleased_FiresAsync()
    {
        // Arrange
        var fired = 0;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Text = "OK"
        };
        button.PointerReleased += (_, _) => fired++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        fired.ShouldBeGreaterThanOrEqualTo(1);
    }

    /// <summary>Verifies PointerMoved fires on move.</summary>
    [Fact]
    public async Task PointerMoved_WhenPointerMoves_FiresAsync()
    {
        // Arrange
        var fired = 0;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Text = "OK"
        };
        button.PointerMoved += (_, _) => fired++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(button);

        // Assert
        fired.ShouldBeGreaterThanOrEqualTo(1);
    }

    /// <summary>Verifies KeyDown fires on key press.</summary>
    [Fact]
    public async Task KeyDown_WhenKeyIsPressed_FiresAsync()
    {
        // Arrange
        var fired = 0;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Focusable = true,
            Text = "OK"
        };
        button.KeyDown += (_, _) => fired++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act — focus and press a key
        await surface.Pointer.ClickAsync(button);
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert
        fired.ShouldBeGreaterThanOrEqualTo(1);
    }

    /// <summary>Verifies KeyUp fires on key release.</summary>
    [Fact]
    public async Task KeyUp_WhenKeyIsReleased_FiresAsync()
    {
        // Arrange
        var fired = 0;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Focusable = true,
            Text = "OK"
        };
        button.KeyUp += (_, _) => fired++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act — focus and complete a character (which produces press+release)
        await surface.Pointer.ClickAsync(button);
        await surface.Keyboard.CompleteCharacterAsync(new Rune('x'));

        // Assert
        fired.ShouldBeGreaterThanOrEqualTo(1);
    }

    /// <summary>Verifies BoundsChanged fires after layout.</summary>
    [Fact]
    public async Task BoundsChanged_WhenControlIsArranged_FiresAsync()
    {
        // Arrange
        var fired = 0;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Text = "OK"
        };
        button.BoundsChanged += (_, _) => fired++;

        // Act — mounting arranges the control, which sets Bounds from default
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert
        fired.ShouldBeGreaterThanOrEqualTo(1);
    }
}
