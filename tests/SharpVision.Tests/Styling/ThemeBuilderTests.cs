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

    /// <summary>Verifies the base control style maps roles to the expected property/state pairs.</summary>
    [Fact]
    public void Build_SetsBaseControlStyleForRepresentativeStates()
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
