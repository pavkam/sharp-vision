// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies the embedded theme catalog discovers, orders, loads, and caches themes.</summary>
public sealed class ThemeCatalogTests
{
    private static readonly string _json = ThemeJson.Create(
        palette: "\"bg\":\"#101020\",\"fg\":\"#f0f0ff\"",
        name: "Ext",
        background: "#101020",
        foreground: "#f0f0ff");

    /// <summary>Verifies the default catalog includes both built-in theme slugs.</summary>
    [Fact]
    public void Default_ContainsBuiltInDefaults()
    {
        ThemeCatalog.Slugs.ShouldContain("default-dark");
        ThemeCatalog.Slugs.ShouldContain("default-light");
    }

    /// <summary>Verifies catalog entries are ordered by (order, slug).</summary>
    [Fact]
    public void Entries_AreOrderedByOrderThenSlug()
    {
        var entries = ThemeCatalog.Entries;

        entries[0].Slug.ShouldBe("default-dark"); // order 0
        entries[1].Slug.ShouldBe("default-light"); // order 1
    }

    /// <summary>Verifies loading the default dark theme produces complete normal control colors.</summary>
    [Fact]
    public void Load_WhenDefaultDark_ProducesCompleteNormalControlColors()
    {
        var theme = ThemeCatalog.Load("default-dark");

        theme.IsFrozen.ShouldBeTrue();
        theme.Resolve(theme.Control.Normal.Face.Foreground).IsRgb.ShouldBeTrue();
        theme.Resolve(theme.Control.Normal.Face.Background).IsRgb.ShouldBeTrue();
    }

    /// <summary>Verifies repeated loads of the same slug return the same cached instance.</summary>
    [Fact]
    public void Load_WhenCalledTwice_ReturnsSameInstance()
    {
        var first = ThemeCatalog.Load("default-dark");
        var second = ThemeCatalog.Load("default-dark");

        ReferenceEquals(first, second).ShouldBeTrue();
    }

    /// <summary>Verifies loading an unknown slug throws <see cref="KeyNotFoundException"/>.</summary>
    [Fact]
    public void Load_WhenUnknownSlug_Throws() =>
        Should.Throw<KeyNotFoundException>(() => ThemeCatalog.Load("nope"));

    /// <summary>Verifies no two embedded themes share a slug.</summary>
    [Fact]
    public void Slugs_AreUnique() =>
        ThemeCatalog.Slugs.Distinct(StringComparer.Ordinal).Count().ShouldBe(ThemeCatalog.Slugs.Count);

    /// <summary>Verifies theme loading has one public entry point backed by a JSON definition model.</summary>
    [Fact]
    public void PublicSurface_WhenInspected_ExposesOnlyTheCatalogLoader()
    {
        _ = typeof(ThemeCatalog).GetMethod(nameof(ThemeCatalog.Parse), [typeof(string)]).ShouldNotBeNull();
        _ = typeof(ThemeCatalog).Assembly.GetType("SharpVision.Styling.ThemeDocument").ShouldNotBeNull();

        // The DTO stays internal - a theme document is parsed through ThemeCatalog, never handed to
        // a caller as a model. The alternative entry points below have never existed and must not
        // appear: one loader, one document shape.
        typeof(ThemeCatalog).Assembly.GetType("SharpVision.Styling.ThemeDocument")!.IsPublic.ShouldBeFalse();
        typeof(ThemeCatalog).Assembly.GetType("SharpVision.Styling.ThemeFile").ShouldBeNull();
        typeof(ThemeCatalog).Assembly.GetType("SharpVision.Styling.ThemeLoader").ShouldBeNull();
    }

    /// <summary>Verifies malformed JSON is wrapped as <see cref="InvalidDataException"/> naming the source.</summary>
    [Fact]
    public void Deserialize_WhenMalformedJson_Throws()
    {
        var error = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse("{ not json", "broken"));

        error.Message.ShouldContain("broken");
    }

    /// <summary>Verifies a complete semantic theme loads metadata and concrete global colors.</summary>
    [Fact]
    public void Parse_WhenSemanticThemeIsComplete_LoadsGlobalValues()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(name: "Test", background: "#1a1a2e", foreground: "#e0e0e0"));

        theme.Name.ShouldBe("Test");
        theme.ResolveColor(SemanticColor.Control).ShouldBe(Color.FromHex("#1a1a2e"));
        theme.ResolveColor(SemanticColor.ControlText).ShouldBe(Color.FromHex("#e0e0e0"));
    }

    /// <summary>Verifies focused profile members overlay normal members without changing unrelated values.</summary>
    [Fact]
    public void Parse_WhenFocusedStateIsResolved_OverlaysNormalAppearance()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(accent: "#5555ff"));

        var normal = theme.Input.Resolve(VisualState.Normal);
        var focused = theme.Input.Resolve(VisualState.Focused);

        theme.Resolve(focused.Face.Foreground).ShouldBe(Color.FromHex("#5555ff"));
        theme.Resolve(focused.Face.Attributes).ShouldBe(TerminalAttributes.Bold);
        theme.Resolve(focused.Border.Foreground).ShouldBe(Color.FromHex("#5555ff"));
        focused.Face.Background.ShouldBe(normal.Face.Background);
    }

    /// <summary>Verifies all high-level profiles inherit unspecified normal values from ControlBase.</summary>
    [Fact]
    public void Parse_WhenInputSectionNormalIsPartial_InheritsControlAppearance()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(foreground: "#eeeeee"));

        theme.Input.Normal.Face.Foreground.ShouldBe(theme.Control.Normal.Face.Foreground);
        theme.Input.Normal.Border.Sides.ShouldBe(BorderSide.All);
        theme.Input.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
    }

    /// <summary>Verifies selector-era control maps are rejected as unknown input.</summary>
    [Fact]
    public void Parse_WhenLegacyControlsFieldIsPresent_Throws()
    {
        var json = /*lang=json,strict*/ """{"controls":{"Control":{}}}""";

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "test"));
    }

    /// <summary>Verifies semantic profiles load without naming CLR control types.</summary>
    [Fact]
    public void Parse_WhenSemanticDocumentIsValid_ResolvesButtonAppearance()
    {
        var theme = ThemeCatalog.Parse(CreateDocument(), "semantic-test");

        var normal = theme.Input.Resolve(VisualState.Normal);
        var hovered = theme.Input.Resolve(VisualState.IsPointerOver);

        normal.Border.Sides.ShouldBe(BorderSide.All);
        normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
        theme.Resolve(normal.Border.Foreground).ShouldBe(Color.Rgb(128, 128, 128));
        theme.Resolve(hovered.Border.Foreground).ShouldBe(Color.Rgb(0, 255, 255));
        theme.ResolveColor(SemanticColor.ActiveBorder).ShouldBe(Color.Rgb(0, 255, 255));
        theme.ResolveAttributes(SemanticDecoration.FocusedText).ShouldBe(TerminalAttributes.Bold);
    }

    /// <summary>Verifies the removed per-control selector root is rejected.</summary>
    [Fact]
    public void Parse_WhenControlsSelectorIsPresent_ThrowsInvalidDataException()
    {
        var json = CreateDocument().Replace(
            "\"styles\":",
            "\"controls\": {}, \"styles\":",
            StringComparison.Ordinal);

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "semantic-test"));
    }

    // Palette keys deliberately mirror semantic role names to exercise the documented exact-key
    // precedence. Style references such as "activeBorder" therefore resolve to these literal
    // palette entries, while differently-cased or absent keys can still name semantic roles.
    private static string CreateDocument() => /*lang=json,strict*/ """
        {
          "name": "Semantic",
          "slug": "semantic",
          "colorScheme": "dark",
          "order": 1,
          "author": "SharpVision",
          "license": "MIT",
          "source": "https://example.invalid/semantic",
          "palette": {
            "window": "#000000",
            "windowSurface": "#202020",
            "windowText": "#ffffff",
            "surface": "#202020",
            "surfaceText": "#ffffff",
            "control": "#303030",
            "controlText": "#ffffff",
            "controlBorder": "#808080",
            "controlShadow": "#101010",
            "activeControl": "#404040",
            "activeText": "#ffffff",
            "activeBorder": "#00ffff",
            "focusedControl": "#303040",
            "focusedText": "#00ffff",
            "focusedBorder": "#00ffff",
            "pressedControl": "#404020",
            "pressedText": "#ffff00",
            "pressedBorder": "#ffff00",
            "selectedControl": "#0000ee",
            "selectedText": "#ffffff",
            "disabledControl": "#303030",
            "disabledText": "#606060",
            "disabledBorder": "#505050",
            "accent": "#00ffff",
            "muted": "#808080",
            "hotkey": "#ffff00",
            "error": "#ff0000",
            "warning": "#ffff00",
            "success": "#00ff00",
            "info": "#5c5cff",
            "red": "#ff0000",
            "green": "#00ff00",
            "yellow": "#ffff00",
            "blue": "#0000ff",
            "magenta": "#ff00ff",
            "cyan": "#00ffff"
          },
          "colors": {
            "window": "window",
            "windowSurface": "windowSurface",
            "windowText": "windowText",
            "surface": "surface",
            "surfaceText": "surfaceText",
            "control": "control",
            "controlText": "controlText",
            "controlBorder": "controlBorder",
            "controlShadow": "controlShadow",
            "activeControl": "activeControl",
            "activeText": "activeText",
            "activeBorder": "activeBorder",
            "focusedControl": "focusedControl",
            "focusedText": "focusedText",
            "focusedBorder": "focusedBorder",
            "pressedControl": "pressedControl",
            "pressedText": "pressedText",
            "pressedBorder": "pressedBorder",
            "selectedControl": "selectedControl",
            "selectedText": "selectedText",
            "disabledControl": "disabledControl",
            "disabledText": "disabledText",
            "disabledBorder": "disabledBorder",
            "accent": "accent",
            "muted": "muted",
            "hotkey": "hotkey",
            "error": "error",
            "warning": "warning",
            "success": "success",
            "info": "info",
            "red": "red",
            "green": "green",
            "yellow": "yellow",
            "blue": "blue",
            "magenta": "magenta",
            "cyan": "cyan"
          },
          "attributes": {
            "normalText": [],
            "activeText": [],
            "focusedText": "bold",
            "pressedText": [],
            "selectedText": [],
            "disabledText": [],
            "border": [],
            "shadow": "dim",
            "hotkey": "underline"
          },
          "styles": {
            "control": {
              "normal": {
                "face": {
                  "foreground": "controlText",
                  "background": "control",
                  "attributes": "normalText"
                },
                "border": {
                  "sides": "none",
                  "glyphStyle": "rounded",
                  "foreground": "controlBorder",
                  "background": "control",
                  "attributes": "border"
                },
                "shadow": {
                  "visible": false,
                  "mode": "composite",
                  "offset": { "x": 0, "y": 0 },
                  "glyph": "▓",
                  "foreground": "controlShadow",
                  "background": "transparent",
                  "attributes": "shadow"
                }
              },
              "pointerOver": {
                "face": { "foreground": "activeText", "background": "activeControl" },
                "border": { "foreground": "activeBorder" }
              },
              "focused": {
                "face": { "foreground": "focusedText", "background": "focusedControl", "attributes": "focusedText" },
                "border": { "foreground": "focusedBorder" }
              },
              "pressed": {
                "face": { "foreground": "pressedText", "background": "pressedControl" },
                "border": { "foreground": "pressedBorder" }
              },
              "selected": {
                "face": { "foreground": "selectedText", "background": "selectedControl" }
              },
              "disabled": {
                "face": { "foreground": "disabledText", "background": "disabledControl" },
                "border": { "foreground": "disabledBorder" }
              }
            },
            "input": {
              "normal": { "border": { "sides": "all", "glyphStyle": "heavy" } },
              "pointerOver": { "border": { "foreground": "activeBorder" } }
            },
            "container": {
              "normal": { "border": { "sides": "all", "glyphStyle": "light" } }
            },
            "window": {
              "normal": { "border": { "sides": "all", "glyphStyle": "paired" } }
            },
            "popup": {
              "normal": { "border": { "sides": "all", "glyphStyle": "rounded" } }
            },
            "tooltip": {
              "normal": { "border": { "sides": "none" } }
            }
          }
        }
        """;

    /// <summary>Verifies a palette reference in "colors.*" still resolves - this path is untouched,
    /// only the interchangeable-hex-literal path below it is removed.</summary>
    [Fact]
    public void Parse_WhenColorsReferencePaletteKey_ResolvesConcreteValue()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(background: "bg", foreground: "fg"), "t");

        theme.ResolveColor(SemanticColor.Control).ShouldBe(Color.Rgb(0x10, 0x10, 0x10));
    }

    /// <summary>Verifies an exact palette key wins when its name also names a semantic color.</summary>
    [Fact]
    public void Parse_WhenStyleColorMatchesPaletteAndSemanticNames_UsesExactPaletteEntry()
    {
        var json = ThemeJson.Create(
            palette: "\"bg\":\"#101010\",\"fg\":\"#e0e0e0\",\"accent\":\"#ff0000\",\"semanticAccent\":\"#0000ff\"",
            accent: "semanticAccent",
            windowExtra: ",\"closeMarkColor\":\"accent\"");

        var theme = ThemeCatalog.Parse(json, "palette-precedence");

        theme.ResolveColor(SemanticColor.Accent).ShouldBe(Color.Rgb(0, 0, 255));
        theme.Resolve(theme.GetWindowStyleSet().Normal.CloseMarkColor).ShouldBe(Color.Rgb(255, 0, 0));
    }

    /// <summary>Verifies a raw hex literal in "colors.*" is rejected: every semantic-color mapping
    /// must name a palette key, so a value shaped like a hex literal is no longer resolved directly
    /// here - it fails the same way an unknown palette key already does.</summary>
    [Fact]
    public void Parse_WhenColorsSectionHasHexLiteral_ThrowsUnknownPaletteKey()
    {
        var json = ThemeJson.Create()
            .Replace("\"controlShadow\":\"__controlShadow\"", "\"controlShadow\":\"#303030\"", StringComparison.Ordinal);

        var error = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t"));

        error.Message.ShouldContain("colors.controlShadow");
        error.Message.ShouldContain("references unknown palette key");
    }

    /// <summary>Verifies focused semantic members overlay normal members independently.</summary>
    [Fact]
    public void Parse_WhenFocusedValuesAreDefined_OverlaysNormalAppearance()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(accent: "#ff8800"), "t");

        var normal = theme.Input.Resolve(VisualState.Normal);
        var focused = theme.Input.Resolve(VisualState.Focused);

        theme.Resolve(focused.Face.Foreground).ShouldBe(Color.Rgb(0xff, 0x88, 0x00));
        theme.Resolve(focused.Border.Foreground).ShouldBe(Color.Rgb(0xff, 0x88, 0x00));
        focused.Face.Background.ShouldBe(normal.Face.Background);
        focused.Border.Background.ShouldBe(normal.Border.Background);
    }

    /// <summary>Verifies role profiles inherit ControlBase appearance before applying role chrome.</summary>
    [Fact]
    public void Parse_WhenWindowSectionNormalIsPartial_InheritsControlAppearance()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(), "t");

        theme.Window.Normal.Face.ShouldBe(theme.Control.Normal.Face);
        theme.Window.Normal.Border.Sides.ShouldBe(BorderSide.All);
        theme.Window.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Paired);
    }

    /// <summary>Verifies a cross-cutting meaning resolves from the colors section.</summary>
    [Fact]
    public void Parse_WhenCrossCuttingColorIsExplicit_ResolvesItFromTheColorsSection()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#fedcba"), "t");

        theme.Hotkey.ShouldBe(Color.Rgb(0xfe, 0xdc, 0xba));
    }

    /// <summary>The regression this test exists to pin. Error, Warning, Success, Info, Muted, and
    /// Hotkey used to be a second color table filled from a parallel "status" theme section, which
    /// all 15 bundled themes authored as a byte-identical duplicate of the same six colors.* keys.
    /// That section was deleted when SemanticColor subsumed the retired StatusColor enum, so these
    /// six are now named shortcuts onto the one table. A reintroduced second table would let them
    /// diverge from ResolveColor silently.</summary>
    [Fact]
    public void CrossCuttingColors_WhenResolved_ReadTheOneColorsTable()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(), "t");

        theme.Error.ShouldBe(theme.ResolveColor(SemanticColor.Error));
        theme.Warning.ShouldBe(theme.ResolveColor(SemanticColor.Warning));
        theme.Success.ShouldBe(theme.ResolveColor(SemanticColor.Success));
        theme.Info.ShouldBe(theme.ResolveColor(SemanticColor.Info));
        theme.Muted.ShouldBe(theme.ResolveColor(SemanticColor.Muted));
        theme.Hotkey.ShouldBe(theme.ResolveColor(SemanticColor.Hotkey));
    }

    /// <summary>Verifies unknown palette references are rejected.</summary>
    [Fact]
    public void Parse_WhenColorReferenceIsUnknown_Throws()
    {
        var json = ThemeJson.Create(background: "missing");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t"));
    }

    /// <summary>Verifies malformed RGB text is rejected.</summary>
    [Fact]
    public void Parse_WhenColorLiteralIsMalformed_Throws()
    {
        var json = ThemeJson.Create(accent: "#zz");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t"));
    }

    /// <summary>Verifies a null palette value is reported as theme data failure.</summary>
    [Fact]
    public void Parse_WhenPaletteValueIsNull_Throws()
    {
        var json = ThemeJson.Create(palette: "\"bg\":null,\"fg\":\"#e0e0e0\"");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t"));
    }

    /// <summary>Verifies malformed JSON is reported as theme data failure.</summary>
    [Fact]
    public void Parse_WhenJsonIsMalformed_Throws() =>
        Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse("{ not json", "t"));

    /// <summary>Verifies an explicit eight-Rune array resolves to the matching BorderGlyphStyle.</summary>
    [Fact]
    public void Parse_WhenGlyphStyleIsExplicitArray_ResolvesEachSegmentInOrder()
    {
        var json = ThemeJson.Create(
            inputGlyphStyle: """["1","2","3","4","5","6","7","8"]""");

        var theme = ThemeCatalog.Parse(json, "t");
        var style = theme.Input.Normal.Border.GlyphStyle;

        style.TopLeft.ShouldBe(new Rune('1'));
        style.Top.ShouldBe(new Rune('2'));
        style.TopRight.ShouldBe(new Rune('3'));
        style.Right.ShouldBe(new Rune('4'));
        style.BottomRight.ShouldBe(new Rune('5'));
        style.Bottom.ShouldBe(new Rune('6'));
        style.BottomLeft.ShouldBe(new Rune('7'));
        style.Left.ShouldBe(new Rune('8'));
    }

    /// <summary>Verifies the named-family form is unaffected by the new array form (regression).</summary>
    [Fact]
    public void Parse_WhenGlyphStyleIsNamedFamily_StillResolvesStandardSet()
    {
        var json = ThemeJson.Create(inputGlyphStyle: "\"paired\"");

        var theme = ThemeCatalog.Parse(json, "t");

        theme.Input.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Paired);
    }

    /// <summary>Verifies an array with too few Runes is rejected.</summary>
    [Fact]
    public void Parse_WhenGlyphStyleArrayIsTooShort_Throws()
    {
        var json = ThemeJson.Create(inputGlyphStyle: """["1","2","3"]""");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t").Input.Normal.Border.GlyphStyle);
    }

    /// <summary>Verifies an array with too many Runes is rejected.</summary>
    [Fact]
    public void Parse_WhenGlyphStyleArrayIsTooLong_Throws()
    {
        var json = ThemeJson.Create(
            inputGlyphStyle: """["1","2","3","4","5","6","7","8","9"]""");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t").Input.Normal.Border.GlyphStyle);
    }

    /// <summary>Verifies an array element containing more than one Rune is rejected.</summary>
    [Fact]
    public void Parse_WhenGlyphStyleArrayElementHasMultipleRunes_Throws()
    {
        var json = ThemeJson.Create(
            inputGlyphStyle: """["ab","2","3","4","5","6","7","8"]""");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t").Input.Normal.Border.GlyphStyle);
    }

    /// <summary>Verifies a non-string array element is rejected.</summary>
    [Fact]
    public void Parse_WhenGlyphStyleArrayElementIsNotString_Throws()
    {
        var json = ThemeJson.Create(
            inputGlyphStyle: """["1","2","3","4","5","6","7",8]""");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t").Input.Normal.Border.GlyphStyle);
    }

    /// <summary>Verifies an unrecognized string family name is still rejected.</summary>
    [Fact]
    public void Parse_WhenGlyphStyleIsUnknownName_Throws()
    {
        var json = ThemeJson.Create(inputGlyphStyle: "\"not-a-family\"");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "t").Input.Normal.Border.GlyphStyle);
    }

    /// <summary>Verifies every declared style leaf is converted before Parse publishes a Theme,
    /// even when no matching control or style property is ever requested.</summary>
    [Fact]
    public void Parse_WhenUnusedStyleLeafIsMalformed_ThrowsAtTheLoadBoundaryWithSource()
    {
        var json = ThemeJson.Create().Replace(
            "\"tooltip\": { \"normal\": { \"border\": { \"sides\":\"none\" } } }",
            "\"tooltip\": { \"normal\": { \"border\": { \"sides\":\"diagonal\" } } }",
            StringComparison.Ordinal);

        var exception = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json, "unused.theme.json"));

        exception.Message.ShouldContain("unused.theme.json");
        exception.Message.ShouldContain("styles.tooltip.normal.border.sides");
    }

    /// <summary>Verifies stream loading retains its diagnostic label through eager style compilation.</summary>
    [Fact]
    public void Load_WhenUnusedStyleLeafIsMalformed_ThrowsNamingTheStream()
    {
        var json = ThemeJson.Create(inputGlyphStyle: "\"diagonal\"");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var exception = Should.Throw<InvalidDataException>(() => ThemeCatalog.Load(stream));

        exception.Message.ShouldContain("<stream>");
    }

    /// <summary>Verifies an unqualified unknown styles key is rejected instead of silently
    /// retained, since it is very likely a typo of one of the six well-known role names.</summary>
    [Fact]
    public void Parse_WhenStylesKeyIsUnqualifiedAndUnknown_Throws()
    {
        var json = ThemeJson.Create(extraStyles: ""","buton":{}""");

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json));
    }

    /// <summary>Verifies parsing valid JSON text returns a frozen theme with resolved control colors.</summary>
    [Fact]
    public void Parse_WhenValid_ReturnsFrozenTheme()
    {
        var theme = ThemeCatalog.Parse(_json);

        theme.IsFrozen.ShouldBeTrue();
        var accent = ThemeColorHelper.Accent(theme);
        accent.ShouldBe(Color.Rgb(0x77, 0xaa, 0xff));
    }

    /// <summary>Verifies parsing a null JSON string throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void Parse_WhenNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ThemeCatalog.Parse(null!));

    /// <summary>Verifies parsing malformed JSON is reported as <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void Parse_WhenMalformedJson_Throws() =>
        Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse("{ not json"));

    /// <summary>Verifies loading from a stream returns a frozen theme with resolved control colors.</summary>
    [Fact]
    public void Load_WhenStream_ReturnsFrozenTheme()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(_json));

        var theme = ThemeCatalog.Load(stream);

        var bg = ThemeColorHelper.Background(theme);
        bg.ShouldBe(Color.Rgb(0x10, 0x10, 0x20));
    }

    /// <summary>Verifies loading from a stream leaves the caller-owned stream open afterward.</summary>
    [Fact]
    public void Load_WhenStream_LeavesStreamOpen()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(_json));

        _ = ThemeCatalog.Load(stream);

        stream.CanRead.ShouldBeTrue();
    }

    /// <summary>Verifies a seekable stream is read into a buffer sized from the document itself
    /// instead of the historical fixed 64KB+1 scratch buffer that every document paid regardless
    /// of its real size. Compares two documents differing only by padding: with a
    /// right-sized buffer the extra allocation tracks the extra padding bytes; with the old fixed
    /// scratch buffer it would stay flat regardless of size. Deserialization and Theme-graph
    /// construction cost is identical between the two calls (the padding is whitespace, not
    /// additional semantic content), so the delta isolates the read buffer.</summary>
    [Fact]
    public void Load_WhenStreamIsSeekable_AllocatesProportionallyToDocumentSize()
    {
        var padded = _json + new string(' ', ThemeCatalog.MaximumDocumentBytes - Encoding.UTF8.GetByteCount(_json) - 100);

        using var small = new MemoryStream(Encoding.UTF8.GetBytes(_json));
        var beforeSmall = GC.GetAllocatedBytesForCurrentThread();
        _ = ThemeCatalog.Load(small);
        var allocatedSmall = GC.GetAllocatedBytesForCurrentThread() - beforeSmall;

        using var large = new MemoryStream(Encoding.UTF8.GetBytes(padded));
        var beforeLarge = GC.GetAllocatedBytesForCurrentThread();
        _ = ThemeCatalog.Load(large);
        var allocatedLarge = GC.GetAllocatedBytesForCurrentThread() - beforeLarge;

        (allocatedLarge - allocatedSmall).ShouldBeGreaterThan(ThemeCatalog.MaximumDocumentBytes / 2);
    }

    /// <summary>Verifies loading from a null stream throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void Load_WhenNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ThemeCatalog.Load((Stream) null!));

    /// <summary>Verifies loading malformed stream content is reported as <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void Load_WhenMalformedJson_Throws()
    {
        using MemoryStream stream = new("{ not json"u8.ToArray());

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Load(stream));
        stream.CanRead.ShouldBeTrue();
    }

    /// <summary>Verifies missing or empty semantic sections are rejected.</summary>
    [Theory]
    [InlineData( /*lang=json,strict*/ """{"status":{}}""")]
    [InlineData( /*lang=json,strict*/ """{"colors":{},"attributes":{},"styles":{}}""")]
    public void Parse_WhenSemanticSectionsAreMissingOrEmpty_Throws(string json) =>
        Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json));

    /// <summary>Verifies unknown and duplicate object fields are rejected instead of silently ignored.</summary>
    [Theory]
    [InlineData( /*lang=json,strict*/
        """{"unknown":true,"controls":{"Control":{"normal":{"background":"#000","foreground":"#fff"}}}}""")]
    [InlineData( /*lang=json,strict*/
        """{"roles":{},"controls":{"Control":{"normal":{"background":"#000","foreground":"#fff"}}}}""")]
    [InlineData( /*lang=json,strict*/
        """{"glyphs":{},"controls":{"Control":{"normal":{"background":"#000","foreground":"#fff"}}}}""")]
    [InlineData( /*lang=json,strict*/
        """{"name":"first","name":"second","controls":{"Control":{"normal":{"background":"#000","foreground":"#fff"}}}}""")]
    public void Parse_WhenFieldIsUnknownOrDuplicated_Throws(string json) =>
        Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json));

    /// <summary>Verifies the documented byte limit accepts its boundary and rejects one extra byte.</summary>
    [Fact]
    public void Parse_WhenDocumentReachesByteLimit_UsesExactBound()
    {
        var bytes = Encoding.UTF8.GetByteCount(_json);
        var boundary = _json + new string(' ', ThemeCatalog.MaximumDocumentBytes - bytes);
        var oversized = boundary + " ";

        ThemeCatalog.Parse(boundary).IsFrozen.ShouldBeTrue();
        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(oversized));
    }

    /// <summary>Verifies fragmented non-seekable input is consumed from its current position and remains caller-owned.</summary>
    [Fact]
    public void Load_WhenFragmentedAndNonSeekable_ParsesIncrementallyAndLeavesOpen()
    {
        var prefix = "ignored"u8.ToArray();
        var document = Encoding.UTF8.GetBytes(_json);
        var bytes = prefix.Concat(document).ToArray();
        using var stream = new FragmentedReadStream(bytes, prefix.Length, fragmentLength: 3);

        var theme = ThemeCatalog.Load(stream);

        theme.IsFrozen.ShouldBeTrue();
        stream.CanRead.ShouldBeTrue();
    }

    /// <summary>Verifies malformed UTF-8 is rejected without closing the caller-owned stream.</summary>
    [Fact]
    public void Load_WhenUtf8IsMalformed_ThrowsAndLeavesStreamOpen()
    {
        using MemoryStream stream = new([0xff, 0xfe, 0xfd]);

        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Load(stream));

        stream.CanRead.ShouldBeTrue();
    }

    /// <summary>Verifies palette entry count accepts its boundary and rejects one extra entry.
    /// <see cref="ThemeJson.Create"/> always reserves <see cref="ThemeJson.DefaultReservedPaletteEntryCount"/>
    /// palette entries of its own (for controlShadow/disabled*/status colors, plus the default
    /// hex-driven background/foreground/accent roles) on top of whatever this test appends, so the
    /// requested count is offset to still land exactly on the catalog's own boundary.</summary>
    [Fact]
    public void Parse_WhenPaletteEntryCountReachesLimit_UsesExactBound()
    {
        var valid = JsonWithPaletteEntries(256 - ThemeJson.DefaultReservedPaletteEntryCount);
        var invalid = JsonWithPaletteEntries(257 - ThemeJson.DefaultReservedPaletteEntryCount);

        ThemeCatalog.Parse(valid).IsFrozen.ShouldBeTrue();
        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(invalid));
    }

    /// <summary>Verifies each metadata string accepts its boundary and rejects one extra character.</summary>
    [Fact]
    public void Parse_WhenMetadataStringReachesLimit_UsesExactBound()
    {
        var boundary = new string('n', 2048);
        var valid = JsonWithName(boundary);
        var invalid = JsonWithName(boundary + "n");

        ThemeCatalog.Parse(valid).IsFrozen.ShouldBeTrue();
        _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(invalid));
    }

    /// <summary>Verifies loading a file path returns a frozen theme with resolved control colors.</summary>
    [Fact]
    public void LoadFile_WhenValid_ReturnsFrozenTheme()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(path, _json);

        try
        {
            var theme = ThemeCatalog.LoadFile(path);

            var accent = ThemeColorHelper.Accent(theme);
            accent.ShouldBe(Color.Rgb(0x77, 0xaa, 0xff));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies a leading UTF-8 byte order mark - what Visual Studio's "UTF-8 with
    /// signature", Notepad, and <c>new StreamWriter(path, false, Encoding.UTF8)</c> all produce -
    /// does not prevent loading. The Deserialize(ReadOnlySpan&lt;byte&gt;, ...) overload does not
    /// strip a preamble the way the Stream overload does for free, and buffering into a byte[] to
    /// enforce the size bound loses that leniency by accident.</summary>
    [Fact]
    public void LoadFile_WhenContentHasUtf8Preamble_ReturnsFrozenTheme()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        var withPreamble = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(_json)).ToArray();
        File.WriteAllBytes(path, withPreamble);

        try
        {
            var theme = ThemeCatalog.LoadFile(path);

            var accent = ThemeColorHelper.Accent(theme);
            accent.ShouldBe(Color.Rgb(0x77, 0xaa, 0xff));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies a root-level parse failure (an empty document, in this case) reports a
    /// clean message instead of the "... at ''." artifact: error.Path is "$" at the document root,
    /// which is not whitespace, so the untrimmed guard let it through while interpolating the
    /// trimmed - empty - value.</summary>
    [Fact]
    public void LoadFile_WhenContentIsEmpty_DoesNotReportDanglingEmptyPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(path, string.Empty);

        try
        {
            var thrown = Should.Throw<InvalidDataException>(() => ThemeCatalog.LoadFile(path));
            thrown.Message.ShouldNotContain("at ''");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies loading a null file path throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void LoadFile_WhenNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ThemeCatalog.LoadFile(null!));

    /// <summary>Verifies loading a missing file path throws <see cref="FileNotFoundException"/>.</summary>
    [Fact]
    public void LoadFile_WhenFileMissing_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

        _ = Should.Throw<FileNotFoundException>(() => ThemeCatalog.LoadFile(path));
    }

    /// <summary>Verifies loading a file with malformed content is reported as <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void LoadFile_WhenMalformedJson_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(path, "{ not json");

        try
        {
            _ = Should.Throw<InvalidDataException>(() => ThemeCatalog.LoadFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string JsonWithPaletteEntries(int count)
    {
        var entries = Enumerable.Range(0, count)
            .Select(static index => $"\"k{index}\":\"#000000\"");
        return ThemeJson.Create(palette: string.Join(',', entries));
    }

    private static string JsonWithName(string name) => ThemeJson.Create(name: name);

    /// <summary>A secondary style type - one that owns no primary appearance of its own, such as
    /// <see cref="ColorPickerStyle"/> - has its derived key rejected exactly like any other unknown
    /// name. This no longer distinguishes secondary from primary styles at all: every leaf's
    /// derived key is rejected now, since a theme's "styles" object is closed to exactly the six
    /// well-known role sections regardless of what kind of style a leaf declares itself to
    /// be.</summary>
    [Fact]
    public void Parse_WhenSectionBelongsToASecondaryStyle_Throws()
    {
        var key = StyleKey.Of<ColorPickerStyle>();
        key.ShouldBe("colorPicker", "the key is still derived from the type name");

        var json = ThemeJson.Create(
            extraStyles: $$""", "{{key}}": { "normal": { "face": { "foreground": "accent" } } } """);

        var exception = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json));

        exception.Message.ShouldContain(key);
    }

    /// <summary>Verifies an absent root-level "glyphs" field resolves to the code-owned
    /// <see cref="GlyphFamily.Default"/>, exactly as every unauthored style key already does.</summary>
    [Fact]
    public void Parse_WhenGlyphsFieldIsAbsent_ResolvesToDefaultFamily()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());

        theme.Glyphs.ShouldBeSameAs(GlyphFamily.Default);
    }

    /// <summary>Verifies every documented "glyphs" name resolves to its matching family, accepted
    /// case-insensitively like every other theme-authored enum-shaped value (see
    /// <see cref="Theme.ParseSectionEnum{TEnum}"/> and <see cref="Theme.ResolveSectionBorderGlyphStyle"/>).</summary>
    [Theory]
    [InlineData("dots", "Dots")]
    [InlineData("DOTS", "Dots")]
    [InlineData("blocks", "Blocks")]
    [InlineData("Blocks", "Blocks")]
    [InlineData("ascii", "Ascii")]
    [InlineData("ASCII", "Ascii")]
    [InlineData("shades", "Shades")]
    [InlineData("Shades", "Shades")]
    [InlineData("lines", "Lines")]
    [InlineData("LINES", "Lines")]
    public void Parse_WhenGlyphsFieldNamesAFamily_Resolves(string value, string familyName)
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(glyphs: value));
        var expected = familyName switch
        {
            "Dots" => GlyphFamily.Dots,
            "Blocks" => GlyphFamily.Blocks,
            "Ascii" => GlyphFamily.Ascii,
            "Shades" => GlyphFamily.Shades,
            _ => GlyphFamily.Lines
        };

        theme.Glyphs.ShouldBeSameAs(expected);
    }

    /// <summary>Verifies an unrecognized "glyphs" value fails with a source-labelled
    /// <see cref="InvalidDataException"/>, the same rejection every other malformed theme value gets.</summary>
    [Fact]
    public void Parse_WhenGlyphsFieldIsUnknown_ThrowsInvalidDataException()
    {
        var exception = Should.Throw<InvalidDataException>(
            () => ThemeCatalog.Parse(ThemeJson.Create(glyphs: "sparkles"), "glyphs-test"));

        exception.Message.ShouldContain("glyphs-test");
        exception.Message.ShouldContain("glyphs");
    }
}
