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
        actual.MarkGap.ShouldBe(1);
        actual.MarkPlacement.ShouldBe(SelectionMarkPlacement.Leading);
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

    /// <summary>Verifies the shared mark placement rejects undefined values before a style copy
    /// can expose invalid layout intent.</summary>
    [Fact]
    public void MarkPlacement_WhenUndefined_ThrowsFromWith()
    {
        var baseline = RadioButtonStyle.Parentheses;

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            baseline with { MarkPlacement = (SelectionMarkPlacement) 99 });
    }

    /// <summary>Verifies the mark-to-caption gap is bounded to the compact terminal-cell range.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void MarkGap_WhenOutsideSupportedRange_Throws(int value)
    {
        var baseline = RadioButtonStyle.Parentheses;

        _ = Should.Throw<ArgumentOutOfRangeException>(() => baseline with { MarkGap = value });
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

    /// <summary>Verifies a mark-gap change remeasures the caption reservation.</summary>
    [Fact]
    public void Definition_Compare_WhenMarkGapChanges_IsMeasure()
    {
        var previous = RadioButtonStyle.Parentheses;
        var current = previous with { MarkGap = previous.MarkGap + 1 };

        RadioButtonStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Measure);
    }

    /// <summary>Verifies moving the mark to the other caption edge rearranges without changing
    /// intrinsic size.</summary>
    [Fact]
    public void Definition_Compare_WhenMarkPlacementChanges_IsArrange()
    {
        var previous = RadioButtonStyle.Parentheses;
        var current = previous with { MarkPlacement = SelectionMarkPlacement.Trailing };

        RadioButtonStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Arrange);
    }

    /// <summary>Verifies Focused/FocusWithin are visibly distinct from Normal under a bundled
    /// theme. RadioButton is a borderless leaf that falls back to <see cref="InputStyle"/> directly
    /// (not through <see cref="Theme.GetInteractiveControlStyleSet"/> or
    /// <see cref="Theme.GetFocusableControlStyleSet"/>), so it used to bypass
    /// <c>Theme.ApplyBorderlessFocusFallback</c> entirely. Every bundled theme maps
    /// focusedControl/focusedText to the exact same literal color as control/controlText, so
    /// Focused/FocusWithin resolved colorwise byte-identical to Normal - tabbing to a radio button
    /// gave no reliable visible indication that it was focused.</summary>
    [Theory]
    [InlineData(VisualState.Focused)]
    [InlineData(VisualState.FocusWithin)]
    public void ResolveAppearance_WhenFocusStateUnderBundledTheme_DiffersFromNormal(VisualState state)
    {
        using var radioButton = new RadioButton("Go");

        var normal = radioButton.ResolveAppearance(ThemeCatalog.Dark);
        var focused = radioButton.ResolveAppearance(ThemeCatalog.Dark, state);

        // The color collapse itself: Dark's focused colors resolve to the exact same literal RGB
        // as Normal's, which is exactly what would leave a borderless radio button with no visible
        // cue if the reverse-video safety net were not engaged.
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
        using var radioButton = new RadioButton("Go") { Style = RadioButtonStyle.Default };

        var normal = radioButton.ResolveAppearance(ThemeCatalog.Dark);
        var focused = radioButton.ResolveAppearance(ThemeCatalog.Dark, state);

        focused.Face.Foreground.ShouldBe(normal.Face.Foreground);
        focused.Face.Background.ShouldBe(normal.Face.Background);
        focused.ShouldNotBe(normal);
        focused.Face.Attributes.IsLiteral.ShouldBeTrue();
        focused.Face.Attributes.Literal.HasFlag(TerminalAttributes.Reverse).ShouldBeTrue();
    }
}
