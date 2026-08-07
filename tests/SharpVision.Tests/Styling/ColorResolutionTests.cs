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
        var theme = ThemeCatalog.Dark;
        var expected = theme.ResolveColor(SemanticColor.ActiveBorder);

        _ = Parallel.For(
            0,
            100_000,
            _ => theme.ResolveColor(SemanticColor.ActiveBorder).ShouldBe(expected));
    }

    /// <summary>Verifies semantic values resolve to the configured concrete color.</summary>
    [Fact]
    public void ResolveColor_WhenKnownSemanticColorIsRequested_ReturnsConcreteColor()
    {
        var expected = ThemeCatalog.Dark.ResolveColor(SemanticColor.FocusedText);

        expected.IsRgb.ShouldBeTrue();
        ThemeCatalog.Dark.Resolve(ThemeCatalog.Dark.Control.Resolve(VisualState.Focused).Face.Foreground).ShouldBe(expected);
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
