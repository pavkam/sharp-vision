// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies text, FIGfont, and theming showcase recipes.</summary>
public sealed class DisplayPaneTests
{
    /// <summary>Verifies Text safely renders dynamic markup metacharacters and malformed fragments.</summary>
    [Fact]
    public void Text_WhenSafeContentRenders_PreservesLiteralInput()
    {
        // Arrange
        using var page = new TextPane();
        var size = new Size(100, 140);
        new Engine().Layout(page, size);
        using Frame frame = new(size);

        // Act
        page.Render(frame.Canvas);

        // Assert
        var screen = new Screen(frame);
        screen.Text.ShouldContain("Dynamic: 2 < 3");
        screen.Text.ShouldContain("Malformed: <unknown=bad>");
        screen.ValidateContinuations();
    }

    /// <summary>Verifies FigletText compares several audited fonts over the same source.</summary>
    [Fact]
    public void FigletText_WhenComparisonBuilds_ContainsDistinctAuditedFonts()
    {
        // Arrange
        using var page = new FigletTextPane();
        new Engine().Layout(page, new Size(120, 180));

        // Act
        var fonts = ControlTree.FindAll<FigletText>(page)
            .Select(value => value.Font.Name)
            .ToArray();

        // Assert
        fonts.ShouldContain("Standard");
        fonts.ShouldContain("Slant");
        fonts.ShouldContain("Small");
    }

    /// <summary>Verifies caller-driven Prism animation changes color without changing text or geometry.</summary>
    [Fact]
    public void Prism_WhenPhaseButtonActivates_ChangesColorsWithoutMovingContent()
    {
        // Arrange
        using var page = new PrismPane();
        var size = new Size(100, 80);
        var engine = new Engine();
        engine.Layout(page, size);
        var prism = ControlTree.FindAll<Prism>(page).Single(value =>
            value.Direction == PrismDirection.Diagonal &&
            value.Content is FigletText { Content: "PRISM" });
        var content = prism.Content.ShouldNotBeNull();
        var advance = ControlTree.FindAll<Button>(page).Single(value =>
            value.Content is ControlText text &&
            text.Content.Contains("Advance phase", StringComparison.Ordinal));
        var status = ControlTree.FindAll<ControlText>(page).Single(value =>
            string.Equals(value.Content, "Phase 0 / 60", StringComparison.Ordinal));
        var prismBounds = prism.Bounds;
        var contentBounds = content.Bounds;
        using Frame beforeFrame = new(size);
        prism.Render(beforeFrame.Canvas);
        var before = new Screen(beforeFrame);
        var point = FindStoredCell(beforeFrame, contentBounds);
        var foreground = beforeFrame.GetCell(point).Style.Foreground;

        // Act
        advance.PerformClick();
        engine.Layout(page, size);
        using Frame afterFrame = new(size);
        prism.Render(afterFrame.Canvas);
        var after = new Screen(afterFrame);

        // Assert
        prism.Bounds.ShouldBe(prismBounds);
        content.Bounds.ShouldBe(contentBounds);
        after.Text.ShouldBe(before.Text);
        afterFrame.GetCell(point).Style.Foreground.ShouldNotBe(foreground);
        prism.Phase.ShouldBe(1d / 60d);
        status.Content.ShouldBe("Phase 1 / 60");
    }

    /// <summary>Verifies Theming exposes catalog metadata and concrete visual-state controls.</summary>
    [Fact]
    public void Theming_WhenPageBuilds_ShowsCatalogAndStateMatrix()
    {
        // Arrange
        using var page = new ThemingPane();
        new Engine().Layout(page, new Size(120, 180));

        // Act
        var content = ControlTree.Text(page);
        var buttons = ControlTree.FindAll<Button>(page);

        // Assert
        content.ShouldContain("Catalog entry:");
        content.ShouldContain("Impact.Measure");
        buttons.ShouldContain(value => !value.IsEnabled);
        ControlTree.FindAll<CheckBox>(page).ShouldNotBeEmpty();
    }

    private static Point FindStoredCell(Frame frame, Rect bounds)
    {
        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                var point = new Point(x, y);

                if (frame.GetGraphemeByteCount(point) > 0)
                {
                    return point;
                }
            }
        }

        throw new InvalidOperationException("The live Prism FIGlet specimen did not render a stored cell.");
    }
}
