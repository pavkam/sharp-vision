// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

using GraphicsImage = Terminal.Graphics.Image;

/// <summary>Proves Image through a mounted application with deterministic cell fallback.</summary>
public sealed class ImageSurfaceTests
{
    /// <summary>Verifies unsupported graphics retain full alternate-text underlay at mounted size.</summary>
    [ComponentBehaviorEvidence(
        typeof(Image),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
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
}
