// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies the semantic-theme and terminal color-resolution boundary.</summary>
public sealed class ColorResolutionTests
{
    /// <summary>Verifies one frozen theme safely resolves global colors from concurrent renderer threads.</summary>
    [Fact]
    public void ResolveColor_WhenFrozenThemeIsReadConcurrently_ReturnsStableColors()
    {
        var theme = Themes.Dark;
        var expected = theme.ResolveColor(ThemeColor.ActiveBorder);

        _ = Parallel.For(
            0,
            100_000,
            _ => theme.ResolveColor(ThemeColor.ActiveBorder).ShouldBe(expected));
    }

    /// <summary>Verifies semantic values resolve to the configured concrete color.</summary>
    [Fact]
    public void ResolveColor_WhenKnownRoleIsRequested_ReturnsConcreteColor()
    {
        var expected = Themes.Dark.ResolveColor(ThemeColor.FocusedText);

        expected.IsRgb.ShouldBeTrue();
        Themes.Dark.Resolve(Themes.Dark.Input.Resolve(VisualState.Focused).Face.Foreground).ShouldBe(expected);
    }

    /// <summary>Verifies a concrete RGB color preserves every channel.</summary>
    [Fact]
    public void Color_WhenConcrete_PreservesValue()
    {
        var color = Color.Rgb(10, 20, 30);

        color.IsRgb.ShouldBeTrue();
        color.Red.ShouldBe((byte) 10);
        color.Green.ShouldBe((byte) 20);
        color.Blue.ShouldBe((byte) 30);
    }
}
