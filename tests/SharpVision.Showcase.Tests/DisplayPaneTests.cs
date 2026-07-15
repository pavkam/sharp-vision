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
}
