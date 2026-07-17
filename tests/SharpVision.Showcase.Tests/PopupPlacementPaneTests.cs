// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies the popup placement specimen communicates its anchor, directions, and preview.</summary>
public sealed class PopupPlacementPaneTests
{
    /// <summary>Verifies the retained placement diagram uses visible chrome and opens the requested preview.</summary>
    [Fact]
    public void Render_WhenPlacementDiagramBuilds_ShowsFramedAnchorAndDirectionControls()
    {
        // Arrange
        using var page = new PopupPane();
        var size = new Size(120, 360);
        var engine = new Engine();
        engine.Layout(page, size);
        var buttons = ControlTree.FindAll<Button>(page);
        var labels = buttons.Select(Content).OfType<string>().ToArray();
        labels.ShouldContain("⚓ Anchor");
        labels.ShouldContain("↑ Above");
        labels.ShouldContain("← Left");
        labels.ShouldContain("Right →");
        labels.ShouldContain("↓ Below");
        var anchor = buttons.Single(value => Content(value) == "⚓ Anchor");
        var directions = buttons.Where(value => Content(value) is "↑ Above" or "← Left" or "Right →" or "↓ Below").ToArray();
        var stage = anchor.Parent.ShouldBeOfType<SharpVision.Controls.Canvas>().Parent.ShouldBeOfType<Overlay>();
        var stageSurface = stage.Children[0].ShouldBeOfType<Dock>();
        var popup = stage.Children[2].ShouldBeOfType<Popup>();
        var right = directions.Single(value => Content(value) == "Right →");

        // Act
        right.PerformClick();
        engine.Layout(page, size);
        using Frame frame = new(size);
        page.Render(frame.Canvas);
        var screen = new Screen(frame);

        // Assert
        stageSurface.BorderThickness.ShouldBe(new Thickness(1));
        stageSurface.BorderGlyphs.ShouldBe(Glyphs.Rounded);
        stageSurface.BorderColor.ShouldBe(ThemeColor.From(ColorRole.Accent));
        anchor.BorderThickness.ShouldBe(new Thickness(1));
        anchor.BorderGlyphs.ShouldBe(Glyphs.Rounded);
        anchor.BorderColor.ShouldBe(ThemeColor.From(ColorRole.Accent));

        foreach (var direction in directions)
        {
            direction.BorderThickness.ShouldBe(new Thickness(1));
            direction.BorderGlyphs.ShouldBe(Glyphs.Rounded);
            direction.BorderColor.ShouldBe(ThemeColor.From(ColorRole.Border));
        }

        popup.IsOpen.ShouldBeTrue();
        popup.Placement.ShouldBe(PopupPlacement.Right);
        screen.Text.ShouldContain("⚓ Anchor");
        screen.Text.ShouldContain("Placement preview");
        screen.Text.ShouldContain("Requested side: Right");
    }

    private static string? Content(Button button) =>
        button.Content is ControlText text ? text.Content : null;
}
