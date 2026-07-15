// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies the builder produces a frozen theme with roles and a base control style.</summary>
public sealed class ThemeBuilderTests
{
    private static Dictionary<ColorRole, Color> Roles()
    {
        Dictionary<ColorRole, Color> roles = [];

        foreach (ColorRole role in Enum.GetValues<ColorRole>())
        {
            roles[role] = Color.Indexed((int) role + 1);
        }

        return roles;
    }

    /// <summary>Verifies the built theme is frozen and carries every resolved role color.</summary>
    [Fact]
    public void Build_ProducesFrozenThemeWithRoles()
    {
        Theme theme = ThemeBuilder.Build(Roles());

        theme.IsFrozen.ShouldBeTrue();
        theme.TryGetColor(ColorRole.Accent, out Color accent).ShouldBeTrue();
        accent.ShouldBe(Color.Indexed((int) ColorRole.Accent + 1));
    }

    /// <summary>Verifies the base control style carries the semantic role color, not a resolved concrete.</summary>
    [Fact]
    public void Build_BaseStyleStoresRoleColors()
    {
        Dictionary<ColorRole, Color> roles = Roles();
        Theme theme = ThemeBuilder.Build(roles);

        // The base style now carries the semantic (role) value, not a pre-resolved concrete.
        ControlStyle<Control> style = theme.GetStyle<Control>()!;
        style.TryGet(Control.ForegroundProperty, State.Normal, out Color? fg).ShouldBeTrue();
        fg!.Value.Kind.ShouldBe(ColorKind.Role);
        fg.Value.RoleId.ShouldBe((int) ColorRole.Foreground);
    }

    /// <summary>Verifies the base control style's role colors resolve to the seeded palette through the theme.</summary>
    [Fact]
    public void Build_ResolvesRoleColorsToSeededPalette()
    {
        Dictionary<ColorRole, Color> roles = Roles();
        Theme theme = ThemeBuilder.Build(roles);

        ThemeResolver.Resolve(theme, typeof(Control), Control.ForegroundProperty, State.Normal)
            .ShouldBe(roles[ColorRole.Foreground]);
        ThemeResolver.Resolve(theme, typeof(Control), Control.BackgroundProperty, State.Selected)
            .ShouldBe(roles[ColorRole.SelectionBackground]);
        ThemeResolver.Resolve(theme, typeof(Control), Control.ForegroundProperty, State.Disabled)
            .ShouldBe(roles[ColorRole.Muted]);
    }
}
