// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

/// <summary>Verifies box-model background ownership through mounted terminal cells.</summary>
public sealed class BoxModelSurfaceTests
{
    /// <summary>Verifies changing margin and padding reflows and repaints the parent and child surfaces.</summary>
    [Fact]
    public async Task UpdateAsync_WhenChildGainsMarginAndPadding_UsesParentAndChildBackgroundsAsync()
    {
        // Arrange
        var parentBackground = ReferenceColors.Get(1);
        var childBackground = ReferenceColors.Get(4);
        var child = new Dock
        {
            Face = AppearanceTestValues.Face(background: childBackground),
        };
        var parent = new Dock
        {
            Face = AppearanceTestValues.Face(background: parentBackground),
            Children = { child }
        };

        await using var surface = await ComponentSurface.MountAsync(
            parent,
            new Size(9, 7),
            TestContext.Current.CancellationToken);
        child.Bounds.ShouldBe(new Rect(0, 0, 9, 7));
        surface.Cell(default).Style.Background.ShouldBe(childBackground);

        // Act
        await surface.UpdateAsync(
            () =>
            {
                child.Margin = new Thickness(1);
                child.Padding = new Thickness(1);
            },
            "apply child margin and padding");

        // Assert
        child.Bounds.ShouldBe(new Rect(1, 1, 7, 5));
        child.ContentBounds.ShouldBe(new Rect(2, 2, 5, 3));
        AssertBackgrounds(
            surface,
            parentBackground,
            new Point(4, 0),
            new Point(0, 3),
            new Point(8, 3),
            new Point(4, 6));
        AssertBackgrounds(
            surface,
            childBackground,
            new Point(4, 1),
            new Point(1, 3),
            new Point(7, 3),
            new Point(4, 5));
    }

    /// <summary>Verifies transparent border cells separate parent-owned margin from child-owned padding without adopting the child face.</summary>
    [Fact]
    public async Task Render_WhenBorderSeparatesMarginAndPadding_PreservesBothBackgroundPlanesAsync()
    {
        // Arrange
        var parentBackground = ReferenceColors.Get(1);
        var childBackground = ReferenceColors.Get(4);
        var borderForeground = ReferenceColors.Get(11);
        var child = new Dock
        {
            Face = AppearanceTestValues.Face(background: childBackground),
            Border = AppearanceTestValues.Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                borderForeground),
            Margin = new Thickness(1),
            Padding = new Thickness(1)
        };
        var parent = new Dock
        {
            Face = AppearanceTestValues.Face(background: parentBackground),
            Children = { child }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            parent,
            new Size(9, 7),
            TestContext.Current.CancellationToken);

        // Assert
        child.Bounds.ShouldBe(new Rect(1, 1, 7, 5));
        child.ContentBounds.ShouldBe(new Rect(3, 3, 3, 1));
        AssertBackgrounds(
            surface,
            parentBackground,
            new Point(4, 0),
            new Point(0, 3),
            new Point(8, 3),
            new Point(4, 6));
        AssertBackgrounds(
            surface,
            childBackground,
            new Point(4, 2),
            new Point(2, 3),
            new Point(6, 3),
            new Point(4, 4));

        var borderPoints = new[]
        {
            new Point(4, 1),
            new Point(1, 3),
            new Point(7, 3),
            new Point(4, 5)
        };
        AssertBackgrounds(surface, parentBackground, borderPoints);

        foreach (var point in borderPoints)
        {
            surface.Cell(point).Style.Foreground.ShouldBe(borderForeground, $"Border cell {point}.");
        }
    }

    private static void AssertBackgrounds(
        ComponentSurface surface,
        Color expected,
        params Point[] points)
    {
        foreach (var point in points)
        {
            surface.Cell(point).Style.Background.ShouldBe(expected, $"Background cell {point}.");
        }
    }
}
