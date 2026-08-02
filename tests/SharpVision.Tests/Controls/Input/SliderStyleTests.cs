// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies complete slider presentations and their glyph family.</summary>
public sealed class SliderStyleTests
{
    /// <summary>Verifies the zero-initialized style resolves to the code-owned defaults.</summary>
    [Fact]
    public void Default_WhenStyleIsZeroInitialized_ResolvesCodeOwnedDefaults()
    {
        var actual = default(SliderStyle);

        actual.ShouldBe(SliderStyle.Default);
        actual.FillColor.ShouldBe(ThemeColor.Accent);
        actual.TrackColor.ShouldBe(ThemeColor.Muted);
        actual.ThumbColor.ShouldBe(ThemeColor.Accent);
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
            Copy(baseline.Appearance));

        baseline.ShouldBe(equivalent);
        baseline.GetHashCode().ShouldBe(equivalent.GetHashCode());
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

    /// <summary>Verifies construction rejects a missing complete appearance profile.</summary>
    [Fact]
    public void Constructor_WhenAppearanceIsNull_Throws()
    {
        var baseline = SliderStyle.Default;

        var exception = Should.Throw<ArgumentNullException>(() => new SliderStyle(
            baseline.FillColor,
            baseline.TrackColor,
            baseline.ThumbColor,
            baseline.Glyphs,
            null!));

        exception.ParamName.ShouldBe("appearance");
    }

    /// <summary>Verifies slider glyph families reject non-one-cell values.</summary>
    [Theory]
    [InlineData(0x4E16)]
    [InlineData(0)]
    public void Glyphs_WhenGlyphIsWideOrControl_Throws(int scalar)
    {
        var exception = Should.Throw<ArgumentException>(() => CreateGlyphs(new Rune(scalar)));

        exception.ParamName.ShouldBe("horizontalTrack");
    }

    /// <summary>Verifies a public ambiguous-width override repairs through its slot's portable fallback.</summary>
    [Fact]
    public void Glyphs_WhenPublicGlyphBecomesWide_UsesSlotFallback()
    {
        var glyphs = CreateUniformGlyphs(new Rune('·'));

        new[]
        {
            glyphs.HorizontalTrackGlyph.Fallback,
            glyphs.HorizontalFillGlyph.Fallback,
            glyphs.VerticalTrackGlyph.Fallback,
            glyphs.VerticalFillGlyph.Fallback,
            glyphs.ThumbGlyph.Fallback
        }.ShouldBe([new Rune('.'), new Rune('='), new Rune('.'), new Rune('='), new Rune('#')]);
        glyphs.HorizontalTrack.Resolve(glyphs.HorizontalTrackGlyph.Fallback, Ambiguous.Wide).ShouldBe(new Rune('.'));
    }

    /// <summary>Verifies equality and hashing include repair behavior as well as public primary runes.</summary>
    [Fact]
    public void Equality_WhenFallbackBehaviorDiffers_IsNotEqual()
    {
        var baseline = CreateUniformGlyphs(new Rune('·'));
        var differentFallback = new SliderGlyphs(
            new ControlGlyph(baseline.HorizontalTrack, new Rune('?')),
            baseline.HorizontalFillGlyph,
            baseline.VerticalTrackGlyph,
            baseline.VerticalFillGlyph,
            baseline.ThumbGlyph);

        baseline.ShouldNotBe(differentFallback);
        baseline.GetHashCode().ShouldNotBe(differentFallback.GetHashCode());
    }

    private static SliderGlyphs CreateGlyphs(Rune horizontalTrack) => new(
        horizontalTrack,
        new Rune('='),
        new Rune('.'),
        new Rune('='),
        new Rune('#'));

    private static SliderGlyphs CreateUniformGlyphs(Rune glyph) => new(glyph, glyph, glyph, glyph, glyph);

    private static ThemeProfile Copy(ThemeProfile profile) => new(
        profile.Normal,
        profile.PointerOver,
        profile.FocusWithin,
        profile.Focused,
        profile.Current,
        profile.Selected,
        profile.Checked,
        profile.Indeterminate,
        profile.Pressed,
        profile.Disabled);
}
