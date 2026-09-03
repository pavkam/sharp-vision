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
        CheckBoxStyle.Default.MarkGap.ShouldBe(1);
        CheckBoxStyle.Default.MarkPlacement.ShouldBe(SelectionMarkPlacement.Leading);
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

    /// <summary>Verifies the shared mark placement rejects undefined values before a style copy
    /// can expose invalid layout intent.</summary>
    [Fact]
    public void MarkPlacement_WhenUndefined_ThrowsFromWith()
    {
        var baseline = CheckBoxStyle.Default;

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            baseline with { MarkPlacement = (SelectionMarkPlacement) 99 });
    }

    /// <summary>Verifies the mark-to-caption gap is bounded to the compact terminal-cell range.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void MarkGap_WhenOutsideSupportedRange_Throws(int value)
    {
        var baseline = CheckBoxStyle.Default;

        _ = Should.Throw<ArgumentOutOfRangeException>(() => baseline with { MarkGap = value });
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

    /// <summary>Verifies a mark-gap change remeasures the caption reservation.</summary>
    [Fact]
    public void Definition_Compare_WhenMarkGapChanges_IsMeasure()
    {
        var previous = CheckBoxStyle.Default;
        var current = previous with { MarkGap = previous.MarkGap + 1 };

        CheckBoxStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Measure);
    }

    /// <summary>Verifies moving the mark to the other caption edge rearranges without changing
    /// intrinsic size.</summary>
    [Fact]
    public void Definition_Compare_WhenMarkPlacementChanges_IsArrange()
    {
        var previous = CheckBoxStyle.Default;
        var current = previous with { MarkPlacement = SelectionMarkPlacement.Trailing };

        CheckBoxStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Arrange);
    }

    /// <summary>Verifies Focused/FocusWithin are visibly distinct from Normal under a bundled
    /// theme. CheckBox is a borderless leaf that falls back to <see cref="InputStyle"/> directly
    /// (not through <see cref="Theme.GetInteractiveControlStyleSet"/> or
    /// <see cref="Theme.GetFocusableControlStyleSet"/>), so it used to bypass
    /// <c>Theme.ApplyBorderlessFocusFallback</c> entirely. Every bundled theme maps
    /// focusedControl/focusedText to the exact same literal color as control/controlText, so
    /// Focused/FocusWithin resolved colorwise byte-identical to Normal - tabbing to a checkbox gave
    /// no reliable visible indication that it was focused.</summary>
    [Theory]
    [InlineData(VisualState.Focused)]
    [InlineData(VisualState.FocusWithin)]
    public void ResolveAppearance_WhenFocusStateUnderBundledTheme_DiffersFromNormal(VisualState state)
    {
        using var checkBox = new CheckBox("Go");

        var normal = checkBox.ResolveAppearance(ThemeCatalog.Dark);
        var focused = checkBox.ResolveAppearance(ThemeCatalog.Dark, state);

        // The color collapse itself: Dark's focused colors resolve to the exact same literal RGB
        // as Normal's, which is exactly what would leave a borderless checkbox with no visible cue
        // if the reverse-video safety net were not engaged.
        focused.Face.Foreground.ShouldBe(normal.Face.Foreground);
        focused.Face.Background.ShouldBe(normal.Face.Background);

        // The safety net forces Reverse on top, making the two states visibly distinct in spite of
        // the color collapse above.
        focused.ShouldNotBe(normal);
        focused.Face.Attributes.IsLiteral.ShouldBeTrue();
        focused.Face.Attributes.Literal.HasFlag(TerminalAttributes.Reverse).ShouldBeTrue();
    }

    /// <summary>Verifies the same safety net engages when a complete local <see cref="Style"/> is
    /// assigned, which resolves through <c>Theme.BuildCodeOwnedStates</c> rather than
    /// <c>Theme.BuildFallbackAwareStates</c> - a differently-shaped resolution path (per-state
    /// overlay deltas rather than a complete per-state style) that needed its own fallback
    /// application rather than sharing the other method's.</summary>
    [Theory]
    [InlineData(VisualState.Focused)]
    [InlineData(VisualState.FocusWithin)]
    public void ResolveAppearance_WhenLocalStyleIsAssignedUnderBundledTheme_DiffersFromNormal(VisualState state)
    {
        using var checkBox = new CheckBox("Go") { Style = CheckBoxStyle.Default };

        var normal = checkBox.ResolveAppearance(ThemeCatalog.Dark);
        var focused = checkBox.ResolveAppearance(ThemeCatalog.Dark, state);

        focused.Face.Foreground.ShouldBe(normal.Face.Foreground);
        focused.Face.Background.ShouldBe(normal.Face.Background);
        focused.ShouldNotBe(normal);
        focused.Face.Attributes.IsLiteral.ShouldBeTrue();
        focused.Face.Attributes.Literal.HasFlag(TerminalAttributes.Reverse).ShouldBeTrue();
    }
}
