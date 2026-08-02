// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies complete progress-bar presentations and their glyph family.</summary>
public sealed class ProgressBarStyleTests
{
    /// <summary>Verifies the zero-initialized style resolves to the code-owned defaults.</summary>
    [Fact]
    public void Default_WhenStyleIsZeroInitialized_ResolvesCodeOwnedDefaults()
    {
        var actual = default(ProgressBarStyle);

        actual.ShouldBe(ProgressBarStyle.Default);
        actual.FillColor.ShouldBe(ThemeColor.Accent);
        actual.TrackColor.ShouldBe(ThemeColor.Muted);
        actual.IndeterminateColor.ShouldBe(ThemeColor.Accent);
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
            Copy(baseline.Appearance));

        baseline.ShouldBe(equivalent);
        baseline.GetHashCode().ShouldBe(equivalent.GetHashCode());
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

    /// <summary>Verifies construction rejects a missing complete appearance profile.</summary>
    [Fact]
    public void Constructor_WhenAppearanceIsNull_Throws()
    {
        var baseline = ProgressBarStyle.Default;

        var exception = Should.Throw<ArgumentNullException>(() => new ProgressBarStyle(
            baseline.FillColor,
            baseline.TrackColor,
            baseline.IndeterminateColor,
            baseline.Glyphs,
            null!));

        exception.ParamName.ShouldBe("appearance");
    }

    /// <summary>Verifies progress-bar glyph families reject non-one-cell values.</summary>
    [Theory]
    [InlineData(0x4E16)]
    [InlineData(0)]
    public void Glyphs_WhenGlyphIsWideOrControl_Throws(int scalar)
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ProgressBarGlyphs(new Rune(scalar), new Rune('.'), new Rune('?')));

        exception.ParamName.ShouldBe("fill");
    }

    /// <summary>Verifies a public ambiguous-width override repairs through its slot's portable fallback.</summary>
    [Fact]
    public void Glyphs_WhenPublicGlyphBecomesWide_UsesSlotFallback()
    {
        var glyph = new Rune('·');
        var glyphs = new ProgressBarGlyphs(glyph, glyph, glyph);

        new[]
        {
            glyphs.FillGlyph.Fallback,
            glyphs.TrackGlyph.Fallback,
            glyphs.IndeterminateGlyph.Fallback
        }.ShouldBe([new Rune('#'), new Rune('.'), new Rune('?')]);
        glyphs.Fill.Resolve(glyphs.FillGlyph.Fallback, Ambiguous.Wide).ShouldBe(new Rune('#'));
    }

    /// <summary>Verifies equality and hashing include repair behavior as well as public primary runes.</summary>
    [Fact]
    public void Equality_WhenFallbackBehaviorDiffers_IsNotEqual()
    {
        var glyph = new Rune('·');
        var baseline = new ProgressBarGlyphs(glyph, glyph, glyph);
        var differentFallback = new ProgressBarGlyphs(
            new ControlGlyph(baseline.Fill, new Rune('!')),
            baseline.TrackGlyph,
            baseline.IndeterminateGlyph);

        baseline.ShouldNotBe(differentFallback);
        baseline.GetHashCode().ShouldNotBe(differentFallback.GetHashCode());
    }

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
