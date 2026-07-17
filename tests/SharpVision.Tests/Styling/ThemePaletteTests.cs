// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies immutable semantic palette completeness.</summary>
public sealed class ThemePaletteTests
{
    /// <summary>Verifies a palette rejects a missing required role.</summary>
    [Fact]
    public void Constructor_WhenRoleIsMissing_Throws()
    {
        var colors = new Dictionary<ColorRole, Color>();

        _ = Should.Throw<ArgumentException>(() => new ThemePalette(colors));
    }

    /// <summary>Verifies the palette owns a copy of supplied values.</summary>
    [Fact]
    public void Constructor_WhenSourceMutates_PreservesSnapshot()
    {
        var colors = Enum.GetValues<ColorRole>().ToDictionary(static role => role, static _ => Color.Default);
        var palette = new ThemePalette(colors);
        colors[ColorRole.Accent] = Color.Indexed(2);

        palette[ColorRole.Accent].ShouldBe(Color.Default);
    }
}
