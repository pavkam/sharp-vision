// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

using GraphicsImage = Terminal.Graphics.ImageSource;

/// <summary>Proves Image through a mounted application with deterministic cell fallback.</summary>
public sealed class ImageSurfaceTests
{
    /// <summary>Verifies unsupported graphics retain full alternate-text underlay at mounted size.</summary>
    [Fact]
    public async Task Render_WhenGraphicsAreUnsupported_ShowsCompleteMountedFallbackAsync()
    {
        var image = new Image
        {
            Source = GraphicsImage.FromRgba(new Size(1, 1), [255, 0, 0, 255]),
            AlternateText = "photo",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            image,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(image);

        surface.ShouldRender("photo░░░\n░░░░░░░░");
        image.IsPointerOver.ShouldBeTrue();
        image.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies direct and ancestor-inherited disable painting, stable geometry across a
    /// genuine resize, and re-enable recovery for a mounted Image.</summary>
    [Fact]
    public async Task IsEnabled_WhenImageIsDisabled_ProvesDisabledContractAsync()
    {
        // Arrange
        var image = new Image
        {
            Source = GraphicsImage.FromRgba(new Size(1, 1), [255, 0, 0, 255]),
            AlternateText = "photo",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            image,
            new Size(8, 2),
            TestContext.Current.CancellationToken);

        // Act — direct disable
        await surface.UpdateAsync(() => image.IsEnabled = false, "disable Image");

        // Assert
        surface.ShouldHaveState(image, VisualState.Disabled);

        // Arrange — ancestor-inherited disable
        var child = new Image
        {
            Source = GraphicsImage.FromRgba(new Size(1, 1), [255, 0, 0, 255]),
            AlternateText = "photo",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var stack = new Stack { Children = { child } };
        await using var ancestorSurface = await ComponentSurface.MountAsync(
            stack,
            new Size(8, 2),
            TestContext.Current.CancellationToken);

        // Act
        await ancestorSurface.UpdateAsync(() => stack.IsEnabled = false, "disable ancestor Stack");

        // Assert
        child.EffectiveIsEnabled.ShouldBeFalse();
        ancestorSurface.ShouldHaveState(child, VisualState.Disabled);

        // Act — geometry stability across a genuine resize
        await surface.ResizeAsync(new Size(6, 3));
        var enabledImage = new Image
        {
            Source = GraphicsImage.FromRgba(new Size(1, 1), [255, 0, 0, 255]),
            AlternateText = "photo",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabledImage,
            new Size(6, 3),
            TestContext.Current.CancellationToken);

        // Assert
        image.Bounds.ShouldBe(enabledImage.Bounds);
        image.DesiredSize.ShouldBe(enabledImage.DesiredSize);

        // Act — re-enable recovery
        await surface.UpdateAsync(() => image.IsEnabled = true, "re-enable Image");

        // Assert
        surface.ShouldHaveState(image, VisualState.Normal);
    }
}
