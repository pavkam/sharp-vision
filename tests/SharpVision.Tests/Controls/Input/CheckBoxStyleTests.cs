// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies the immutable checkbox presentation record: its three code-owned presets,
/// its declared one-hop chromeless fallback to <see cref="InputStyle"/>'s "input" key,
/// and its invalidation policy.</summary>
public sealed class CheckBoxStyleTests
{
    /// <summary>Verifies Default resolves to the three-cell bracket preset.</summary>
    [Fact]
    public void Default_ResolvesBrackets()
    {
        CheckBoxStyle.Default.ShouldBe(CheckBoxStyle.Brackets);
        CheckBoxStyle.Default.MarkStyle.ShouldBe(CheckBoxMarkStyle.Brackets);
        CheckBoxStyle.Default.MarkWidth.ShouldBe(3);
    }

    /// <summary>Verifies Brackets is chromeless (a selectable control, not a framed one) and
    /// retains the established horizontal-line indeterminate recipe.</summary>
    [Fact]
    public void Brackets_IsChromelessAndUsesHorizontalLineIndeterminateGlyph()
    {
        var actual = CheckBoxStyle.Brackets;

        actual.Border.ShouldBe(ControlStyle.NoBorder);
        actual.Shadow.ShouldBe(ControlStyle.NoShadow);
        actual.Glyphs.Indeterminate.ShouldBe(new Rune('─'));
    }

    /// <summary>Verifies non-bracket built-ins reserve one terminal cell.</summary>
    [Fact]
    public void Presets_WhenNotBracketed_ReserveOneCell()
    {
        CheckBoxStyle.Tick.MarkWidth.ShouldBe(1);
        CheckBoxStyle.Square.MarkWidth.ShouldBe(1);
    }

    /// <summary>Verifies equality compares every record member structurally.</summary>
    [Fact]
    public void Equality_WhenEveryMemberMatches_IsEqual()
    {
        var equivalent = new CheckBoxStyle(
            CheckBoxStyle.Default.Face,
            CheckBoxStyle.Default.Border,
            CheckBoxStyle.Default.Shadow,
            CheckBoxStyle.Default.MarkStyle,
            CheckBoxStyle.Default.Glyphs);

        equivalent.ShouldBe(CheckBoxStyle.Default);
        equivalent.ShouldNotBe(CheckBoxStyle.Tick);
    }

    /// <summary>Verifies an undefined mark style is rejected both by the constructor and by a
    /// <c>with</c> expression, since MarkStyle validates in its own init accessor.</summary>
    [Fact]
    public void MarkStyle_WhenUndefined_ThrowsFromConstructorAndWith()
    {
        var baseline = CheckBoxStyle.Default;

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new CheckBoxStyle(baseline.Face, baseline.Border, baseline.Shadow, (CheckBoxMarkStyle) 99, baseline.Glyphs));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            baseline with { MarkStyle = (CheckBoxMarkStyle) 99 });
    }

    /// <summary>Verifies an unauthored theme falls back to Input's chromeless defaults.</summary>
    [Fact]
    public void Definition_Resolve_WhenThemeDoesNotAuthorCheckBox_FallsBackToInput()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var resolved = CheckBoxStyle.Definition.Resolve(null, theme);

        resolved.Border.ShouldBe(ControlStyle.NoBorder);
        resolved.MarkStyle.ShouldBe(CheckBoxMarkStyle.Brackets);
    }

    /// <summary>Verifies a theme's own "checkBox" key can restyle MarkStyle directly - the
    /// standalone registrable section this used to require is retired.</summary>
    [Fact]
    public void Definition_Resolve_WhenThemeAuthorsCheckBoxMarkStyle_Applies()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(extraStyles: """, "checkBox": { "normal": { "markStyle": "tick" } } """));

        var resolved = CheckBoxStyle.Definition.Resolve(null, theme);

        resolved.MarkStyle.ShouldBe(CheckBoxMarkStyle.Tick);
    }

    /// <summary>Verifies every bundled theme resolves CheckBox's mark style and glyph trio to
    /// exactly the glyph family that theme's own root-level "glyphs" field declares - the same
    /// values the deleted "checkBox" section used to author directly for the curated set (see
    /// themes.md#glyph-families).</summary>
    [Fact]
    public void EveryTheme_ResolvesTheThemesDeclaredGlyphFamily()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var resolved = CheckBoxStyle.Definition.Resolve(null, theme);

            resolved.MarkStyle.ShouldBe(theme.Glyphs.CheckBox.MarkStyle, slug);
            resolved.Glyphs.ShouldBe(theme.Glyphs.CheckBox.Glyphs, slug);
        }
    }

    /// <summary>Verifies a MarkWidth change (bracket vs. one-cell family) is measure-affecting.</summary>
    [Fact]
    public void Definition_Compare_WhenMarkWidthChanges_IsMeasure()
    {
        CheckBoxStyle.Definition.Compare(CheckBoxStyle.Brackets, null, CheckBoxStyle.Tick, null)
            .ShouldBe(InvalidationImpact.Measure);
    }

    /// <summary>Verifies a glyph-only change at the same mark width is render-affecting only.</summary>
    [Fact]
    public void Definition_Compare_WhenOnlyGlyphsChange_IsRender()
    {
        var previous = CheckBoxStyle.Tick;
        var current = previous with { Glyphs = new CheckBoxGlyphs(new Rune('a'), new Rune('b'), new Rune('c')) };

        CheckBoxStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Render);
    }

    /// <summary>Verifies an AffixGap-only change is measure-affecting, since a wider or narrower
    /// gap shifts the affix and caption apart without touching MarkWidth, MarkStyle, or Glyphs.</summary>
    [Fact]
    public void Definition_Compare_WhenAffixGapChanges_IsMeasure()
    {
        var previous = CheckBoxStyle.Default;
        var current = previous with { AffixGap = previous.AffixGap + 1 };

        CheckBoxStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Measure);
    }
}
