// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies intrinsic border glyph families through mounted terminal surfaces.</summary>
public sealed class IntrinsicBorderSurfaceTests
{
    /// <summary>Verifies the half-block preset draws every physical edge and corner exactly.</summary>
    [Fact]
    public async Task Render_WhenHalfBlockBorderIsMounted_DrawsSculptedFrameAsync()
    {
        // Arrange
        var control = new Dock
        {
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.HalfBlock,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { new ControlText("Half") },
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            control,
            new Size(7, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
            ▛▀▀▀▀▀▜
            ▌Half ▐
            ▙▄▄▄▄▄▟
            """);
    }
}
