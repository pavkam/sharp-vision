// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies the immutable radio-button presentation record: its two code-owned presets,
/// its declared one-hop chromeless fallback to <see cref="InputStyle"/>'s "input" key with a
/// checked-state accent default, and its invalidation policy.</summary>
public sealed class RadioButtonStyleTests
{
    /// <summary>Verifies Default resolves to the exact three-cell parenthesized preset.</summary>
    [Fact]
    public void Default_ResolvesParenthesesWithExactThreeCellMarks()
    {
        var actual = RadioButtonStyle.Default;

        actual.ShouldBe(RadioButtonStyle.Parentheses);
        actual.UncheckedText.ShouldBe("( )");
        actual.CheckedText.ShouldBe("(•)");
        actual.MarkWidth.ShouldBe(3);
    }

    /// <summary>Verifies the compact glyph preset retains the established radio marks and is chromeless.</summary>
    [Fact]
    public void Glyph_UsesOneCellMarksAndIsChromeless()
    {
        var actual = RadioButtonStyle.Glyph;

        actual.MarkStyle.ShouldBe(RadioButtonMarkStyle.Circle);
        actual.UncheckedText.ShouldBe("○");
        actual.CheckedText.ShouldBe("◉");
        actual.MarkWidth.ShouldBe(1);
        actual.Border.ShouldBe(ControlStyle.NoBorder);
    }

    /// <summary>Verifies equality compares every record member structurally.</summary>
    [Fact]
    public void Equality_WhenEveryMemberMatches_IsEqual()
    {
        var baseline = RadioButtonStyle.Parentheses;
        var equivalent = new RadioButtonStyle(baseline.Face, baseline.Border, baseline.Shadow, baseline.MarkStyle, baseline.Glyphs);

        equivalent.ShouldBe(baseline);
        equivalent.ShouldNotBe(RadioButtonStyle.Glyph);
    }

    /// <summary>Verifies an undefined mark style is rejected both by the constructor and by a
    /// <c>with</c> expression, since MarkStyle validates in its own init accessor.</summary>
    [Fact]
    public void MarkStyle_WhenUndefined_ThrowsFromConstructorAndWith()
    {
        var baseline = RadioButtonStyle.Parentheses;

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new RadioButtonStyle(baseline.Face, baseline.Border, baseline.Shadow, (RadioButtonMarkStyle) 99, baseline.Glyphs));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            baseline with { MarkStyle = (RadioButtonMarkStyle) 99 });
    }

    /// <summary>Verifies radio glyphs reject non-one-cell values.</summary>
    [Theory]
    [InlineData(0x4E16)]
    [InlineData(0)]
    public void Glyphs_WhenGlyphIsWideOrControl_Throws(int scalar)
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new RadioButtonGlyphs(new Rune(scalar), new Rune('x')));

        exception.ParamName.ShouldBe("uncheckedMark");
    }

    /// <summary>Verifies an unauthored theme falls back to Input's chromeless defaults, with the
    /// checked state defaulting to the accent foreground.</summary>
    [Fact]
    public void Definition_Resolve_WhenThemeDoesNotAuthorRadioButton_FallsBackToInputWithCheckedAccent()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var normal = RadioButtonStyle.Definition.Resolve(null, theme);
        var profile = RadioButtonStyle.Definition.Appearance!(normal, theme);

        normal.Border.ShouldBe(ControlStyle.NoBorder);
        profile.Resolve(VisualState.Checked).Face.Foreground.ShouldBe((ControlColor) SemanticColor.Accent);
    }

    /// <summary>Verifies every bundled theme resolves RadioButton's mark style and glyph pair to
    /// exactly the glyph family that theme's own root-level "glyphs" field declares - the same
    /// values the deleted "radioButton" section used to author directly for the curated set (see
    /// themes.md#glyph-families).</summary>
    [Fact]
    public void EveryTheme_ResolvesTheThemesDeclaredGlyphFamily()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var resolved = RadioButtonStyle.Definition.Resolve(null, theme);

            resolved.MarkStyle.ShouldBe(theme.Glyphs.RadioButton.MarkStyle, slug);
            resolved.Glyphs.ShouldBe(theme.Glyphs.RadioButton.Glyphs, slug);
        }
    }

    /// <summary>Verifies a MarkWidth change (parentheses vs. one-cell family) is measure-affecting.</summary>
    [Fact]
    public void Definition_Compare_WhenMarkWidthChanges_IsMeasure()
    {
        RadioButtonStyle.Definition.Compare(RadioButtonStyle.Parentheses, null, RadioButtonStyle.Glyph, null)
            .ShouldBe(InvalidationImpact.Measure);
    }

    /// <summary>Verifies an AffixGap-only change is measure-affecting, since a wider or narrower
    /// gap shifts the affix and caption apart without touching MarkWidth, MarkStyle, or Glyphs.</summary>
    [Fact]
    public void Definition_Compare_WhenAffixGapChanges_IsMeasure()
    {
        var previous = RadioButtonStyle.Parentheses;
        var current = previous with { AffixGap = previous.AffixGap + 1 };

        RadioButtonStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Measure);
    }
}
