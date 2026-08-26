// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;

/// <summary>Verifies public Theme construction preserves valid immutable metadata.</summary>
public sealed class ThemeTests
{
    /// <summary>Verifies privileged root lookup is selected by exact framework type identity.</summary>
    [Fact]
    public void GetStyleSet_WhenExternalTypeNameCollidesWithRoot_Throws()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var exception = Should.Throw<ArgumentException>(() =>
            theme.GetStyleSet(new Collisions.InputStyle()));

        exception.Message.ShouldContain("well-known root");
    }

    /// <summary>Verifies immutable role adapters and Window's derived state set are cached.</summary>
    [Fact]
    public void AppearanceAndWindowStyleSets_WhenReadRepeatedly_ReturnSameInstances()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        theme.Control.ShouldBeSameAs(theme.Control);
        theme.Input.ShouldBeSameAs(theme.Input);
        theme.Container.ShouldBeSameAs(theme.Container);
        theme.Window.ShouldBeSameAs(theme.Window);
        theme.Popup.ShouldBeSameAs(theme.Popup);
        theme.Tooltip.ShouldBeSameAs(theme.Tooltip);
        theme.GetWindowStyleSet().ShouldBeSameAs(theme.GetWindowStyleSet());
    }
    /// <summary>Verifies an undefined color-scheme value is rejected before publication.</summary>
    [Fact]
    public void Constructor_WhenColorSchemeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Theme(colorScheme: (ColorScheme) int.MaxValue));
    }

    /// <summary>Verifies required identity and provenance metadata cannot be null or blank.</summary>
    [Fact]
    public void Constructor_WhenMetadataIsInvalid_ThrowsBeforePublishingTheme()
    {
        _ = Should.Throw<ArgumentNullException>(() => new Theme(name: null!));
        _ = Should.Throw<ArgumentNullException>(() => new Theme(slug: null!));
        _ = Should.Throw<ArgumentNullException>(() => new Theme(author: null!));
        _ = Should.Throw<ArgumentNullException>(() => new Theme(license: null!));
        _ = Should.Throw<ArgumentNullException>(() => new Theme(source: null!));

        _ = Should.Throw<ArgumentException>(() => new Theme(name: " \t"));
        _ = Should.Throw<ArgumentException>(() => new Theme(slug: " \t"));
        _ = Should.Throw<ArgumentException>(() => new Theme(author: " \t"));
        _ = Should.Throw<ArgumentException>(() => new Theme(license: " \t"));
        _ = Should.Throw<ArgumentException>(() => new Theme(source: " \t"));
    }

    /// <summary>Verifies catalog metadata cannot publish an undefined color scheme.</summary>
    [Fact]
    public void ThemeCatalogEntry_WhenColorSchemeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ThemeCatalogEntry(
            "Theme",
            "theme",
            (ColorScheme) int.MaxValue,
            "Author",
            "MIT",
            "https://example.invalid/theme"));
    }

    /// <summary>Verifies loading retains named colors separately from resolved control colors.</summary>
    [Fact]
    public void FromJson_WhenPaletteIsNamed_RetainsPaletteAndSemanticMaps()
    {
        var theme = ThemeCatalog.Parse(
            ThemeJson.Create(
                palette: "\"bg\":\"#101010\",\"fg\":\"#e0e0e0\",\"brand\":\"#112233\"",
                accent: "brand"),
            "test");

        theme.Palette["brand"].ShouldBe(Color.Rgb(0x11, 0x22, 0x33));
        ThemeColorHelper.Accent(theme).ShouldBe(Color.Rgb(0x11, 0x22, 0x33));
    }

    /// <summary>Verifies the published maps cannot be changed through dictionary interfaces.</summary>
    [Fact]
    public void Theme_WhenMapsArePublished_AreReadOnly()
    {
        var theme = ThemeCatalog.Parse(
            ThemeJson.Create(),
            "test");

        _ = Should.Throw<NotSupportedException>(() =>
            ((IDictionary<string, Color>) theme.Palette).Add("extra", Color.Default));
    }

    /// <summary>Verifies "transparent" resolves to Color.Transparent.</summary>
    [Fact]
    public void ResolveSectionColor_WhenValueIsTransparent_ResolvesTransparent()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var value = theme.ResolveSectionColor("transparent", "context");

        theme.Resolve(value).ShouldBe(Color.Transparent);
    }

    /// <summary>Verifies "default" resolves to Color.Default.</summary>
    [Fact]
    public void ResolveSectionColor_WhenValueIsDefault_ResolvesDefault()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var value = theme.ResolveSectionColor("default", "context");

        theme.Resolve(value).ShouldBe(Color.Default);
    }

    /// <summary>Verifies a SemanticColor name resolves through the theme's semantic color.</summary>
    [Fact]
    public void ResolveSectionColor_WhenValueIsThemeColorName_ResolvesSemanticColor()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(accent: "#77aaff"));

        var value = theme.ResolveSectionColor("accent", "context");

        theme.Resolve(value).ShouldBe(Color.Rgb(0x77, 0xaa, 0xff));
    }

    /// <summary>Verifies a well-formed hex literal is rejected: every per-control color member must
    /// name a SemanticColor role or a palette key, so a value shaped like a hex literal is no longer
    /// resolved directly here even when well-formed - it fails the same way an unknown palette key
    /// already does.</summary>
    [Fact]
    public void ResolveSectionColor_WhenValueIsHexLiteral_ThrowsUnknownPaletteKey()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var error = Should.Throw<InvalidDataException>(() => theme.ResolveSectionColor("#123456", "context"));

        error.Message.ShouldContain("references unknown palette key");
    }

    /// <summary>Verifies a malformed hex literal is rejected the same way a well-formed one now is -
    /// neither is ever recognized as a hex literal anymore, so both fail identically as an unknown
    /// palette key rather than one failing on shape and the other on parseability.</summary>
    [Fact]
    public void ResolveSectionColor_WhenValueLooksLikeHexLiteralButIsMalformed_ThrowsUnknownPaletteKey()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var error = Should.Throw<InvalidDataException>(() => theme.ResolveSectionColor("#zzz", "context"));

        error.Message.ShouldContain("references unknown palette key");
    }

    /// <summary>Verifies a named palette key resolves through the theme's own retained palette.</summary>
    [Fact]
    public void ResolveSectionColor_WhenValueIsPaletteKey_ResolvesPaletteColor()
    {
        var theme = ThemeCatalog.Parse(
            ThemeJson.Create(palette: "\"bg\":\"#101010\",\"fg\":\"#e0e0e0\",\"brand\":\"#112233\""));

        var value = theme.ResolveSectionColor("brand", "context");

        theme.Resolve(value).ShouldBe(Color.Rgb(0x11, 0x22, 0x33));
    }

    /// <summary>Verifies an unknown palette key throws InvalidDataException.</summary>
    [Fact]
    public void ResolveSectionColor_WhenValueIsUnknownPaletteKey_Throws()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        _ = Should.Throw<InvalidDataException>(() => theme.ResolveSectionColor("no-such-key", "context"));
    }

    /// <summary>Verifies a null value throws ArgumentNullException.</summary>
    [Fact]
    public void ResolveSectionColor_WhenValueIsNull_Throws()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        _ = Should.Throw<ArgumentNullException>(() => theme.ResolveSectionColor(null!, "context"));
    }

    /// <summary>The regression this file exists to pin: a validating init accessor rejecting a
    /// value is a labelled theme error, not a raw TargetInvocationException. Exercised directly
    /// against <see cref="Theme.Overlay"/> with a synthetic fragment whose own init accessor
    /// validates - the same mechanic every real style's validating member (a markStyle, a paint
    /// channel, a glyph) shares, independent of which theme section reaches it. A theme's own
    /// leaf sections no longer exist to carry this scenario, since a leaf resolves no section of
    /// its own any more.</summary>
    [Fact]
    public void Overlay_WhenAValidatingMemberRejectsTheValue_ReportsALabelledThemeError()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var error = Should.Throw<InvalidDataException>(() => theme.Overlay(
            new TestValidatingStyle { Count = 1 },
            ParseOverrides(/*lang=json,strict*/ """{"count":-1}"""),
            "styles.acme.normal"));

        error.Message.Contains("styles.acme.normal.count", StringComparison.Ordinal).ShouldBeTrue(
            $"the failure must name its dotted path, but read '{error.Message}'");
        error.InnerException.ShouldNotBeNull()
            .ShouldNotBeOfType<TargetInvocationException>(
                "the accessor's own exception must be unwrapped, not the reflection wrapper");
    }

    /// <summary>Verifies a get-only computed property is refused by name rather than resolving,
    /// converting, and then crashing inside SetValue. Exercised directly against
    /// <see cref="Theme.Overlay"/> for the same reason as the validating-member test above.</summary>
    [Fact]
    public void Overlay_WhenAKeyNamesAComputedProperty_ReportsItAsUnknown()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        var error = Should.Throw<InvalidDataException>(() => theme.Overlay(
            new TestComputedStyle { Seed = 1 },
            ParseOverrides(/*lang=json,strict*/ """{"computed":3}"""),
            "styles.acme.normal"));

        error.Message.Contains("is not a known property", StringComparison.Ordinal).ShouldBeTrue(
            $"a derived member must be refused by name, but read '{error.Message}'");
    }

    /// <summary>Verifies ParseSectionGlyph's null passthrough, single-Rune success, and the
    /// multi-Rune/empty rejection message shape - the shared helper six controls' own ParseGlyph
    /// used to hand-copy verbatim now route through.</summary>
    [Fact]
    public void ParseSectionGlyph_WhenValueIsNullSingleOrMultiRune_MatchesTheDocumentedContract()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        theme.ParseSectionGlyph(null, "styles.checkBox.glyphs.unchecked").ShouldBeNull();
        theme.ParseSectionGlyph("x", "styles.checkBox.glyphs.unchecked").ShouldBe(new Rune('x'));

        var exception = Should.Throw<InvalidDataException>(
            () => theme.ParseSectionGlyph("xy", "styles.checkBox.glyphs.unchecked"));
        exception.Message.ShouldBe(
            "Theme '<parsed>' styles.checkBox.glyphs.unchecked must contain one Rune.");
    }

    /// <summary>Verifies ParseSectionEnum's null passthrough, case-insensitive success, and the
    /// unknown-value rejection message shape - the shared helper four controls' own
    /// ParseMarkStyle/ParseChrome/ParseFill used to hand-copy verbatim now route through.</summary>
    [Fact]
    public void ParseSectionEnum_WhenValueIsNullKnownOrUnknown_MatchesTheDocumentedContract()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        theme.ParseSectionEnum<CheckBoxMarkStyle>(null, "styles.checkBox.markStyle").ShouldBeNull();
        theme.ParseSectionEnum<CheckBoxMarkStyle>("BRACKETS", "styles.checkBox.markStyle")
            .ShouldBe(CheckBoxMarkStyle.Brackets);

        var exception = Should.Throw<InvalidDataException>(
            () => theme.ParseSectionEnum<CheckBoxMarkStyle>("bogus", "styles.checkBox.markStyle"));
        exception.Message.ShouldBe(
            "Theme '<parsed>' styles.checkBox.markStyle has unknown value 'bogus'.");
    }

    /// <summary>Verifies one frozen theme safely resolves global colors from concurrent renderer threads.</summary>
    [Fact]
    public void ResolveColor_WhenFrozenThemeIsReadConcurrently_ReturnsStableColors()
    {
        var theme = ThemeCatalog.Dark;
        var expected = theme.ResolveColor(SemanticColor.ActiveBorder);

        _ = Parallel.For(
            0,
            100_000,
            _ => theme.ResolveColor(SemanticColor.ActiveBorder).ShouldBe(expected));
    }

    /// <summary>Verifies semantic values resolve to the configured concrete color.</summary>
    [Fact]
    public void ResolveColor_WhenKnownSemanticColorIsRequested_ReturnsConcreteColor()
    {
        var expected = ThemeCatalog.Dark.ResolveColor(SemanticColor.FocusedText);

        expected.IsRgb.ShouldBeTrue();
        ThemeCatalog.Dark.Resolve(ThemeCatalog.Dark.Input.Resolve(VisualState.Focused).Face.Foreground).ShouldBe(expected);
    }

    /// <summary>Verifies borderless controls that directly own interaction preserve the passive
    /// control resting appearance while consuming the input role's focused color delta.</summary>
    [Fact]
    public void BorderlessInteractiveStyles_WhenFocused_RebaseInputColorsOntoControlGeometry()
    {
        var theme = ThemeCatalog.Dark;

        AssertRebased(ExpanderStyle.Definition.Appearance!(ExpanderStyle.Definition.Resolve(null, theme), theme));
        AssertRebased(SliderStyle.Definition.Appearance!(SliderStyle.Definition.Resolve(null, theme), theme));
        AssertRebased(ScrollBarStyle.Definition.Appearance!(ScrollBarStyle.Definition.Resolve(null, theme), theme));

        void AssertRebased(AppearanceStates states)
        {
            var normal = states.Resolve(VisualState.Normal);
            var focused = states.Resolve(VisualState.Focused);
            var inputFocused = theme.Input.Resolve(VisualState.Focused);

            normal.ShouldBe(theme.Control.Normal);
            focused.Face.Foreground.ShouldBe(inputFocused.Face.Foreground);
            focused.Face.Background.ShouldBe(inputFocused.Face.Background);
            focused.Face.Underline.ShouldBe(inputFocused.Face.Underline);
            focused.Face.UnderlineColor.ShouldBe(inputFocused.Face.UnderlineColor);

            // Dark's focusedControl/focusedText collapse onto control/controlText exactly like
            // every other bundled theme, so this borderless geometry has no color of its own to
            // signal focus; the safety net in Theme.ApplyBorderlessFocusFallback forces Reverse
            // onto the resolved attributes here rather than leaving the attribute-only cue (bold)
            // that a block/line-drawing glyph frequently cannot render distinctly.
            focused.Face.Attributes.IsLiteral.ShouldBeTrue();
            focused.Face.Attributes.Literal.ShouldBe(theme.ResolveAttributes(inputFocused.Face.Attributes.SemanticDecoration) | TerminalAttributes.Reverse);

            focused.Border.Foreground.ShouldBe(inputFocused.Border.Foreground);
            focused.Border.Sides.ShouldBe(normal.Border.Sides);
            focused.Border.GlyphStyle.ShouldBe(normal.Border.GlyphStyle);
        }
    }

    /// <summary>Verifies selectable borderless rows use Input's pointer foreground while
    /// retaining Control's passive background until a semantic selection state owns the fill.</summary>
    [Fact]
    public void InteractiveRowStyle_WhenPointerIsOver_PreservesPassiveBackground()
    {
        var theme = ThemeCatalog.Dark;
        var states = theme.GetInteractiveRowStyleSet().ToAppearanceStates();

        var pointerOver = states.Resolve(VisualState.IsPointerOver);
        var selected = states.Resolve(VisualState.Selected);

        pointerOver.Face.Foreground.ShouldBe(theme.Input.Resolve(VisualState.IsPointerOver).Face.Foreground);
        pointerOver.Face.Background.ShouldBe(theme.Control.Normal.Face.Background);
        selected.Face.Background.ShouldBe(theme.Input.Resolve(VisualState.Selected).Face.Background);
    }

    /// <summary>A leaf declares no theme section of its own any more, so there is nothing left for
    /// a leaf-owned block to narrow or replace: a fallback-authored state reaches a real leaf style
    /// (Button) exactly as fully as it reaches the fallback itself. This is the direct successor of
    /// the three-test "leaf narrows/replaces/is-removed" regression suite the pre-closure design
    /// needed - with leaf-owned narrowing deleted outright, only the single inheritance fact
    /// remains to prove.</summary>
    [Fact]
    public void BuildFallbackAwareStates_WhenTheFallbackAuthorsDisabled_TheLeafInheritsItWithNothingToNarrowIt()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            inputStates: """, "disabled": { "face": { "foreground": "disabledText" } } """));

        var resolved = ButtonStyle.Definition.Appearance!(ButtonStyle.Definition.Resolve(null, theme), theme)
            .Resolve(VisualState.Disabled);

        resolved.Face.Foreground.ShouldBe((ControlColor) SemanticColor.DisabledText);
    }

    /// <summary>Verifies a theme authoring only <c>control</c> colors leaves every sibling's
    /// code-owned border intact. Input's Heavy/All, Container's Light, Window's Paired, and Popup's
    /// Rounded were all replaced by Control's borderless default.</summary>
    [Fact]
    public void GetStyleSet_WhenOnlyControlColorsAreAuthored_SiblingsKeepTheirCodeOwnedChrome()
    {
        var theme = ThemeCatalog.Parse(_colorsOnlyTheme);

        theme.GetStyleSet(InputStyle.Default).Normal.Border.Sides.ShouldBe(InputStyle.Default.Border.Sides);
        theme.GetStyleSet(InputStyle.Default).Normal.Border.GlyphStyle.ShouldBe(InputStyle.Default.Border.GlyphStyle);
        theme.GetStyleSet(ContainerStyle.Default).Normal.Border.GlyphStyle
            .ShouldBe(ContainerStyle.Default.Border.GlyphStyle);
        theme.GetStyleSet(WindowStyle.Default).Normal.Border.GlyphStyle.ShouldBe(WindowStyle.Default.Border.GlyphStyle);
        theme.GetStyleSet(PopupStyle.Default).Normal.Border.GlyphStyle.ShouldBe(PopupStyle.Default.Border.GlyphStyle);
    }

    /// <summary>Verifies Window's composite shadow survives too - the one sibling whose distinctive
    /// default is more than a border, and the one whose loss changes the visual footprint.</summary>
    [Fact]
    public void GetStyleSet_WhenOnlyControlColorsAreAuthored_WindowKeepsItsShadow()
    {
        var shadow = ThemeCatalog.Parse(_colorsOnlyTheme).GetStyleSet(WindowStyle.Default).Normal.Shadow;

        shadow.IsVisible.ShouldBe(WindowStyle.Default.Shadow.IsVisible);
        shadow.Mode.ShouldBe(WindowStyle.Default.Shadow.Mode);
        shadow.Offset.ShouldBe(WindowStyle.Default.Shadow.Offset);
    }

    /// <summary>The point of the cascade, which the fix must not break: what <c>control</c> DOES
    /// author still reaches every sibling.</summary>
    [Fact]
    public void GetStyleSet_WhenControlAuthorsColors_SiblingsStillInheritThem()
    {
        var theme = ThemeCatalog.Parse(_colorsOnlyTheme);

        theme.GetStyleSet(InputStyle.Default).Normal.Face.Foreground
            .ShouldBe((ControlColor) SemanticColor.ControlText);
        theme.GetStyleSet(ContainerStyle.Default).Normal.Face.Background
            .ShouldBe((ControlColor) SemanticColor.Control);
    }

    /// <summary>Verifies a theme that explicitly authors <c>control</c>'s border still cascades it.
    /// The fix narrows the cascade to what the theme wrote, not to colors: an author who writes
    /// <c>control.normal.border.sides</c> means it.</summary>
    [Fact]
    public void GetStyleSet_WhenControlAuthorsItsBorder_SiblingsInheritThatToo()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        theme.GetStyleSet(TooltipStyle.Default).Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Rounded);
    }

    /// <summary>Verifies the bundled themes' repeated sibling <c>border</c> blocks still win, so the
    /// change makes them redundant rather than inverting them.</summary>
    [Fact]
    public void GetStyleSet_WhenASiblingReAuthorsItsBorder_ThatStillWins()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(inputSides: "\"all\"", inputGlyphStyle: "\"light\""));

        var border = theme.GetStyleSet(InputStyle.Default).Normal.Border;

        border.Sides.ShouldBe(BorderSide.All);
        border.GlyphStyle.ShouldBe(BorderGlyphStyle.Light);
    }

    /// <summary>Verifies <c>control</c>'s own root "normal" state is tracked as authored the same
    /// way every other state already is. <c>BuildRootStyleSet</c> resolved the root "normal" state
    /// through the four-argument <c>ResolveRawState</c> overload, the only one of its ten calls that
    /// omitted the <c>authored</c> dictionary - so <c>AuthoredFor("normal")</c> was always null for
    /// "control", and the sibling cascade fell back to value-diffing, which drops a member the
    /// author wrote back to a value <c>ControlStyle.Default</c> already has (border sides "none" is
    /// both InputStyle's and Window's own code-owned default absent). Both siblings inherit
    /// <c>control</c>'s heavy/all and paired/all borders by default, so this is the one shape where
    /// the missing authored-tracking is observable.</summary>
    [Fact]
    public void GetStyleSet_WhenControlAuthorsBorderSidesEqualToItsCodeOwnedDefault_SiblingsStillCascadeIt()
    {
        var theme = ThemeCatalog.Parse(_colorsAndBorderOnlyTheme);

        theme.GetStyleSet(InputStyle.Default).Normal.Border.Sides.ShouldBe(BorderSide.None);
        theme.GetStyleSet(WindowStyle.Default).Normal.Border.Sides.ShouldBe(BorderSide.None);
    }

    /// <summary>Verifies a structural (non-Face/Border/Shadow) member authored under a state other
    /// than "normal" is rejected rather than parsed, validated, and silently discarded -
    /// <see cref="AppearanceOverlay"/> carries only Face/Border/Shadow, so nothing downstream ever
    /// reads it. Authored under "control" - one of the six role sections a theme can still author
    /// at all - rather than a synthetic key, since a theme document no longer admits any other
    /// kind.</summary>
    [Fact]
    public void GetStyleSet_WhenAStructuralMemberIsAuthoredUnderANonNormalState_ThrowsNamingTheDottedPath()
    {
        var theme = CreateThemeWithControlStyles(
            /*lang=json,strict*/ """{"pressed":{"weight":2}}""");

        var exception = Should.Throw<InvalidDataException>(() =>
            theme.GetStyleSet("control", StructuralDefault()));

        exception.Message.ShouldContain("styles.control.pressed.weight");
    }

    /// <summary>The counter-case: the same structural member authored under "normal" - the one
    /// state every style type's own structural members are actually completed from - succeeds.</summary>
    [Fact]
    public void GetStyleSet_WhenAStructuralMemberIsAuthoredUnderNormal_Succeeds()
    {
        var theme = CreateThemeWithControlStyles(
            /*lang=json,strict*/ """{"normal":{"weight":2}}""");

        var resolved = theme.GetStyleSet("control", StructuralDefault());

        resolved.Normal.Weight.ShouldBe(2);
    }

    // Authors control's colors and nothing else - no border, no shadow, and no sibling sections at
    // all. Exercises the cascade's ordinary shape: control authors real colors, and every sibling
    // must inherit exactly what was authored while keeping its own code-owned chrome.
    [StringSyntax(StringSyntaxAttribute.Json)]
    private const string _colorsOnlyTheme = """
        { "name": "T", "slug": "t", "colorScheme": "dark", "order": 1,
          "author": "A", "license": "MIT", "source": "s",
          "palette": {
            "bg":"#101010", "fg":"#e0e0e0", "shadow":"#303030", "accent":"#77aaff",
            "disabledText":"#707070", "disabledBorder":"#606060",
            "error":"#ff0000", "warning":"#ffff00", "success":"#00ff00", "info":"#0000ff",
            "red":"#ff0000", "green":"#00ff00", "yellow":"#ffff00", "blue":"#0000ff",
            "magenta":"#ff00ff", "cyan":"#00ffff"
          },
          "colors": {
            "window":"bg", "windowSurface":"bg", "windowText":"fg",
            "surface":"bg", "surfaceText":"fg",
            "control":"bg", "controlText":"fg",
            "controlBorder":"fg", "controlShadow":"shadow",
            "activeControl":"bg", "activeText":"fg", "activeBorder":"accent",
            "focusedControl":"bg", "focusedText":"accent", "focusedBorder":"accent",
            "pressedControl":"bg", "pressedText":"accent", "pressedBorder":"accent",
            "selectedControl":"accent", "selectedText":"fg",
            "disabledControl":"bg", "disabledText":"disabledText", "disabledBorder":"disabledBorder",
            "accent":"accent", "muted":"disabledText", "hotkey":"accent",
            "error":"error", "warning":"warning", "success":"success", "info":"info",
            "red":"red", "green":"green", "yellow":"yellow", "blue":"blue",
            "magenta":"magenta", "cyan":"cyan"
          },
          "attributes": {
            "normalText":[], "activeText":[], "focusedText":"bold", "pressedText":[],
            "selectedText":[], "disabledText":[], "border":[], "shadow":"dim", "hotkey":"underline"
          },
          "styles": {
            "control": { "normal": {
              "face": { "foreground":"controlText", "background":"control", "attributes":"normalText" }
            } }
          } }
        """;

    // Same shape as _colorsOnlyTheme, plus control.normal.border authored explicitly with every
    // member ConvertLeaf can express set to ControlStyle.NoBorder's own value (sides "none",
    // foreground "default", background "transparent", no attributes) - GlyphStyle is left
    // unauthored since no JSON spelling produces BorderGlyphStyle.Default, but it resolves to that
    // value anyway because Overlay patches onto ControlStyle.Default, whose own Border already
    // carries it. The resulting Border is therefore value-identical to ControlStyle.Default's -
    // still no sibling sections at all, so only authored-tracking, not a value difference, can make
    // a sibling cascade it.
    [StringSyntax(StringSyntaxAttribute.Json)]
    private const string _colorsAndBorderOnlyTheme = """
        { "name": "T", "slug": "t", "colorScheme": "dark", "order": 1,
          "author": "A", "license": "MIT", "source": "s",
          "palette": {
            "bg":"#101010", "fg":"#e0e0e0", "shadow":"#303030", "accent":"#77aaff",
            "disabledText":"#707070", "disabledBorder":"#606060",
            "error":"#ff0000", "warning":"#ffff00", "success":"#00ff00", "info":"#0000ff",
            "red":"#ff0000", "green":"#00ff00", "yellow":"#ffff00", "blue":"#0000ff",
            "magenta":"#ff00ff", "cyan":"#00ffff"
          },
          "colors": {
            "window":"bg", "windowSurface":"bg", "windowText":"fg",
            "surface":"bg", "surfaceText":"fg",
            "control":"bg", "controlText":"fg",
            "controlBorder":"fg", "controlShadow":"shadow",
            "activeControl":"bg", "activeText":"fg", "activeBorder":"accent",
            "focusedControl":"bg", "focusedText":"accent", "focusedBorder":"accent",
            "pressedControl":"bg", "pressedText":"accent", "pressedBorder":"accent",
            "selectedControl":"accent", "selectedText":"fg",
            "disabledControl":"bg", "disabledText":"disabledText", "disabledBorder":"disabledBorder",
            "accent":"accent", "muted":"disabledText", "hotkey":"accent",
            "error":"error", "warning":"warning", "success":"success", "info":"info",
            "red":"red", "green":"green", "yellow":"yellow", "blue":"blue",
            "magenta":"magenta", "cyan":"cyan"
          },
          "attributes": {
            "normalText":[], "activeText":[], "focusedText":"bold", "pressedText":[],
            "selectedText":[], "disabledText":[], "border":[], "shadow":"dim", "hotkey":"underline"
          },
          "styles": {
            "control": { "normal": {
              "face": { "foreground":"controlText", "background":"control", "attributes":"normalText" },
              "border": { "sides":"none", "foreground":"default", "background":"transparent", "attributes":[] }
            } }
          } }
        """;

    private static TestStructuralRootStyle StructuralDefault() =>
        new(ControlStyle.DefaultFace, ControlStyle.NoBorder, ControlStyle.NoShadow, weight: 1);

    private static Theme CreateThemeWithControlStyles(string json)
    {
        var theme = new Theme();
        theme.SetStyleSections(new Dictionary<string, JsonElement>
        {
            ["control"] = JsonSerializer.Deserialize<JsonElement>(json)
        });
        return theme;
    }

    // A root style with one structural (non-Face/Border/Shadow) member, the shape
    // restrictToChrome exists to police: Weight is declared on this type, not on ControlStyle, so
    // it is exactly the kind of member every state but "normal" must reject rather than silently
    // resolve and discard.
    private sealed record TestStructuralRootStyle: ControlStyle
    {
        [SetsRequiredMembers]
        public TestStructuralRootStyle(Face face, Border border, Shadow shadow, int weight) : base(face, border, shadow) =>
            Weight = weight;

        public required int Weight { get; init; }
    }

    /// <summary>Verifies a leaf (non-fragment) property is replaced outright and the source
    /// instance is never mutated.</summary>
    [Fact]
    public void Overlay_WhenOverridingALeafProperty_ReplacesItAndLeavesSourceUnchanged()
    {
        var theme = CreateTheme();
        var original = new TestLeafStyle { Name = "original", Count = 1 };

        var result = (TestLeafStyle) theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"name":"patched"}"""), "test");

        result.Name.ShouldBe("patched");
        result.Count.ShouldBe(1);
        original.Name.ShouldBe("original");
    }

    /// <summary>Verifies a property whose type implements IAppearanceFragment is recursed into,
    /// patching only the named nested member and leaving every sibling nested member untouched.</summary>
    [Fact]
    public void Overlay_WhenOverridingANestedFragmentProperty_RecursesAndPreservesSiblings()
    {
        var theme = CreateTheme();
        var original = new TestNestedStyle
        {
            Label = "outer",
            Leaf = new TestLeafStyle { Name = "inner", Count = 5 }
        };

        var result = (TestNestedStyle) theme.Overlay(
            original,
            ParseOverrides(/*lang=json,strict*/ """{"leaf":{"name":"replaced"}}"""),
            "test");

        result.Label.ShouldBe("outer");
        result.Leaf.Name.ShouldBe("replaced");
        result.Leaf.Count.ShouldBe(5);
    }

    /// <summary>Verifies recursion composes through three nested fragment levels, patching only
    /// the deepest leaf.</summary>
    [Fact]
    public void Overlay_WhenOverridingThreeLevelsDeep_PatchesOnlyTheDeepestLeaf()
    {
        var theme = CreateTheme();
        var original = new TestDeepStyle
        {
            Nested = new TestNestedStyle
            {
                Label = "outer",
                Leaf = new TestLeafStyle { Name = "inner", Count = 5 }
            }
        };

        var result = (TestDeepStyle) theme.Overlay(
            original,
            ParseOverrides(/*lang=json,strict*/ """{"nested":{"leaf":{"count":42}}}"""),
            "test");

        result.Nested.Label.ShouldBe("outer");
        result.Nested.Leaf.Name.ShouldBe("inner");
        result.Nested.Leaf.Count.ShouldBe(42);
    }

    /// <summary>Verifies an override key that maps to no public property throws InvalidDataException
    /// naming the exact dotted path, not a silent no-op or a raw reflection failure.</summary>
    [Fact]
    public void Overlay_WhenKeyIsUnknown_ThrowsNamingTheExactPath()
    {
        var theme = CreateTheme();
        var original = new TestLeafStyle { Name = "x", Count = 1 };

        var exception = Should.Throw<InvalidDataException>(
            () => theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"bogus":"value"}"""), "styles.acme.normal"));

        exception.Message.ShouldContain("styles.acme.normal.bogus");
    }

    /// <summary>Verifies an unknown key nested inside a recursed fragment also names its full,
    /// nested dotted path.</summary>
    [Fact]
    public void Overlay_WhenNestedKeyIsUnknown_ThrowsNamingTheFullNestedPath()
    {
        var theme = CreateTheme();
        var original = new TestNestedStyle
        {
            Label = "outer",
            Leaf = new TestLeafStyle { Name = "inner", Count = 5 }
        };

        var exception = Should.Throw<InvalidDataException>(() => theme.Overlay(
            original,
            ParseOverrides(/*lang=json,strict*/ """{"leaf":{"bogus":1}}"""),
            "styles.acme.normal"));

        exception.Message.ShouldContain("styles.acme.normal.leaf.bogus");
    }

    /// <summary>Verifies a structural member is rejected under <c>restrictToChrome</c>, naming the
    /// exact dotted path - the guard every non-"normal" per-state resolution path applies, since a
    /// member other than Face/Border/Shadow written there is parsed, validated, and then never read
    /// by anything.</summary>
    [Fact]
    public void Overlay_WhenRestrictedToChromeAndKeyIsNotFaceBorderOrShadow_ThrowsNamingTheExactPath()
    {
        var theme = CreateTheme();
        var original = StructuralDefault();

        var exception = Should.Throw<InvalidDataException>(() => theme.Overlay(
            original,
            ParseOverrides(/*lang=json,strict*/ """{"weight":2}"""),
            "styles.acme.pressed",
            restrictToChrome: true));

        exception.Message.ShouldContain("styles.acme.pressed.weight");
    }

    /// <summary>The counter-case: Face/Border/Shadow - the only members every non-"normal" state is
    /// ever read back from - are still admitted under <c>restrictToChrome</c>.</summary>
    [Fact]
    public void Overlay_WhenRestrictedToChromeAndKeyIsFaceBorderOrShadow_Succeeds()
    {
        var theme = CreateTheme();
        var original = StructuralDefault();

        var result = (TestStructuralRootStyle) theme.Overlay(
            original,
            ParseOverrides(/*lang=json,strict*/ """{"border":{"sides":"all"}}"""),
            "styles.acme.pressed",
            restrictToChrome: true);

        result.Border.Sides.ShouldBe(BorderSide.All);
        result.Weight.ShouldBe(1);
    }

    /// <summary>Verifies the <c>restrictToChrome</c> guard rejects a nested
    /// <see cref="IAppearanceFragment"/>-typed structural member - <see cref="PopupStyle.AnchorGlyphs"/> -
    /// on a real root style, exercised directly against <see cref="Theme.Overlay"/> the same way the
    /// synthetic-style guard tests above are. <c>AnchorGlyphs</c> is declared on
    /// <see cref="PopupStyle"/> itself rather than <see cref="ControlStyle"/>, so the declaring-type
    /// check rejects it by name before <c>Overlay</c> ever recurses into its own
    /// <c>pointingUp</c>/<c>pointingDown</c>/<c>pointingLeft</c>/<c>pointingRight</c> members - the
    /// same "reject before recursing" shape a scalar structural member like <c>weight</c> above gets,
    /// now proven against a production nested fragment instead of a flat one.</summary>
    [Fact]
    public void Overlay_WhenRestrictedToChromeAndKeyIsANestedFragment_ThrowsNamingTheExactPathBeforeRecursing()
    {
        var theme = CreateTheme();

        var exception = Should.Throw<InvalidDataException>(() => theme.Overlay(
            PopupStyle.Default,
            ParseOverrides(/*lang=json,strict*/ """{"anchorGlyphs":{"pointingUp":"^"}}"""),
            "styles.popup.pressed",
            restrictToChrome: true));

        exception.Message.ShouldContain("styles.popup.pressed.anchorGlyphs");
    }

    /// <summary>Verifies a scalar value where a fragment property expects an object surfaces as an
    /// InvalidDataException, never a raw JsonException.</summary>
    [Fact]
    public void Overlay_WhenFragmentPropertyValueIsWrongShape_ThrowsInvalidDataException()
    {
        var theme = CreateTheme();
        var original = new TestNestedStyle
        {
            Label = "outer",
            Leaf = new TestLeafStyle { Name = "inner", Count = 5 }
        };

        var exception = Should.Throw<InvalidDataException>(() => theme.Overlay(
            original,
            ParseOverrides(/*lang=json,strict*/ """{"leaf":"not-an-object"}"""),
            "styles.acme.normal"));

        exception.Message.ShouldContain("styles.acme.normal.leaf");
        _ = exception.InnerException.ShouldNotBeNull();
    }

    /// <summary>Verifies a ControlColor leaf resolves through the same theme-color-or-literal rule
    /// ResolveSectionColor already uses - a palette key here.</summary>
    [Fact]
    public void Overlay_WhenLeafIsControlColor_ResolvesThroughThemePalette()
    {
        var theme = CreateTheme();
        var original = new TestColorStyle { Tint = Color.Default };

        var result = (TestColorStyle) theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"tint":"bg"}"""), "test");

        result.Tint.ShouldBe((ControlColor) theme.Palette["bg"]);
    }

    /// <summary>Verifies a ControlColor leaf referencing an unknown palette key throws
    /// InvalidDataException instead of silently resolving to a default color.</summary>
    [Fact]
    public void Overlay_WhenControlColorReferencesUnknownPaletteKey_Throws()
    {
        var theme = CreateTheme();
        var original = new TestColorStyle { Tint = Color.Default };

        _ = Should.Throw<InvalidDataException>(
            () => theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"tint":"no-such-key"}"""), "test"));
    }

    /// <summary>Verifies a Rune leaf resolves through the same single-Rune parser
    /// (ParseSectionGlyph) unified elsewhere in the overlay engine.</summary>
    [Fact]
    public void Overlay_WhenLeafIsRune_ParsesSingleRune()
    {
        var theme = CreateTheme();
        var original = new TestGlyphStyle { Glyph = new Rune('a') };

        var result = (TestGlyphStyle) theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"glyph":"x"}"""), "test");

        result.Glyph.ShouldBe(new Rune('x'));
    }

    /// <summary>Verifies a multi-Rune value for a Rune leaf throws InvalidDataException.</summary>
    [Fact]
    public void Overlay_WhenRuneValueHasMultipleRunes_Throws()
    {
        var theme = CreateTheme();
        var original = new TestGlyphStyle { Glyph = new Rune('a') };

        _ = Should.Throw<InvalidDataException>(
            () => theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"glyph":"xy"}"""), "test"));
    }

    /// <summary>Verifies an enum leaf resolves case-insensitively through the same parser
    /// (ParseSectionEnum&lt;TEnum&gt;) unified elsewhere, invoked here via a per-type-cached reflective call
    /// since the concrete enum type is only known at runtime.</summary>
    [Fact]
    public void Overlay_WhenLeafIsEnum_ParsesCaseInsensitively()
    {
        var theme = CreateTheme();
        var original = new TestEnumStyle { Mode = TestMode.First };

        var result = (TestEnumStyle) theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"mode":"SECOND"}"""), "test");

        result.Mode.ShouldBe(TestMode.Second);
    }

    /// <summary>Verifies an unknown enum value throws InvalidDataException instead of silently
    /// defaulting.</summary>
    [Fact]
    public void Overlay_WhenEnumValueIsUnknown_Throws()
    {
        var theme = CreateTheme();
        var original = new TestEnumStyle { Mode = TestMode.First };

        _ = Should.Throw<InvalidDataException>(
            () => theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"mode":"bogus"}"""), "test"));
    }

    /// <summary>Verifies a plain JSON-convertible leaf shape (here, an int) that is neither
    /// ControlColor, Rune, nor an enum deserializes directly through the shared JsonSerializerOptions.</summary>
    [Fact]
    public void Overlay_WhenLeafIsPlainConvertibleType_DeserializesDirectly()
    {
        var theme = CreateTheme();
        var original = new TestLeafStyle { Name = "x", Count = 1 };

        var result = (TestLeafStyle) theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"count":99}"""), "test");

        result.Count.ShouldBe(99);
    }

    /// <summary>Verifies the two-call composition a per-state style resolution needs - Normal
    /// patched from a code-owned default, then IsPointerOver patched from the already-resolved
    /// Normal - so an unspecified IsPointerOver property inherits Normal's value exactly like
    /// today's AppearanceOverlay-based per-state overlay already does for Face/Border/Shadow, just
    /// generalized to the whole fragment shape.</summary>
    [Fact]
    public void Overlay_WhenComposedTwiceForPerStateResolution_UnspecifiedPropertiesInheritFromNormal()
    {
        var theme = CreateTheme();
        var codeOwnedDefault = new TestLeafStyle { Name = "default", Count = 0 };

        var normal = (TestLeafStyle) theme.Overlay(
            codeOwnedDefault,
            ParseOverrides(/*lang=json,strict*/ """{"name":"normal-name","count":10}"""),
            "styles.acme.normal");
        var pointerOver = (TestLeafStyle) theme.Overlay(
            normal,
            ParseOverrides(/*lang=json,strict*/ """{"count":20}"""),
            "styles.acme.pointerOver");

        pointerOver.Name.ShouldBe("normal-name");
        pointerOver.Count.ShouldBe(20);
        normal.Count.ShouldBe(10);
    }

    /// <summary>Verifies Overlay recurses through a real production fragment - Border nested inside
    /// a synthetic style, patching one Border member and leaving its siblings and the outer
    /// style's own members untouched - proving the engine composes correctly against a type it
    /// did not define, now that Border implements IAppearanceFragment with init-only
    /// properties.</summary>
    [Fact]
    public void Overlay_WhenNestedPropertyIsARealBorder_PatchesOnlyTheNamedBorderMember()
    {
        var theme = CreateTheme();
        var border = new Border(BorderSide.All, BorderGlyphStyle.Light, Color.Default, Color.Transparent, TerminalAttributes.None);
        var original = new TestBorderHostStyle { Label = "outer", Border = border };

        var result = (TestBorderHostStyle) theme.Overlay(
            original,
            ParseOverrides(/*lang=json,strict*/ """{"border":{"sides":"none"}}"""),
            "test");

        result.Label.ShouldBe("outer");
        result.Border.Sides.ShouldBe(BorderSide.None);
        result.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Light);
        original.Border.Sides.ShouldBe(BorderSide.All);
    }

    /// <summary>Verifies ResolveProperty's cache resolves the same property for repeated lookups
    /// of the same (type, key) pair, and returns null for an unmapped key instead of throwing -
    /// Overlay itself is the layer responsible for turning that null into InvalidDataException.</summary>
    [Fact]
    public void ResolveProperty_WhenCalledRepeatedlyOrWithUnknownKey_IsConsistent()
    {
        var first = ThemeStyleFragment.ResolveProperty(typeof(TestLeafStyle), "name");
        var second = ThemeStyleFragment.ResolveProperty(typeof(TestLeafStyle), "name");
        var unknown = ThemeStyleFragment.ResolveProperty(typeof(TestLeafStyle), "doesNotExist");

        _ = first.ShouldNotBeNull();
        first.ShouldBeSameAs(second);
        unknown.ShouldBeNull();
    }

    private static Dictionary<string, JsonElement> ParseOverrides(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    private static Theme CreateTheme() => ThemeCatalog.Parse(ThemeJson.Create());

    private sealed record TestLeafStyle: IAppearanceFragment
    {
        public required string Name { get; init; }
        public required int Count { get; init; }

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }

    // A validating init accessor, exactly like a real style's markStyle enum or paint channel -
    // used to prove Overlay's property.SetValue door unwraps the accessor's own exception rather
    // than surfacing the raw reflection TargetInvocationException.
    private sealed record TestValidatingStyle: IAppearanceFragment
    {
        public required int Count
        {
            get;
            init
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                field = value;
            }
        }

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }

    // A get-only computed property, derived from Seed with no init accessor of its own - used to
    // prove ThemeStyleFragment.ResolveProperty refuses it by name rather than Overlay resolving,
    // converting, and then crashing trying to write it back.
    private sealed record TestComputedStyle: IAppearanceFragment
    {
        public required int Seed { get; init; }

        public int Computed => Seed + 1;

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }

    private sealed record TestBorderHostStyle: IAppearanceFragment
    {
        public required string Label { get; init; }
        public required Border Border { get; init; }

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }

    private sealed record TestNestedStyle: IAppearanceFragment
    {
        public required string Label { get; init; }
        public required TestLeafStyle Leaf { get; init; }

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }

    private sealed record TestDeepStyle: IAppearanceFragment
    {
        public required TestNestedStyle Nested { get; init; }

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }

    private sealed record TestColorStyle: IAppearanceFragment
    {
        public required ControlColor Tint { get; init; }

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }

    private sealed record TestGlyphStyle: IAppearanceFragment
    {
        public required Rune Glyph { get; init; }

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }

    private enum TestMode
    {
        First,
        Second
    }

    private sealed record TestEnumStyle: IAppearanceFragment
    {
        public required TestMode Mode { get; init; }

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }
}
