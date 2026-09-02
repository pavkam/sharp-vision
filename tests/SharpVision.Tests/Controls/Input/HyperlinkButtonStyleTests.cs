// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies the immutable HyperlinkButton presentation record: its code-owned default,
/// its declared one-hop fallback to <see cref="Theme.GetInteractiveControlStyleSet"/>, and that a
/// complete local style remains authoritative in every visual state.</summary>
public sealed class HyperlinkButtonStyleTests
{
    /// <summary>Verifies Default forces the accent-colored underline at rest.</summary>
    [Fact]
    public void Default_ResolvesAccentColoredUnderline()
    {
        var actual = HyperlinkButtonStyle.Default;

        actual.Face.Foreground.ShouldBe((ControlColor) SemanticColor.Accent);
        actual.Face.Underline.ShouldBe(Underline.Straight);
        actual.Face.UnderlineColor.ShouldBe((ControlColor) SemanticColor.Accent);
    }

    /// <summary>Verifies equality compares every record member structurally, the free record behavior.</summary>
    [Fact]
    public void Equality_WhenEveryMemberMatches_IsEqual()
    {
        var equivalent = new HyperlinkButtonStyle(HyperlinkButtonStyle.Default.Face, HyperlinkButtonStyle.Default.Border, HyperlinkButtonStyle.Default.Shadow);

        equivalent.ShouldBe(HyperlinkButtonStyle.Default);
        (equivalent == HyperlinkButtonStyle.Default).ShouldBeTrue();
    }

    /// <summary>Verifies a local override always wins over both the theme and the fallback.</summary>
    [Fact]
    public void Definition_Resolve_WhenLocalIsSupplied_LocalWins()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());
        var local = HyperlinkButtonStyle.Default with
        {
            Face = HyperlinkButtonStyle.Default.Face with { Foreground = SemanticColor.Warning }
        };

        var resolved = HyperlinkButtonStyle.Definition.Resolve(local, theme);

        resolved.ShouldBe(local);
    }

    /// <summary>Verifies a complete local typed style remains authoritative in every interactive
    /// state rather than having fallback Theme deltas cascaded over it - the exact defect where
    /// every non-Normal state used to fall back to <see cref="ControlStyle.DefaultFace"/>'s
    /// <see cref="Color.Default"/> foreground and no attributes, discarding both the local style's
    /// own colors and the active theme's.</summary>
    [Theory]
    [InlineData(VisualState.IsPointerOver)]
    [InlineData(VisualState.FocusWithin)]
    [InlineData(VisualState.Focused)]
    [InlineData(VisualState.Pressed)]
    [InlineData(VisualState.Disabled)]
    [InlineData(VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed)]
    public void ResolveAppearance_WhenCompleteLocalStyleIsAssigned_LocalWinsThemeStates(VisualState state)
    {
        // Arrange
        var local = HyperlinkButtonStyle.Default with
        {
            Face = new Face(
                Color.Rgb(1, 2, 3),
                Color.Rgb(4, 5, 6),
                TerminalAttributes.Bold,
                Underline.None,
                Color.Default),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Paired,
                Color.Rgb(7, 8, 9),
                Color.Rgb(10, 11, 12),
                TerminalAttributes.Italic),
            Shadow = ControlStyle.NoShadow
        };
        using var link = new HyperlinkButton("Go") { Style = local };
        var expected = link.ResolveAppearance(ThemeCatalog.Dark);

        // Act
        var actual = link.ResolveAppearance(ThemeCatalog.Dark, state);

        // Assert
        actual.ShouldBe(expected);
        actual.Face.Foreground.ShouldBe((ControlColor) Color.Rgb(1, 2, 3));
        actual.Face.Foreground.ShouldNotBe((ControlColor) Color.Default);
    }

    /// <summary>Verifies a Face change is classified as a render-affecting invalidation.</summary>
    [Fact]
    public void Definition_Compare_WhenFaceChanges_IsRender()
    {
        var previous = HyperlinkButtonStyle.Default;
        var current = previous with { Face = previous.Face with { Foreground = SemanticColor.Warning } };

        HyperlinkButtonStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Render);
    }

    /// <summary>Verifies an unchanged style is a non-invalidating comparison.</summary>
    [Fact]
    public void Definition_Compare_WhenNothingChanges_IsNone() =>
        HyperlinkButtonStyle.Definition.Compare(HyperlinkButtonStyle.Default, null, HyperlinkButtonStyle.Default, null)
            .ShouldBe(InvalidationImpact.None);
}
