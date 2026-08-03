// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies complete progress-bar presentations and partial style composition.</summary>
public sealed class ProgressBarStyleWithTests
{
    /// <summary>Verifies the zero-initialized style resolves to the standard preset.</summary>
    [Fact]
    public void Default_WhenStyleIsZeroInitialized_ResolvesStandard()
    {
        var actual = default(ProgressBarStyle);

        actual.ShouldBe(ProgressBarStyle.Default);
        actual.FillColor.ShouldBe((ColorValue) ThemeColor.Accent);
        actual.TrackColor.ShouldBe((ColorValue) ThemeColor.Muted);
        actual.IndeterminateColor.ShouldBe((ColorValue) ThemeColor.Accent);
        actual.Glyphs.ShouldBe(ProgressBarGlyphs.Default);
    }

    /// <summary>Verifies equality compares resolved profiles rather than profile identity.</summary>
    [Fact]
    public void Equality_WhenCompleteValuesAreEquivalent_IsSemantic()
    {
        var baseline = ProgressBarStyle.Default;
        var equivalent = new ProgressBarStyle(
            baseline.FillColor,
            baseline.TrackColor,
            baseline.IndeterminateColor,
            baseline.Glyphs,
            baseline.Appearance);

        baseline.ShouldBe(equivalent);
        baseline.GetHashCode().ShouldBe(equivalent.GetHashCode());
    }

    /// <summary>Verifies a supplied fill color preserves omitted complete members.</summary>
    [Fact]
    public void Apply_WhenOnlyFillColorIsSupplied_PreservesOmittedMembers()
    {
        var baseline = ProgressBarStyle.Default;
        var actual = baseline.With(fillColor: Color.Rgb(255, 0, 0));

        actual.FillColor.ShouldBe((ColorValue) Color.Rgb(255, 0, 0));
        actual.TrackColor.ShouldBe(baseline.TrackColor);
        actual.IndeterminateColor.ShouldBe(baseline.IndeterminateColor);
        actual.Glyphs.ShouldBe(baseline.Glyphs);
        actual.Appearance.ShouldBe(baseline.Appearance);
    }

    /// <summary>Verifies appearance contributions compose without replacing structural members.</summary>
    [Fact]
    public void Apply_WhenAppearanceIsSupplied_ComposesProfile()
    {
        var baseline = ProgressBarStyle.Default;
        var actual = baseline.With(
            appearance: new AppearanceProfileSet(
                focused: new AppearanceSet(face: new FaceSet(foreground: ThemeColor.FocusedText))));

        actual.FillColor.ShouldBe(baseline.FillColor);
        actual.Appearance.Normal.ShouldBe(baseline.Appearance.Normal);
        actual.Appearance.Focused.Face.ShouldNotBeNull().Foreground.ShouldBe(ThemeColor.FocusedText);
    }

    /// <summary>Verifies every progress-bar part foreground rejects transparent paint.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenPartColorIsTransparent_Throws(int part)
    {
        var baseline = ProgressBarStyle.Default;
        ColorValue transparent = Color.Transparent;

        var exception = Should.Throw<ArgumentException>(() => new ProgressBarStyle(
            part == 0 ? transparent : baseline.FillColor,
            part == 1 ? transparent : baseline.TrackColor,
            part == 2 ? transparent : baseline.IndeterminateColor,
            baseline.Glyphs,
            baseline.Appearance));

        exception.ParamName.ShouldBe(part switch
        {
            0 => "fillColor",
            1 => "trackColor",
            _ => "indeterminateColor"
        });
    }

    /// <summary>Verifies partial style construction rejects transparent part foregrounds immediately.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void With_WhenPartColorIsTransparent_Throws(int part)
    {
        ColorValue transparent = Color.Transparent;

        var exception = Should.Throw<ArgumentException>(() => ProgressBarStyle.Default.With(
            fillColor: part == 0 ? transparent : null,
            trackColor: part == 1 ? transparent : null,
            indeterminateColor: part == 2 ? transparent : null));

        exception.ParamName.ShouldBe(part switch
        {
            0 => "fillColor",
            1 => "trackColor",
            _ => "indeterminateColor"
        });
    }

    /// <summary>Verifies progress-bar glyph families reject non-one-cell values.</summary>
    [Fact]
    public void Glyphs_WhenGlyphIsWideOrControl_Throws()
    {
        var exception = Should.Throw<ArgumentException>(() => new ProgressBarGlyphs(
            new Rune('世'), new Rune('.'), new Rune('?')));

        exception.ParamName.ShouldBe("fill");
    }
}
