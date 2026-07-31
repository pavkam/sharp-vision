// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies complete slider presentations and partial style composition.</summary>
public sealed class SliderStyleSetTests
{
    /// <summary>Verifies the zero-initialized style resolves to the standard preset.</summary>
    [Fact]
    public void Default_WhenStyleIsZeroInitialized_ResolvesStandard()
    {
        var actual = default(SliderStyle);

        actual.ShouldBe(SliderStyle.Default);
        actual.FillColor.ShouldBe((ColorValue) ThemeColor.Accent);
        actual.TrackColor.ShouldBe((ColorValue) ThemeColor.Muted);
        actual.ThumbColor.ShouldBe((ColorValue) ThemeColor.Accent);
        actual.Glyphs.ShouldBe(SliderGlyphs.Default);
    }

    /// <summary>Verifies equality compares resolved profiles rather than profile identity.</summary>
    [Fact]
    public void Equality_WhenCompleteValuesAreEquivalent_IsSemantic()
    {
        var baseline = SliderStyle.Default;
        var equivalent = new SliderStyle(
            baseline.FillColor,
            baseline.TrackColor,
            baseline.ThumbColor,
            baseline.Glyphs,
            baseline.Appearance);

        baseline.ShouldBe(equivalent);
        baseline.GetHashCode().ShouldBe(equivalent.GetHashCode());
    }

    /// <summary>Verifies a supplied fill color preserves omitted complete members.</summary>
    [Fact]
    public void Apply_WhenOnlyFillColorIsSupplied_PreservesOmittedMembers()
    {
        var baseline = SliderStyle.Default;
        var set = new SliderStyleSet(fillColor: Color.Rgb(255, 0, 0));

        var actual = set.Apply(baseline);

        actual.FillColor.ShouldBe((ColorValue) Color.Rgb(255, 0, 0));
        actual.TrackColor.ShouldBe(baseline.TrackColor);
        actual.ThumbColor.ShouldBe(baseline.ThumbColor);
        actual.Glyphs.ShouldBe(baseline.Glyphs);
        actual.Appearance.ShouldBe(baseline.Appearance);
    }

    /// <summary>Verifies appearance contributions compose without replacing structural members.</summary>
    [Fact]
    public void Apply_WhenAppearanceIsSupplied_ComposesProfile()
    {
        var baseline = SliderStyle.Default;
        var set = new SliderStyleSet(
            appearance: new AppearanceProfileSet(
                focused: new AppearanceSet(face: new FaceSet(foreground: ThemeColor.FocusedText))));

        var actual = set.Apply(baseline);

        actual.FillColor.ShouldBe(baseline.FillColor);
        actual.Appearance.Normal.ShouldBe(baseline.Appearance.Normal);
        actual.Appearance.Focused.Face.ShouldNotBeNull().Foreground.ShouldBe(ThemeColor.FocusedText);
    }

    /// <summary>Verifies every slider part foreground rejects transparent paint.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenPartColorIsTransparent_Throws(int part)
    {
        var baseline = SliderStyle.Default;
        ColorValue transparent = Color.Transparent;

        var exception = Should.Throw<ArgumentException>(() => new SliderStyle(
            part == 0 ? transparent : baseline.FillColor,
            part == 1 ? transparent : baseline.TrackColor,
            part == 2 ? transparent : baseline.ThumbColor,
            baseline.Glyphs,
            baseline.Appearance));

        exception.ParamName.ShouldBe(part switch
        {
            0 => "fillColor",
            1 => "trackColor",
            _ => "thumbColor"
        });
    }

    /// <summary>Verifies partial style construction rejects transparent part foregrounds immediately.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void StyleSet_WhenPartColorIsTransparent_ThrowsAtConstruction(int part)
    {
        ColorValue transparent = Color.Transparent;

        var exception = Should.Throw<ArgumentException>(() => new SliderStyleSet(
            fillColor: part == 0 ? transparent : null,
            trackColor: part == 1 ? transparent : null,
            thumbColor: part == 2 ? transparent : null));

        exception.ParamName.ShouldBe(part switch
        {
            0 => "fillColor",
            1 => "trackColor",
            _ => "thumbColor"
        });
    }

    /// <summary>Verifies slider glyph families reject non-one-cell values.</summary>
    [Fact]
    public void Glyphs_WhenGlyphIsWideOrControl_Throws()
    {
        var exception = Should.Throw<ArgumentException>(() => new SliderGlyphs(
            new Rune('世'), new Rune('='), new Rune('.'), new Rune('='), new Rune('#')));

        exception.ParamName.ShouldBe("horizontalTrack");
    }
}
