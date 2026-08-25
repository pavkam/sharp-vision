// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using System.Text.Json;

/// <summary>Verifies a style value cannot be brought into existence in a state its own constructor
/// would refuse, whichever door it comes through.
///
/// <para>There were three doors and only one was guarded. The constructor validated; a <c>with</c>
/// expression did not, because a bare <c>{ get; init; }</c> runs no code; and neither did
/// <c>Theme.Overlay</c>, which writes each JSON-named member with <c>PropertyInfo.SetValue</c> -
/// init-only properties are writable through reflection, and no constructor is in that path at
/// all. So a theme could produce a wide CJK scrollbar thumb or a transparent foreground, and the
/// rejection arrived later, from inside the render loop, with no theme slug and no
/// <c>styles.*</c> path.</para>
///
/// <para>The checks now live in the <c>init</c> accessors, which all three doors go through. These
/// tests therefore assert per member family and per door, not per call site: the point is that the
/// three agree.</para>
/// </summary>
public sealed class StyleInvariantEnforcementTests
{
    private static readonly Rune _wide = new('漢');

    /// <summary>Verifies a wide glyph in a nested <c>glyphs</c> member is refused by the parser,
    /// with the theme and the dotted path still named - the exact case themes.md promises is
    /// "rejected the same way a hand-authored glyph value would be", and the one
    /// <c>ParseSectionGlyph</c> never measured because it only counted Runes. Exercised directly
    /// against <see cref="Theme.Overlay"/> - a theme can no longer author a "scrollBar" section of
    /// its own, but the dotted path this asserts is exactly what a role section's own nested-glyph
    /// member would produce, and <c>Overlay</c> does not care which section context handed it the
    /// override.</summary>
    [Fact]
    public void Overlay_WhenAThemeAuthorsAWideNestedGlyph_RejectsItWithTheStylePath()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var error = Should.Throw<InvalidDataException>(() => theme.Overlay(
            ScrollBarStyle.Default,
            ParseOverrides(/*lang=json,strict*/ """{"glyphs":{"blockThumb":"漢"}}"""),
            "styles.scrollBar.normal"));

        error.Message.ShouldContain("styles.scrollBar.normal.glyphs.blockThumb");
    }

    /// <summary>The counter-case that keeps the width check honest: a one-cell non-ASCII glyph in
    /// the same member is still accepted, so this did not narrow the contract to ASCII.</summary>
    [Fact]
    public void Overlay_WhenAThemeAuthorsANarrowNestedGlyph_AcceptsIt()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var result = (ScrollBarStyle) theme.Overlay(
            ScrollBarStyle.Default,
            ParseOverrides(/*lang=json,strict*/ """{"glyphs":{"blockThumb":"▒"}}"""),
            "styles.scrollBar.normal");

        result.Glyphs.BlockThumb.ShouldBe(new Rune('▒'));
    }

    /// <summary>Verifies a wide glyph inside a Rune <em>array</em> names the offending index. The
    /// accessor catches this too, but only once the whole eight-Rune family has been assembled, so
    /// its message can say the family is invalid and not which entry made it so. Measuring in the
    /// parser is what keeps the index.</summary>
    [Fact]
    public void GetStyleSet_WhenAThemeAuthorsAWideGlyphInARuneArray_NamesTheOffendingIndex()
    {
        var error = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(ThemeJson.Create(
            controlExtra: """, "pressed": { "border": { "glyphStyle": ["─","│","┌","漢","└","┘","├","┤"] } } """)));

        error.Message.ShouldContain("styles.control.pressed.border.glyphStyle[3]");
    }

    /// <summary>Verifies the frame sequence is measured per entry too - the other Rune-array member,
    /// and the only one in the codebase typed as a sequence rather than a fixed family. Exercised
    /// directly against <see cref="Theme.Overlay"/> - a theme can no longer author a "spinner"
    /// section of its own, but the mechanic is the same regardless of which section context reaches
    /// it.</summary>
    [Fact]
    public void Overlay_WhenAThemeAuthorsAWideSpinnerFrame_RejectsIt()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var error = Should.Throw<InvalidDataException>(() => theme.Overlay(
            SpinnerStyle.Default,
            ParseOverrides(/*lang=json,strict*/ """{"frames":["a","漢"]}"""),
            "styles.spinner.normal"));

        error.Message.ShouldContain("styles.spinner.normal.frames");
    }

    /// <summary>Verifies a transparent paint channel authored through a theme is refused where the
    /// theme is still named, rather than surviving into a <c>Face</c> the render loop then
    /// rejects.</summary>
    [Fact]
    public void GetStyleSet_WhenAThemeAuthorsATransparentForeground_RejectsItWithTheStylePath()
    {
        var error = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(ThemeJson.Create(
            controlExtra: """, "pressed": { "face": { "foreground": "transparent" } } """)));

        error.Message.ShouldContain("styles.control.pressed.face.foreground");
    }

    /// <summary>Verifies the same refusal reaches a leaf style's own nine part colors, which were
    /// the last style type still validating in its constructor body while every sibling had
    /// already moved to the accessor pattern. Exercised directly against
    /// <see cref="Theme.Overlay"/> - a theme can no longer author a "calendar" section of its own,
    /// but the mechanic is the same regardless of which section context reaches it.</summary>
    [Fact]
    public void Overlay_WhenAThemeAuthorsATransparentCalendarPartColor_RejectsIt()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var error = Should.Throw<InvalidDataException>(() => theme.Overlay(
            CalendarStyle.Default,
            ParseOverrides(/*lang=json,strict*/ """{"selectedDayColor":"transparent"}"""),
            "styles.calendar.normal"));

        error.Message.ShouldContain("styles.calendar.normal.selectedDayColor");
    }

    /// <summary>Verifies the cross-member decoration invariant - the one no single accessor can
    /// enforce, because it relates three members that are not all written yet - is still checked
    /// once the overlay has finished assembling the fragment.</summary>
    [Fact]
    public void GetStyleSet_WhenAThemeAuthorsAConflictingDecoration_RejectsTheCompletedFace()
    {
        var error = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(ThemeJson.Create(
            controlExtra:
            """, "pressed": { "face": { "attributes": ["underline"], "underline": "straight" } } """)));

        error.Message.ShouldContain("styles.control.pressed.face");
    }

    /// <summary>Verifies a <c>with</c> expression on <c>Face</c> is checked exactly as the
    /// constructor is - the second unguarded door, and the one ordinary application code uses.</summary>
    [Fact]
    public void With_WhenFaceForegroundBecomesTransparent_Throws() =>
        _ = Should.Throw<ArgumentException>(
            () => ControlStyle.DefaultFace with { Foreground = Color.Transparent });

    /// <summary>Verifies the same for the underline channel, which has its own paint check.</summary>
    [Fact]
    public void With_WhenFaceUnderlineColorBecomesTransparent_Throws() =>
        _ = Should.Throw<ArgumentException>(
            () => ControlStyle.DefaultFace with { UnderlineColor = Color.Transparent });

    /// <summary>Verifies an out-of-range enum is refused through the same door.</summary>
    [Fact]
    public void With_WhenFaceUnderlineIsUnknown_Throws() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => ControlStyle.DefaultFace with { Underline = (Underline) 99 });

    /// <summary>Verifies <c>Border</c>'s flags guard survives a <c>with</c>.</summary>
    [Fact]
    public void With_WhenBorderSidesCarryUnknownFlags_Throws() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => ControlStyle.NoBorder with { Sides = (BorderSide) 0b1000_0000 });

    /// <summary>Verifies <c>Shadow</c>'s enum guard survives a <c>with</c>.</summary>
    [Fact]
    public void With_WhenShadowModeIsUnknown_Throws() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => ControlStyle.NoShadow with { Mode = (ShadowMode) 42 });

    /// <summary>Verifies <c>Shadow</c>'s glyph width check survives a <c>with</c>.</summary>
    [Fact]
    public void With_WhenShadowGlyphIsWide_Throws() =>
        _ = Should.Throw<ArgumentException>(() => ControlStyle.NoShadow with { Glyph = _wide });

    /// <summary>Verifies the two constructor behaviors that are normalizations rather than checks
    /// survive a <c>with</c> too. These were silently dropped for every non-constructor path,
    /// which is a quieter failure than a rejection: the value simply stopped drawing.</summary>
    [Fact]
    public void With_WhenBorderGlyphStyleIsZeroed_NormalizesToTheDefaultFamily() =>
        (ControlStyle.NoBorder with { GlyphStyle = default }).GlyphStyle.ShouldBe(BorderGlyphStyle.Default);

    /// <summary>Verifies the shadow-glyph normalization survives the same door.</summary>
    [Fact]
    public void With_WhenShadowGlyphIsZeroed_NormalizesToTheCodeOwnedGlyph() =>
        (ControlStyle.NoShadow with { Glyph = default }).Glyph.Value.ShouldNotBe(0);

    /// <summary>Verifies every one of the five glyph families rejects a wide replacement through a
    /// <c>with</c>. All five have the identical shape, so one is not evidence for the rest.</summary>
    [Fact]
    public void With_WhenAGlyphFamilyMemberIsWide_EveryFamilyThrows()
    {
        _ = Should.Throw<ArgumentException>(() => CheckBoxGlyphs.Default with { Checked = _wide });
        _ = Should.Throw<ArgumentException>(() => RadioButtonGlyphs.Default with { Checked = _wide });
        _ = Should.Throw<ArgumentException>(() => SliderGlyphs.Default with { Thumb = _wide });
        _ = Should.Throw<ArgumentException>(() => ProgressBarGlyphs.Default with { Fill = _wide });
        _ = Should.Throw<ArgumentException>(() => ScrollBarGlyphs.Default with { BlockThumb = _wide });
    }

    /// <summary>Verifies the constructors still reject what they always did. Moving a check into an
    /// accessor is only sound if the constructor, which now assigns through that accessor, keeps
    /// throwing - a plain field write would have silently disarmed all of them at once.</summary>
    [Fact]
    public void Constructor_WhenAGlyphFamilyMemberIsWide_EveryFamilyStillThrows()
    {
        _ = Should.Throw<ArgumentException>(() => new CheckBoxGlyphs(_wide, _wide, _wide));
        _ = Should.Throw<ArgumentException>(() => new RadioButtonGlyphs(_wide, _wide));
        _ = Should.Throw<ArgumentException>(() => new ProgressBarGlyphs(_wide, _wide, _wide));
    }

    /// <summary>Verifies a constructor rejection still names the argument that caused it. Moving the
    /// checks into the accessors alone silently downgraded every one of these to "value" - the only
    /// name an accessor has - which for a ten-glyph family or a nine-color style identifies nothing.
    /// That is why the constructors validate by name as well, rather than only assigning through
    /// the accessors.</summary>
    [Fact]
    public void Constructor_WhenAnArgumentIsRejected_NamesThatArgument()
    {
        Should.Throw<ArgumentException>(() => new ScrollBarGlyphs(
                ScrollBarGlyphs.Default.VerticalDecrement,
                ScrollBarGlyphs.Default.VerticalIncrement,
                ScrollBarGlyphs.Default.HorizontalDecrement,
                ScrollBarGlyphs.Default.HorizontalIncrement,
                ScrollBarGlyphs.Default.BlockTrack,
                _wide,
                ScrollBarGlyphs.Default.HorizontalLineTrack,
                ScrollBarGlyphs.Default.HorizontalLineThumb,
                ScrollBarGlyphs.Default.VerticalLineTrack,
                ScrollBarGlyphs.Default.VerticalLineThumb))
            .ParamName.ShouldBe("blockThumb");

        Should.Throw<ArgumentException>(() => new Face(
                Color.Rgb(1, 1, 1),
                Color.Rgb(2, 2, 2),
                TerminalAttributes.None,
                Underline.None,
                Color.Transparent))
            .ParamName.ShouldBe("underlineColor");

        Should.Throw<ArgumentOutOfRangeException>(() => new Shadow(
                isVisible: true,
                (ShadowMode) 42,
                default,
                default,
                Color.Rgb(1, 1, 1),
                Color.Rgb(2, 2, 2),
                TerminalAttributes.None))
            .ParamName.ShouldBe("mode");
    }

    /// <summary>Verifies the constructor path still rejects a transparent foreground.</summary>
    [Fact]
    public void Constructor_WhenFaceForegroundIsTransparent_StillThrows() =>
        _ = Should.Throw<ArgumentException>(() => new Face(
            Color.Transparent,
            Color.Rgb(1, 1, 1),
            TerminalAttributes.None,
            Underline.None,
            Color.Rgb(2, 2, 2)));

    /// <summary>Verifies the constructor path still rejects a conflicting decoration triple, which
    /// is now enforced after the members are assigned rather than before.</summary>
    [Fact]
    public void Constructor_WhenDecorationsConflict_StillThrows() =>
        _ = Should.Throw<ArgumentException>(() => new Face(
            Color.Rgb(1, 1, 1),
            Color.Rgb(2, 2, 2),
            TerminalAttributes.Underline,
            Underline.Straight,
            Color.Rgb(3, 3, 3)));

    private static Dictionary<string, JsonElement> ParseOverrides(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
}
