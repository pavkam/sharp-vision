// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Button appearance and interaction through a mounted terminal surface.</summary>
public sealed class ButtonSurfaceTests
{
    /// <summary>Verifies initial layout, normal styling, and the detached composite shadow.</summary>
    [Fact]
    public async Task Render_WhenButtonIsMounted_ShowsNormalFaceAndCompositeShadowAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Content = new ControlText("Save"),
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldHaveState(button, State.Normal);
        surface.ShouldRender("""
            ╭──────╮
            │Save  │
            ╰──────╯


            """);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(Color.Indexed(8));
        surface.Cell(new Point(8, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
    }
}
