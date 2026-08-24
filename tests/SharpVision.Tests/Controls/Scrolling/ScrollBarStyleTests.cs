// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Scrolling;

/// <summary>Verifies complete scrollbar presentations and their glyph family.</summary>
public sealed class ScrollBarStyleTests
{
    /// <summary>Verifies Default resolves to the full block preset.</summary>
    [Fact]
    public void Default_ResolvesFullBlock()
    {
        var actual = ScrollBarStyle.Default;

        actual.ShouldBe(ScrollBarStyle.FullBlock);
        actual.Chrome.ShouldBe(ScrollBarChrome.Full);
        actual.Fill.ShouldBe(ScrollBarFill.Block);
        actual.Glyphs.ShouldBe(ScrollBarGlyphs.Default);
    }

    /// <summary>Verifies every bundled theme resolves ScrollBar's chrome, fill, and glyph set to
    /// exactly the glyph family that theme's own root-level "glyphs" field declares - the same
    /// values the deleted "scrollBar" section used to author directly for the curated set (see
    /// themes.md#glyph-families).</summary>
    [Fact]
    public void EveryTheme_ResolvesTheThemesDeclaredGlyphFamily()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var resolved = ScrollBarStyle.Definition.Resolve(null, theme);

            resolved.Chrome.ShouldBe(theme.Glyphs.ScrollBar.Chrome, slug);
            resolved.Fill.ShouldBe(theme.Glyphs.ScrollBar.Fill, slug);
            resolved.Glyphs.ShouldBe(theme.Glyphs.ScrollBar.Glyphs, slug);
        }
    }

    /// <summary>Verifies the named presets retain all chrome and fill combinations.</summary>
    [Theory]
    [InlineData(ScrollBarChrome.Full, ScrollBarFill.Block)]
    [InlineData(ScrollBarChrome.Full, ScrollBarFill.Line)]
    [InlineData(ScrollBarChrome.Thin, ScrollBarFill.Block)]
    [InlineData(ScrollBarChrome.Thin, ScrollBarFill.Line)]
    public void Preset_WhenResolved_UsesRequestedChromeAndFill(ScrollBarChrome chrome, ScrollBarFill fill)
    {
        var actual = (chrome, fill) switch
        {
            (ScrollBarChrome.Full, ScrollBarFill.Block) => ScrollBarStyle.FullBlock,
            (ScrollBarChrome.Full, ScrollBarFill.Line) => ScrollBarStyle.FullLine,
            (ScrollBarChrome.Thin, ScrollBarFill.Block) => ScrollBarStyle.ThinBlock,
            _ => ScrollBarStyle.ThinLine
        };

        actual.Chrome.ShouldBe(chrome);
        actual.Fill.ShouldBe(fill);
    }

    /// <summary>Verifies equality compares every record member structurally.</summary>
    [Fact]
    public void Equality_WhenEveryMemberMatches_IsEqual()
    {
        var baseline = ScrollBarStyle.Default;
        var equivalent = new ScrollBarStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            baseline.Chrome,
            baseline.Fill,
            baseline.Glyphs,
            baseline.TrackColor,
            baseline.ThumbColor,
            baseline.ButtonColor);

        baseline.ShouldBe(equivalent);
        baseline.GetHashCode().ShouldBe(equivalent.GetHashCode());
    }

    /// <summary>Verifies a supplied fill preserves omitted complete members.</summary>
    [Fact]
    public void With_WhenOnlyFillIsSupplied_PreservesOmittedMembers()
    {
        var baseline = ScrollBarStyle.FullBlock;
        var actual = baseline with { Fill = ScrollBarFill.Line };

        actual.Fill.ShouldBe(ScrollBarFill.Line);
        actual.Chrome.ShouldBe(baseline.Chrome);
        actual.Glyphs.ShouldBe(baseline.Glyphs);
        actual.TrackColor.ShouldBe(baseline.TrackColor);
        actual.ThumbColor.ShouldBe(baseline.ThumbColor);
        actual.ButtonColor.ShouldBe(baseline.ButtonColor);
    }

    /// <summary>Verifies invalid enums are rejected from the constructor and from a <c>with</c> expression.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnumMember_WhenUndefined_ThrowsFromConstructorAndWith(bool invalidChrome)
    {
        var baseline = ScrollBarStyle.Default;

        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ScrollBarStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            invalidChrome ? (ScrollBarChrome) 99 : baseline.Chrome,
            invalidChrome ? baseline.Fill : (ScrollBarFill) 99,
            baseline.Glyphs,
            baseline.TrackColor,
            baseline.ThumbColor,
            baseline.ButtonColor));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => invalidChrome
            ? baseline with { Chrome = (ScrollBarChrome) 99 }
            : baseline with { Fill = (ScrollBarFill) 99 });
    }

    /// <summary>Verifies every scrollbar part foreground rejects transparent paint from the constructor.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenPartColorIsTransparent_Throws(int part)
    {
        var baseline = ScrollBarStyle.Default;
        ControlColor transparent = Color.Transparent;

        _ = Should.Throw<ArgumentException>(() => new ScrollBarStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            baseline.Chrome,
            baseline.Fill,
            baseline.Glyphs,
            part == 0 ? transparent : baseline.TrackColor,
            part == 1 ? transparent : baseline.ThumbColor,
            part == 2 ? transparent : baseline.ButtonColor));
    }

    /// <summary>Verifies a <c>with</c> expression rejects a transparent part foreground too, since
    /// each color property validates in its own init accessor.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void With_WhenPartColorIsTransparent_Throws(int part)
    {
        var baseline = ScrollBarStyle.Default;
        ControlColor transparent = Color.Transparent;

        _ = Should.Throw<ArgumentException>(() => part switch
        {
            0 => baseline with { TrackColor = transparent },
            1 => baseline with { ThumbColor = transparent },
            _ => baseline with { ButtonColor = transparent }
        });
    }

    /// <summary>Verifies scrollbar glyph families reject non-one-cell values.</summary>
    [Theory]
    [InlineData(0x4E16)]
    [InlineData(0)]
    public void Glyphs_WhenGlyphIsWideOrControl_Throws(int scalar)
    {
        var exception = Should.Throw<ArgumentException>(() => CreateGlyphs(new Rune(scalar)));

        exception.ParamName.ShouldBe("verticalDecrement");
    }

    private static ScrollBarGlyphs CreateGlyphs(Rune verticalDecrement) => new(
        verticalDecrement,
        new Rune('v'),
        new Rune('<'),
        new Rune('>'),
        new Rune('.'),
        new Rune('#'),
        new Rune('-'),
        new Rune('='),
        new Rune('|'),
        new Rune('#'));
}
