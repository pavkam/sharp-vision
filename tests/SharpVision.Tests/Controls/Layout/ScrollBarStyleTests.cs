// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

using System.Text.Json;

/// <summary>Verifies complete scrollbar presentations and partial style composition.</summary>
public sealed class ScrollBarStyleTests
{
    /// <summary>Verifies the zero-initialized style resolves to the full block preset.</summary>
    [Fact]
    public void Default_WhenStyleIsZeroInitialized_ResolvesFullBlock()
    {
        var actual = default(ScrollBarStyle);

        actual.ShouldBe(ScrollBarStyle.Default);
        actual.Chrome.ShouldBe(ScrollBarChrome.Full);
        actual.Fill.ShouldBe(ScrollBarFill.Block);
        actual.Glyphs.ShouldBe(ScrollBarGlyphs.Default);
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

    /// <summary>Verifies equality compares resolved profiles rather than profile identity.</summary>
    [Fact]
    public void Equality_WhenCompleteValuesAreEquivalent_IsSemantic()
    {
        var baseline = ScrollBarStyle.Default;
        var equivalent = new ScrollBarStyle(
            baseline.Chrome,
            baseline.Fill,
            baseline.Glyphs,
            baseline.TrackColor,
            baseline.ThumbColor,
            baseline.ButtonColor,
            Copy(baseline.Appearance));

        baseline.ShouldBe(equivalent);
        baseline.GetHashCode().ShouldBe(equivalent.GetHashCode());
    }

    /// <summary>Verifies a supplied fill preserves omitted complete members.</summary>
    [Fact]
    public void Apply_WhenOnlyFillIsSupplied_PreservesOmittedMembers()
    {
        var baseline = ScrollBarStyle.FullBlock;
        var set = new ScrollBarStyleSet(fill: ScrollBarFill.Line);

        var actual = set.Apply(baseline);

        actual.Fill.ShouldBe(ScrollBarFill.Line);
        actual.Chrome.ShouldBe(baseline.Chrome);
        actual.Glyphs.ShouldBe(baseline.Glyphs);
        actual.TrackColor.ShouldBe(baseline.TrackColor);
        actual.ThumbColor.ShouldBe(baseline.ThumbColor);
        actual.ButtonColor.ShouldBe(baseline.ButtonColor);
        actual.Appearance.ShouldBeSameAs(baseline.Appearance);
    }

    /// <summary>Verifies appearance contributions compose without replacing structural members.</summary>
    [Fact]
    public void Apply_WhenAppearanceIsSupplied_ComposesProfile()
    {
        var baseline = ScrollBarStyle.Default;
        var set = new ScrollBarStyleSet(
            appearance: new AppearanceProfileSet(
                focused: new AppearanceSet(face: new FaceSet(foreground: ThemeColor.FocusedText))));

        var actual = set.Apply(baseline);

        actual.Chrome.ShouldBe(baseline.Chrome);
        actual.Appearance.Normal.ShouldBe(baseline.Appearance.Normal);
        actual.Appearance.Focused.Face.ShouldNotBeNull().Foreground.ShouldBe(ThemeColor.FocusedText);
    }

    /// <summary>Verifies invalid enums are rejected before a complete style is created.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenEnumIsUndefined_Throws(bool invalidChrome)
    {
        var baseline = ScrollBarStyle.Default;

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ScrollBarStyle(
            invalidChrome ? (ScrollBarChrome) 99 : baseline.Chrome,
            invalidChrome ? baseline.Fill : (ScrollBarFill) 99,
            baseline.Glyphs,
            baseline.TrackColor,
            baseline.ThumbColor,
            baseline.ButtonColor,
            baseline.Appearance));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies every scrollbar part foreground rejects transparent paint.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenPartColorIsTransparent_Throws(int part)
    {
        var baseline = ScrollBarStyle.Default;
        ColorValue transparent = Color.Transparent;

        var exception = Should.Throw<ArgumentException>(() => new ScrollBarStyle(
            baseline.Chrome,
            baseline.Fill,
            baseline.Glyphs,
            part == 0 ? transparent : baseline.TrackColor,
            part == 1 ? transparent : baseline.ThumbColor,
            part == 2 ? transparent : baseline.ButtonColor,
            baseline.Appearance));

        exception.ParamName.ShouldBe(part switch
        {
            0 => "trackColor",
            1 => "thumbColor",
            _ => "buttonColor"
        });
    }

    /// <summary>Verifies partial style construction rejects undefined enum contributions immediately.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StyleSet_WhenEnumIsUndefined_ThrowsAtConstruction(bool invalidChrome)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ScrollBarStyleSet(
            chrome: invalidChrome ? (ScrollBarChrome) 99 : null,
            fill: invalidChrome ? null : (ScrollBarFill) 99));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies partial style construction rejects transparent part foregrounds immediately.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void StyleSet_WhenPartColorIsTransparent_ThrowsAtConstruction(int part)
    {
        ColorValue transparent = Color.Transparent;

        var exception = Should.Throw<ArgumentException>(() => new ScrollBarStyleSet(
            trackColor: part == 0 ? transparent : null,
            thumbColor: part == 1 ? transparent : null,
            buttonColor: part == 2 ? transparent : null));

        exception.ParamName.ShouldBe(part switch
        {
            0 => "trackColor",
            1 => "thumbColor",
            _ => "buttonColor"
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

    /// <summary>Verifies a public ambiguous-width override repairs through its slot's portable fallback.</summary>
    [Fact]
    public void Glyphs_WhenPublicGlyphBecomesWide_UsesSlotFallback()
    {
        var glyphs = CreateUniformGlyphs(new Rune('·'));

        new[]
        {
            glyphs.VerticalDecrementGlyph.Fallback,
            glyphs.VerticalIncrementGlyph.Fallback,
            glyphs.HorizontalDecrementGlyph.Fallback,
            glyphs.HorizontalIncrementGlyph.Fallback,
            glyphs.BlockTrackGlyph.Fallback,
            glyphs.BlockThumbGlyph.Fallback,
            glyphs.HorizontalLineTrackGlyph.Fallback,
            glyphs.HorizontalLineThumbGlyph.Fallback,
            glyphs.VerticalLineTrackGlyph.Fallback,
            glyphs.VerticalLineThumbGlyph.Fallback
        }.ShouldBe([
            new Rune('^'), new Rune('v'), new Rune('<'), new Rune('>'), new Rune('.'),
            new Rune('#'), new Rune('-'), new Rune('='), new Rune('|'), new Rune('#')
        ]);
        CellGlyphResolver.Resolve(
            glyphs.VerticalDecrement,
            glyphs.VerticalDecrementGlyph.Fallback,
            Ambiguous.Wide).ShouldBe(new Rune('^'));
    }

    /// <summary>Verifies equality and hashing include repair behavior as well as public primary runes.</summary>
    [Fact]
    public void Equality_WhenFallbackBehaviorDiffers_IsNotEqual()
    {
        var baseline = CreateUniformGlyphs(new Rune('·'));
        var differentFallback = new ScrollBarGlyphs(
            new ControlGlyph(baseline.VerticalDecrement, new Rune('?')),
            baseline.VerticalIncrementGlyph,
            baseline.HorizontalDecrementGlyph,
            baseline.HorizontalIncrementGlyph,
            baseline.BlockTrackGlyph,
            baseline.BlockThumbGlyph,
            baseline.HorizontalLineTrackGlyph,
            baseline.HorizontalLineThumbGlyph,
            baseline.VerticalLineTrackGlyph,
            baseline.VerticalLineThumbGlyph);

        baseline.ShouldNotBe(differentFallback);
        baseline.GetHashCode().ShouldNotBe(differentFallback.GetHashCode());
    }

    /// <summary>Verifies construction rejects a missing complete appearance profile.</summary>
    [Fact]
    public void Constructor_WhenAppearanceIsNull_Throws()
    {
        var baseline = ScrollBarStyle.Default;

        var exception = Should.Throw<ArgumentNullException>(() => new ScrollBarStyle(
            baseline.Chrome,
            baseline.Fill,
            baseline.Glyphs,
            baseline.TrackColor,
            baseline.ThumbColor,
            baseline.ButtonColor,
            null!));

        exception.ParamName.ShouldBe("appearance");
    }

    /// <summary>Verifies the typed glyph definition retains deterministic authored strings.</summary>
    [Fact]
    public void Deserialize_WhenScrollbarGlyphsAreAuthored_PreservesTypedShape()
    {
        var json = /*lang=json,strict*/
            """{"glyphs":{"verticalDecrement":"^","verticalIncrement":"v","horizontalDecrement":"<","horizontalIncrement":">","blockTrack":".","blockThumb":"#","horizontalLineTrack":"-","horizontalLineThumb":"=","verticalLineTrack":"|","verticalLineThumb":"#"}}""";

        var definition = JsonSerializer.Deserialize<ScrollBarStyleDefinition>(json).ShouldNotBeNull();
        var glyphs = definition.Glyphs.ShouldNotBeNull();

        glyphs.VerticalDecrement.ShouldBe("^");
        glyphs.VerticalLineThumb.ShouldBe("#");
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

    private static ScrollBarGlyphs CreateUniformGlyphs(Rune glyph) => new(
        glyph,
        glyph,
        glyph,
        glyph,
        glyph,
        glyph,
        glyph,
        glyph,
        glyph,
        glyph);

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
