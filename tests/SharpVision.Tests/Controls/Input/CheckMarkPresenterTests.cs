// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies CheckMarkPresenter.Format's exact terminal-cell output for every mark family,
/// state, and Unicode-width degradation path.</summary>
public sealed class CheckMarkPresenterTests
{
    /// <summary>Verifies Brackets wraps the resolved glyph in a leading and trailing bracket for
    /// every documented state.</summary>
    [Fact]
    public void Format_WhenBrackets_WrapsResolvedGlyphInBrackets()
    {
        var mark = CheckMark.Brackets;

        mark.Format(true, Ambiguous.Narrow).ShouldBe("[✓]");
        mark.Format(false, Ambiguous.Narrow).ShouldBe("[ ]");
        mark.Format(null, Ambiguous.Narrow).ShouldBe("[─]");
    }

    /// <summary>Verifies Tick and Square return a bare one-cell glyph without bracket wrapping.</summary>
    [Theory]
    [InlineData(true, "✓")]
    [InlineData(false, "○")]
    [InlineData(null, "−")]
    public void Format_WhenTick_ReturnsBareResolvedGlyph(bool? state, string expected) =>
        CheckMark.Tick.Format(state, Ambiguous.Narrow).ShouldBe(expected);

    /// <summary>Verifies Square returns a bare one-cell glyph without bracket wrapping.</summary>
    [Theory]
    [InlineData(true, "☑")]
    [InlineData(false, "☐")]
    [InlineData(null, "◩")]
    public void Format_WhenSquare_ReturnsBareResolvedGlyph(bool? state, string expected) =>
        CheckMark.Square.Format(state, Ambiguous.Narrow).ShouldBe(expected);

    /// <summary>Verifies each state degrades to its own state-specific fallback - never the
    /// unchecked fallback - when the configured glyph is East Asian Ambiguous and the active
    /// policy widens it past one cell, matching CheckMarkPresenter's documented per-state
    /// degradation contract.</summary>
    [Fact]
    public void Format_WhenGlyphIsAmbiguousWidthUnderWidePolicy_DegradesToItsOwnStateFallback()
    {
        // U+00B7 MIDDLE DOT is one cell under Narrow (valid at construction) and two cells under
        // Wide, exercising the runtime degradation path independently of construction validation.
        var glyphs = new CheckBoxGlyphs(new Rune('·'), new Rune('·'), new Rune('·'));
        var mark = new CheckMark(CheckBoxMarkStyle.Brackets, glyphs);

        // Sanity: the configured glyph draws directly under Narrow.
        mark.Format(true, Ambiguous.Narrow).ShouldBe("[·]");

        // Under Wide, every state falls back to its own configured fallback rune, not a shared one.
        mark.Format(true, Ambiguous.Wide).ShouldBe("[x]");
        mark.Format(false, Ambiguous.Wide).ShouldBe("[ ]");
        mark.Format(null, Ambiguous.Wide).ShouldBe("[-]");
    }

    /// <summary>Verifies the same ambiguous-width degradation applies to the one-cell Tick and
    /// Square families, using their own configured fallbacks instead of the bracket family's.</summary>
    [Theory]
    [InlineData(CheckBoxMarkStyle.Tick, true, "x")]
    [InlineData(CheckBoxMarkStyle.Tick, false, "o")]
    [InlineData(CheckBoxMarkStyle.Tick, null, "-")]
    [InlineData(CheckBoxMarkStyle.Square, true, "x")]
    [InlineData(CheckBoxMarkStyle.Square, false, "o")]
    [InlineData(CheckBoxMarkStyle.Square, null, "-")]
    public void Format_WhenNonBracketFamilyGlyphIsAmbiguousWidthUnderWidePolicy_DegradesToOwnFallback(
        CheckBoxMarkStyle style,
        bool? state,
        string expected)
    {
        var glyphs = new CheckBoxGlyphs(new Rune('·'), new Rune('·'), new Rune('·'));
        var mark = new CheckMark(style, glyphs);

        mark.Format(state, Ambiguous.Wide).ShouldBe(expected);
    }
}
