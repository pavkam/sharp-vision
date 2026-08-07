// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the behavior-preserving accessibility promotions (ResolveColor,
/// MaximumImpact) are reachable exactly as before, from strictly wider seams. The third
/// promotion this file originally covered, Theme.GetProfile, was itself retired -
/// the six well-known semantic profiles it dispatched to (Theme.Control/Input/Container/
/// Window/Popup/Tooltip) remain public properties in their own right, so the accessibility
/// promise stands even though the enum-indexed switch that used to front them does not.</summary>
public sealed class AccessibilityPromotionTests
{
    /// <summary>Verifies Control.ResolveColor is reachable from a third-party subclass in another
    /// assembly, which the previous internal accessibility disallowed.</summary>
    [Fact]
    public void ResolveColor_WhenCalledFromThirdPartySubclass_ResolvesLiteralAndThemeValues()
    {
        var literal = Color.Rgb(0xff, 0x00, 0x00);
        AccessibilityPromotionProbe.ProbeResolveColor(literal, ThemeCatalog.Dark).ShouldBe(literal);
        AccessibilityPromotionProbe.ProbeResolveColor(SemanticColor.Accent, ThemeCatalog.Dark)
            .ShouldBe(ThemeCatalog.Dark.ResolveColor(SemanticColor.Accent));
        AccessibilityPromotionProbe.ProbeResolveColor(SemanticColor.Accent, null).ShouldBe(Color.Default);
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
