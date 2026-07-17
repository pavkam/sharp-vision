// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies the UI/terminal colour boundary.</summary>
public sealed class ThemeColorTests
{
    /// <summary>Verifies a semantic role remains unresolved until a theme supplies its concrete colour.</summary>
    [Fact]
    public void Resolve_WhenThemeColorIsRole_UsesThemePalette()
    {
        var theme = new Theme();
        theme.SetColor(ColorRole.Accent, Color.Indexed(14));
        theme.Freeze();

        var resolved = theme.Resolve(ThemeColor.From(ColorRole.Accent));

        resolved.ShouldBe(Color.Indexed(14));
    }

    /// <summary>Verifies a concrete UI colour crosses the theme boundary unchanged.</summary>
    [Fact]
    public void Resolve_WhenThemeColorIsConcrete_PreservesColor()
    {
        var theme = new Theme();
        var color = Color.Rgb(10, 20, 30);

        theme.Resolve(ThemeColor.From(color)).ShouldBe(color);
    }
}
