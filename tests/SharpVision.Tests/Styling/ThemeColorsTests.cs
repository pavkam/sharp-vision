// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;



/// <summary>Verifies the semantic-color accessor maps to color roles.</summary>
public sealed class ThemeColorsTests
{
    /// <summary>Verifies <see cref="ThemeColors.Accent"/> is a role color carrying <see cref="ColorRole.Accent"/>.</summary>
    [Fact]
    public void Accent_IsRoleColorForAccent()
    {
        ThemeColors.Accent.Kind.ShouldBe(ColorKind.Role);
        ThemeColors.Accent.RoleId.ShouldBe((int) ColorRole.Accent);
    }

    /// <summary>Verifies every <see cref="ColorRole"/> has a matching <see cref="ThemeColors"/> accessor.</summary>
    [Fact]
    public void EveryRole_HasAMatchingAccessor()
    {
        // Each ThemeColors property is a role color whose id round-trips to a ColorRole.
        (Color color, ColorRole role)[] map =
        [
            (ThemeColors.Foreground, ColorRole.Foreground),
            (ThemeColors.Background, ColorRole.Background),
            (ThemeColors.Surface, ColorRole.Surface),
            (ThemeColors.Border, ColorRole.Border),
            (ThemeColors.Accent, ColorRole.Accent),
            (ThemeColors.Muted, ColorRole.Muted),
            (ThemeColors.SelectionBackground, ColorRole.SelectionBackground),
            (ThemeColors.SelectionForeground, ColorRole.SelectionForeground),
            (ThemeColors.Error, ColorRole.Error),
            (ThemeColors.Warning, ColorRole.Warning),
            (ThemeColors.Success, ColorRole.Success),
            (ThemeColors.Info, ColorRole.Info),
        ];

        foreach ((var color, var role) in map)
        {
            color.RoleId.ShouldBe((int) role);
        }
    }
}
