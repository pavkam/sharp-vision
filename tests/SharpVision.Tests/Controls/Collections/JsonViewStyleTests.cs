// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies <see cref="JsonViewStyle"/>'s resolved chromatic token colors.</summary>
public sealed class JsonViewStyleTests
{
    /// <summary>Verifies the five chromatic JSON-kind roles - key, index, string, number, and
    /// boolean - resolve pairwise distinct colors under both the default dark and default light
    /// themes, proving the chromatic remap actually differentiates JSON content by color instead
    /// of merely renaming a palette that still collides two of them, the way Info previously
    /// collided IndexColor with the Cyan KeyColor in several bundled themes.</summary>
    [Theory]
    [MemberData(nameof(Themes))]
    public void Default_WhenResolvedAgainstABundledTheme_DifferentiatesKeyIndexStringNumberAndBoolean(Theme theme)
    {
        var style = JsonViewStyle.Definition.Resolve(null, theme);

        var colors = new[]
        {
            ControlBase.ResolveColor(style.KeyColor, theme),
            ControlBase.ResolveColor(style.IndexColor, theme),
            ControlBase.ResolveColor(style.StringColor, theme),
            ControlBase.ResolveColor(style.NumberColor, theme),
            ControlBase.ResolveColor(style.BooleanColor, theme)
        };

        colors.Distinct().Count().ShouldBe(colors.Length);
    }

    /// <summary>Verifies <see cref="JsonViewStyle.BooleanColor"/> and <see cref="JsonViewStyle.NullColor"/>
    /// resolve to the same color under both the default dark and default light themes. <c>true</c>,
    /// <c>false</c>, and <c>null</c> are the three JSON literal tokens, and they deliberately share
    /// one hue as one "literal" family: each token already disambiguates itself by its own text, so
    /// there is no need to spend a second distinct color telling them apart the way the chromatic
    /// key, index, string, number, and boolean roles above must be.</summary>
    [Theory]
    [MemberData(nameof(Themes))]
    public void Default_WhenResolvedAgainstABundledTheme_SharesOneColorAcrossBooleanAndNull(Theme theme)
    {
        var style = JsonViewStyle.Definition.Resolve(null, theme);

        ControlBase.ResolveColor(style.BooleanColor, theme).ShouldBe(ControlBase.ResolveColor(style.NullColor, theme));
    }

    /// <summary>Supplies the default dark and default light themes.</summary>
    public static TheoryData<Theme> Themes =>
    [
        ThemeCatalog.Dark,
        ThemeCatalog.White
    ];
}
