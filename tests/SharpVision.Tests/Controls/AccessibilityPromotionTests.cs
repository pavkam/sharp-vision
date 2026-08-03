// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the behavior-preserving accessibility promotions from #155 (GetProfile,
/// ResolveColor, MaximumImpact) are reachable exactly as before, from strictly wider seams.</summary>
public sealed class AccessibilityPromotionTests
{
    /// <summary>Verifies Theme.GetProfile is public and returns the same profiles component authors
    /// previously could reach only through the internal semantic-role switch.</summary>
    [Fact]
    public void GetProfile_WhenCalledPublicly_ReturnsMatchingSemanticProfile()
    {
        Themes.Dark.GetProfile(ThemeRole.Control).ShouldBe(Themes.Dark.Control);
        Themes.Dark.GetProfile(ThemeRole.Input).ShouldBe(Themes.Dark.Input);
        Themes.Dark.GetProfile(ThemeRole.Container).ShouldBe(Themes.Dark.Container);
        Themes.Dark.GetProfile(ThemeRole.Window).ShouldBe(Themes.Dark.Window);
        Themes.Dark.GetProfile(ThemeRole.Popup).ShouldBe(Themes.Dark.Popup);
    }

    /// <summary>Verifies GetProfile still rejects an unknown role.</summary>
    [Fact]
    public void GetProfile_WhenRoleIsUnknown_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Themes.Dark.GetProfile((ThemeRole) 999));

    /// <summary>Verifies Control.ResolveColor is reachable from a third-party subclass in another
    /// assembly, which the previous internal accessibility disallowed.</summary>
    [Fact]
    public void ResolveColor_WhenCalledFromThirdPartySubclass_ResolvesLiteralAndThemeValues()
    {
        var literal = Color.Rgb(0xff, 0x00, 0x00);
        AccessibilityPromotionProbe.ProbeResolveColor(literal, Themes.Dark).ShouldBe(literal);
        AccessibilityPromotionProbe.ProbeResolveColor(ThemeColor.Accent, Themes.Dark)
            .ShouldBe(Themes.Dark.ResolveColor(ThemeColor.Accent));
        AccessibilityPromotionProbe.ProbeResolveColor(ThemeColor.Accent, null).ShouldBe(Color.Default);
    }

    /// <summary>Verifies Control.MaximumImpact is reachable from a third-party subclass in another
    /// assembly, which the previous internal accessibility disallowed.</summary>
    [Theory]
    [InlineData(InvalidationImpact.None, InvalidationImpact.Render, InvalidationImpact.Render)]
    [InlineData(InvalidationImpact.Measure, InvalidationImpact.Arrange, InvalidationImpact.Measure)]
    [InlineData(InvalidationImpact.Render, InvalidationImpact.Render, InvalidationImpact.Render)]
    public void MaximumImpact_WhenCalledFromThirdPartySubclass_ReturnsStrongerImpact(
        InvalidationImpact left,
        InvalidationImpact right,
        InvalidationImpact expected) =>
        AccessibilityPromotionProbe.ProbeMaximumImpact(left, right).ShouldBe(expected);
}
