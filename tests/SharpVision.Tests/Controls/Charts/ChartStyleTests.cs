// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Charts;

/// <summary>Verifies <see cref="ChartStyle"/>'s resolved six-color series palette.</summary>
public sealed class ChartStyleTests
{
    /// <summary>Verifies the code-owned default assigns the six-color chromatic sequence in
    /// order - Blue, Green, Yellow, Magenta, Cyan, then Red last since it reads as an alarm.</summary>
    [Fact]
    public void Default_WhenSeriesColorsAreRead_UsesTheChromaticSequence()
    {
        var style = ChartStyle.Default;

        style.PrimaryColor.SemanticColor.ShouldBe(SemanticColor.Blue);
        style.SecondaryColor.SemanticColor.ShouldBe(SemanticColor.Green);
        style.TertiaryColor.SemanticColor.ShouldBe(SemanticColor.Yellow);
        style.QuaternaryColor.SemanticColor.ShouldBe(SemanticColor.Magenta);
        style.QuinaryColor.SemanticColor.ShouldBe(SemanticColor.Cyan);
        style.SenaryColor.SemanticColor.ShouldBe(SemanticColor.Red);
    }

    /// <summary>Verifies all six series colors resolve pairwise distinct under both the default
    /// dark and default light themes, proving the chromatic remap actually differentiates six
    /// series rather than merely renaming a palette that still collides two of them.</summary>
    [Theory]
    [MemberData(nameof(Themes))]
    public void GetSeriesColor_WhenIndexed0Through5_ResolvesPairwiseDistinctColors(Theme theme)
    {
        var style = ChartStyle.Definition.Resolve(null, theme);

        var colors = Enumerable.Range(0, 6)
            .Select(index => ControlBase.ResolveColor(style.GetSeriesColor(index), theme))
            .ToList();

        colors.Distinct().Count().ShouldBe(colors.Count);
    }

    /// <summary>Supplies the default dark and default light themes.</summary>
    public static TheoryData<Theme> Themes =>
    [
        ThemeCatalog.Dark,
        ThemeCatalog.White
    ];
}
