// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Models a third-party control exercising the protected accessibility seams promoted
/// for component authors (ResolveColor, MaximumImpact).</summary>
internal sealed class AccessibilityPromotionProbe: ControlBase
{
    /// <summary>Resolves one color value through the protected static seam.</summary>
    /// <param name="value">The literal or theme-referenced color value.</param>
    /// <param name="theme">The active theme, or null.</param>
    /// <returns>The resolved color.</returns>
    internal static Color ProbeResolveColor(ControlColor value, Theme? theme) => ResolveColor(value, theme);

    /// <summary>Combines two invalidation impacts through the protected static seam.</summary>
    /// <param name="left">The first validated impact.</param>
    /// <param name="right">The second validated impact.</param>
    /// <returns>The stronger of the two impacts.</returns>
    internal static InvalidationImpact ProbeMaximumImpact(InvalidationImpact left, InvalidationImpact right) =>
        MaximumImpact(left, right);
}
