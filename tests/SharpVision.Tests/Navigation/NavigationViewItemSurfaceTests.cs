// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies NavigationViewItem's StartAffix/EndAffix rendering on a mounted surface.</summary>
public sealed class NavigationViewItemSurfaceTests
{
    /// <summary>Verifies an item without affixes renders exactly as before - the marker, glyph
    /// prefix, and caption with no reserved affix columns.</summary>
    [Fact]
    public async Task Render_WhenItemHasNoAffixes_DrawsOnlyMarkerAndCaptionAsync()
    {
        // Arrange
        var item = new NavigationViewItem { Text = "Go" };
        var view = CreateView(9);
        view.Items.Add(item);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(9, 3),
            TestContext.Current.CancellationToken);

        // Assert - chrome is " {marker} " (3 cells), then the caption immediately follows.
        var row = item.Bounds.Y;
        surface.Cell(new Point(item.Bounds.X + 3, row)).Text.ShouldBe("G");
        surface.Cell(new Point(item.Bounds.X + 4, row)).Text.ShouldBe("o");
    }

    /// <summary>Verifies both affixes reserve their own cell column pinned flush to the content
    /// box edges - after the marker/glyph prefix on the start side, at the far right on the end
    /// side - with the caption confined to the remaining middle box.</summary>
    [Fact]
    public async Task Render_WhenItemHasBothAffixes_PinsThemInsideTheContentBoxAsync()
    {
        // Arrange
        var item = new NavigationViewItem
        {
            Text = "Go",
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        // 3 chrome cells + (1 affix + 1 gap) + 2 caption cells + (1 gap + 1 affix) = 9, an exact fit.
        var view = CreateView(9);
        view.Items.Add(item);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(9, 3),
            TestContext.Current.CancellationToken);

        // Assert
        var row = item.Bounds.Y;
        surface.Cell(new Point(item.Bounds.X + 3, row)).Text.ShouldBe(">");
        surface.Cell(new Point(item.Bounds.X + 5, row)).Text.ShouldBe("G");
        surface.Cell(new Point(item.Bounds.X + 6, row)).Text.ShouldBe("o");
        surface.Cell(new Point(item.Bounds.X + 8, row)).Text.ShouldBe("<");
    }

    /// <summary>Verifies the marker and glyph prefix stay outboard of both affixes - the glyph
    /// prefix keeps its own fixed position regardless of whether affixes are set.</summary>
    [Fact]
    public async Task Render_WhenItemHasGlyphAndAffixes_KeepsGlyphPrefixOutboardAsync()
    {
        // Arrange
        var item = new NavigationViewItem
        {
            Text = "Go",
            Glyph = "*",
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        // 3 chrome cells + 2 glyph-prefix cells + (1 affix + 1 gap) + 2 caption cells + (1 gap + 1 affix) = 11.
        var view = CreateView(11);
        view.Items.Add(item);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(11, 3),
            TestContext.Current.CancellationToken);

        // Assert - the glyph prefix draws at its usual fixed position, untouched by affixes.
        var row = item.Bounds.Y;
        surface.Cell(new Point(item.Bounds.X + 3, row)).Text.ShouldBe("*");
        surface.Cell(new Point(item.Bounds.X + 5, row)).Text.ShouldBe(">");
        surface.Cell(new Point(item.Bounds.X + 7, row)).Text.ShouldBe("G");
        surface.Cell(new Point(item.Bounds.X + 8, row)).Text.ShouldBe("o");
        surface.Cell(new Point(item.Bounds.X + 10, row)).Text.ShouldBe("<");
    }

    /// <summary>Verifies the start affix survives and the end affix drops whole when the content
    /// box beyond the marker/glyph prefix has room for only one affix, matching the documented
    /// priority: caption shrinks first, then the end affix, then the start affix.</summary>
    [Fact]
    public async Task Render_WhenContentBoxHasRoomForOnlyOneAffix_DropsTheEndAffixFirstAsync()
    {
        // Arrange
        var item = new NavigationViewItem
        {
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        // 3 chrome cells + exactly 1 spare cell beyond them.
        var view = CreateView(4);
        view.Items.Add(item);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Assert - the sole remaining cell is claimed by the start affix.
        var row = item.Bounds.Y;
        surface.Cell(new Point(item.Bounds.X + 3, row)).Text.ShouldBe(">");
    }

    /// <summary>Verifies a same-resolved-width affix content swap updates the mounted surface
    /// without a remeasure.</summary>
    [Fact]
    public async Task StartAffix_WhenContentChangesAtTheSameResolvedWidth_UpdatesRenderOnlyAsync()
    {
        // Arrange
        var item = new NavigationViewItem { Text = "Go", StartAffix = new Affix("|") };
        var view = CreateView(9);
        view.Items.Add(item);
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(9, 3),
            TestContext.Current.CancellationToken);
        var row = item.Bounds.Y;
        surface.Cell(new Point(item.Bounds.X + 3, row)).Text.ShouldBe("|");
        var impact = Invalidation.None;

        // Act
        await surface.UpdateAsync(
            () =>
            {
                item.Clear(Invalidation.All);
                item.StartAffix = new Affix("/");
                impact = item.Pending;
            },
            "swap start affix content at the same resolved width");

        // Assert
        impact.ShouldBe(Invalidation.Render);
        surface.Cell(new Point(item.Bounds.X + 3, row)).Text.ShouldBe("/");
    }

    private static NavigationView CreateView(int width)
    {
        var view = new NavigationView
        {
            Width = Length.Cells(width),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        return view;
    }
}
