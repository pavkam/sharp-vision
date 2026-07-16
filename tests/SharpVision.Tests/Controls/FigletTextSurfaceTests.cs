// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies FigletText art, mutation, style, and clipping through a mounted surface.</summary>
public sealed class FigletTextSurfaceTests
{
    /// <summary>Verifies audited Small font output and mutation clear stale generated cells.</summary>
    [Fact]
    public async Task UpdateAsync_WhenFigletContentChanges_ReplacesExactAuditedArtAsync()
    {
        // Arrange
        var title = new FigletText(FigletCatalog.Default.Load("Small"))
        {
            Content = "A",
            Foreground = Color.Indexed(14),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        await using var surface = await ComponentSurface.MountAsync(
            title,
            new Size(7, 5),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
               _
              /_\
             / _ \
            /_/ \_\

            """);
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(Color.Indexed(14));

        // Act
        await surface.UpdateAsync(() => title.Content = "I", "change FigletText content");

        // Assert
        surface.ShouldRender("""
             ___
            |_ _|
             | |
            |___|

            """);
        surface.Cell(new Point(5, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(5, 0)).Style.Attributes.ShouldBe(Attributes.None);
    }

    /// <summary>Verifies parent clipping exposes only complete in-bounds generated cells.</summary>
    [Fact]
    public async Task Render_WhenFigletArtExceedsSurface_ClipsToMountedBoundsAsync()
    {
        // Arrange
        var title = new FigletText(FigletCatalog.Default.Load("Small"))
        {
            Content = "A",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(7),
            Height = Length.Cells(5),
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            title,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
               _
              /_
             / _
            """);
    }
}
