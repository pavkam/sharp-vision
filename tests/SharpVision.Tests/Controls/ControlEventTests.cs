// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies convenience CLR events on the base ControlBase class.</summary>
public sealed class ControlEventTests
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
            Content = new ControlText("OK")
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
            Content = new ControlText("OK")
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
            Content = new ControlText("OK")
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
            Content = new ControlText("OK")
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
            Content = new ControlText("OK")
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
            Content = new ControlText("OK")
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

    /// <summary>Verifies IsEnabledChanged fires when IsEnabled changes.</summary>
    [Fact]
    public void IsEnabledChanged_WhenDisabled_Fires()
    {
        // Arrange
        var fired = 0;
        var control = new ProbeControl();
        control.IsEnabledChanged += (_, _) => fired++;

        // Act
        control.IsEnabled = false;

        // Assert
        fired.ShouldBe(1);
    }

    /// <summary>Verifies VisibilityChanged fires when Visibility changes.</summary>
    [Fact]
    public void VisibilityChanged_WhenCollapsed_Fires()
    {
        // Arrange
        var fired = 0;
        var control = new ProbeControl();
        control.VisibilityChanged += (_, _) => fired++;

        // Act
        control.Visibility = Visibility.Collapsed;

        // Assert
        fired.ShouldBe(1);
    }

    /// <summary>Verifies ParentChanged fires when added to a container.</summary>
    [Fact]
    public void ParentChanged_WhenAddedToContainer_Fires()
    {
        // Arrange
        var fired = 0;
        var child = new ProbeControl();
        child.ParentChanged += (_, _) => fired++;

        // Act
        _ = new Stack { Children = { child } };

        // Assert
        fired.ShouldBe(1);
    }
}
