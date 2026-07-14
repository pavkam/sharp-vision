// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies semantic color roles are stored, cloned, and readable from built-in themes.</summary>
public sealed class ColorRoleTests
{
    /// <summary>Verifies a set color role is read back.</summary>
    [Fact]
    public void SetColor_ThenTryGetColor_ReturnsValue()
    {
        Theme theme = new();
        theme.SetColor(ColorRole.Accent, Color.Indexed(45));

        theme.TryGetColor(ColorRole.Accent, out Color color).ShouldBeTrue();
        color.ShouldBe(Color.Indexed(45));
    }

    /// <summary>Verifies an undefined role reports absence.</summary>
    [Fact]
    public void TryGetColor_WhenUndefined_ReturnsFalse()
    {
        Theme theme = new();

        theme.TryGetColor(ColorRole.Accent, out _).ShouldBeFalse();
    }

    /// <summary>Verifies the built-in themes define distinct accent colors.</summary>
    [Fact]
    public void BuiltInThemes_DefineAccentRole()
    {
        Themes.White.TryGetColor(ColorRole.Accent, out Color white).ShouldBeTrue();
        Themes.Dark.TryGetColor(ColorRole.Accent, out Color dark).ShouldBeTrue();

        white.ShouldNotBe(dark);
    }

    /// <summary>Verifies cloning preserves color roles.</summary>
    [Fact]
    public void Clone_PreservesColorRoles()
    {
        Theme theme = new();
        theme.SetColor(ColorRole.Border, Color.Indexed(67));

        Theme clone = theme.Clone();

        clone.TryGetColor(ColorRole.Border, out Color color).ShouldBeTrue();
        color.ShouldBe(Color.Indexed(67));
    }

    /// <summary>Verifies status and selection roles round-trip through set/get.</summary>
    [Fact]
    public void SetColor_WhenStatusRole_RoundTrips()
    {
        Theme theme = new();
        theme.SetColor(ColorRole.Error, Color.Rgb(255, 0, 0));
        theme.SetColor(ColorRole.SelectionBackground, Color.Indexed(4));
        theme.SetColor(ColorRole.SelectionForeground, Color.Indexed(15));

        theme.TryGetColor(ColorRole.Error, out Color error).ShouldBeTrue();
        error.ShouldBe(Color.Rgb(255, 0, 0));
        theme.TryGetColor(ColorRole.SelectionBackground, out Color selBg).ShouldBeTrue();
        selBg.ShouldBe(Color.Indexed(4));
        theme.TryGetColor(ColorRole.SelectionForeground, out Color selFg).ShouldBeTrue();
        selFg.ShouldBe(Color.Indexed(15));
    }
}
