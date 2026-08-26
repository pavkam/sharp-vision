// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using SharpVision.Controls.Charts;
using SharpVision.Controls.Collections;
using SharpVision.Controls.Display;
using SharpVision.Controls.Input;
using SharpVision.Controls.Layout;
using SharpVision.Controls.Scrolling;
using SharpVision.Dialogs;
using SharpVision.Menus;
using SharpVision.Navigation;

/// <summary>Verifies <see cref="StyleKey"/> derives the section key each of the six well-known
/// base style types owns from its type name, so the two can never drift apart, and verifies a
/// theme document's "styles" object is closed to exactly those six names - every leaf control
/// style's own derived key (still computable, since <see cref="StyleKey.Of{TStyle}"/> is generic
/// over every <c>ControlStyle</c>-derived type) and every vendor-dotted key is rejected the same
/// way an unknown name is.</summary>
public sealed class StyleKeyTests
{
    /// <summary>Verifies the six well-known roots drop their <c>Theme</c> prefix and <c>Style</c>
    /// suffix, keeping the exact section names every bundled theme document already authors.</summary>
    [Fact]
    public void Of_WhenTypeIsAWellKnownRoot_DerivesTheExistingSectionName()
    {
        StyleKey.Of<ControlStyle>().ShouldBe("control");
        StyleKey.Of<InputStyle>().ShouldBe("input");
        StyleKey.Of<ContainerStyle>().ShouldBe("container");
        StyleKey.Of<WindowStyle>().ShouldBe("window");
        StyleKey.Of<PopupStyle>().ShouldBe("popup");
        StyleKey.Of<TooltipStyle>().ShouldBe("tooltip");
    }

    /// <summary>Verifies the derivation rule keeps working for a leaf control style's type name,
    /// even though the result no longer names any theme section - <c>Theme.GetStyleSet&lt;TStyle&gt;</c>
    /// stays generic over every <c>ControlStyle</c>-derived type, so the algorithm itself must keep
    /// producing a stable key for a type shaped like a leaf, dropping only the <c>Style</c> suffix
    /// and camel-casing the remainder.</summary>
    [Fact]
    public void Of_WhenTypeIsALeafControlStyle_StillDerivesAConsistentKey()
    {
        StyleKey.Of<ButtonStyle>().ShouldBe("button");
        StyleKey.Of<CheckBoxStyle>().ShouldBe("checkBox");
        StyleKey.Of<RadioButtonStyle>().ShouldBe("radioButton");
        StyleKey.Of<ScrollBarStyle>().ShouldBe("scrollBar");
        StyleKey.Of<ChaseIndicatorStyle>().ShouldBe("chaseIndicator");
        StyleKey.Of<JsonViewStyle>().ShouldBe("jsonView");
        StyleKey.Of<MessageBoxStyle>().ShouldBe("messageBox");
        StyleKey.Of<FilePickerDialogStyle>().ShouldBe("filePickerDialog");
        StyleKey.Of<SaveFileDialogStyle>().ShouldBe("saveFileDialog");
        StyleKey.Of<ChartStyle>().ShouldBe("chart");
    }

    /// <summary>The regression this file exists to pin, restated for the closed vocabulary: every
    /// leaf's derived key - real and legitimate as a string, matching exactly what
    /// <see cref="StyleDefinitions"/> would have needed before section resolution closed to the six
    /// well-known roots - is still rejected as unknown, since a leaf resolves no theme section at
    /// all any more. This sweeps every leaf style type declared in the library, so a future style
    /// that reintroduces a stray explicit key cannot silently reopen the vocabulary.</summary>
    [Fact]
    public void Parse_WhenThemeAuthorsAnyLeafSection_IsRejected()
    {
        foreach (var key in new[]
        {
            StyleKey.Of<ChartStyle>(),
            StyleKey.Of<JsonViewStyle>(),
            StyleKey.Of<TabControlStyle>(),
            StyleKey.Of<TreeViewStyle>(),
            StyleKey.Of<ChaseIndicatorStyle>(),
            StyleKey.Of<ProgressBarStyle>(),
            StyleKey.Of<SeparatorStyle>(),
            StyleKey.Of<SpinnerStyle>(),
            StyleKey.Of<StatusBarItemStyle>(),
            StyleKey.Of<TextStyle>(),
            StyleKey.Of<ButtonStyle>(),
            StyleKey.Of<CalendarStyle>(),
            StyleKey.Of<CheckBoxStyle>(),
            StyleKey.Of<HyperlinkButtonStyle>(),
            StyleKey.Of<RadioButtonStyle>(),
            StyleKey.Of<SliderStyle>(),
            StyleKey.Of<ExpanderStyle>(),
            StyleKey.Of<TableStyle>(),
            StyleKey.Of<ScrollBarStyle>(),
            StyleKey.Of<FilePickerDialogStyle>(),
            StyleKey.Of<MessageBoxStyle>(),
            StyleKey.Of<SaveFileDialogStyle>(),
            StyleKey.Of<MenuItemStyle>(),
            StyleKey.Of<MenuSeparatorStyle>(),
            StyleKey.Of<NavigationViewGroupStyle>(),
            StyleKey.Of<NavigationViewItemStyle>(),
            StyleKey.Of<NavigationViewSeparatorStyle>()
        })
        {
            var json = ThemeJson.Create(
                extraStyles: $$""", "{{key}}": { "normal": { "face": { "foreground": "accent" } } } """);

            var exception = Should.Throw<InvalidDataException>(
                () => ThemeCatalog.Parse(json), $"section '{key}' must be rejected");
            exception.Message.ShouldContain(key);
        }
    }

    /// <summary>Verifies an unqualified section no style type owns is still rejected, so the closed
    /// six-name vocabulary has not simply stopped catching typos.</summary>
    [Fact]
    public void Parse_WhenSectionIsAnUnownedUnqualifiedName_Throws()
    {
        var json = ThemeJson.Create(
            extraStyles: """, "buton": { "normal": { "face": { "foreground": "accent" } } } """);

        var exception = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json));

        exception.Message.ShouldContain("buton");
    }

    /// <summary>Verifies a dot-namespaced third-party section - once admitted with no registry
    /// entry needed, as a deliberate escape hatch for a control declared outside the library - is
    /// now rejected exactly like any other unknown name. The dot no longer bypasses validation:
    /// there is no longer any registrable third-party section for it to admit.</summary>
    [Fact]
    public void Parse_WhenSectionIsVendorNamespaced_IsRejected()
    {
        var json = ThemeJson.Create(
            extraStyles: """, "acme.gauge": { "normal": { "face": { "foreground": "accent" } } } """);

        var exception = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json));

        exception.Message.ShouldContain("acme.gauge");
    }

    /// <summary>Verifies a theme can author a partial border, which every other layer accepts.
    ///
    /// <para><c>ParseSectionEnum</c> gated every enum leaf on <c>Enum.IsDefined</c>, which is false
    /// for any <c>[Flags]</c> combination that is not itself a declared member. <c>BorderSide</c> is
    /// the only flags enum reachable through that path, so the only values a theme could write were
    /// its six literal names: <c>"top, bottom"</c> was rejected as an <em>unknown</em> value while
    /// being a well-formed, type-legal one that <c>Border</c>'s and <c>BorderOverlay</c>'s own
    /// constructors accept and that <c>intrinsic-chrome.md</c> documents.</para>
    /// </summary>
    [Theory]
    [InlineData("top, bottom", BorderSide.Top | BorderSide.Bottom)]
    [InlineData("left, right", BorderSide.Left | BorderSide.Right)]
    [InlineData("top", BorderSide.Top)]
    [InlineData("all", BorderSide.All)]
    [InlineData("none", BorderSide.None)]
    public void Parse_WhenBorderSidesAreAFlagsCombination_IsAccepted(string sides, BorderSide expected)
    {
        var json = ThemeJson.Create(containerSides: $"\"{sides}\"");

        var theme = ThemeCatalog.Parse(json);

        theme.GetStyleSet(ContainerStyle.Default).Normal.Border.Sides.ShouldBe(expected);
    }

    /// <summary>The counter-case that keeps the widening honest: bits outside the declared set are
    /// still rejected, exactly as Border's own constructor rejects them.</summary>
    [Fact]
    public void Parse_WhenBorderSidesNameSomethingUndeclared_Throws()
    {
        _ = Should.Throw<InvalidDataException>(() =>
            ThemeCatalog.Parse(ThemeJson.Create(containerSides: "\"top, diagonal\"")));
    }

    /// <summary>Verifies a plain enum still requires an exact declared member, so widening the
    /// flags case did not loosen every other enum leaf.</summary>
    [Fact]
    public void Parse_WhenAPlainEnumValueIsUndeclared_Throws()
    {
        _ = Should.Throw<InvalidDataException>(() =>
            ThemeCatalog.Parse(ThemeJson.Create(inputGlyphStyle: "\"triple\"")));
    }

    /// <summary>Verifies style enum leaves accept names only, never unstable numeric ordinals.</summary>
    [Theory]
    [InlineData("\"0\"", "\"all\"")]
    [InlineData("\"heavy\"", "\"3\"")]
    [InlineData("\"heavy\"", "\"-1\"")]
    public void Parse_WhenStyleSymbolIsNumeric_ThrowsNormalizedInvalidDataException(
        string glyphStyle,
        string sides)
    {
        _ = Should.Throw<InvalidDataException>(() =>
            ThemeCatalog.Parse(ThemeJson.Create(inputGlyphStyle: glyphStyle, inputSides: sides)));
    }

    /// <summary>The counter-case: an unauthored theme keeps the code-owned tab strip - TabControl's
    /// own dividerGlyph/underlineGlyph/dividerColor/selectionIndicatorColor. A theme could once
    /// author a "tabControl" section to move these; a leaf resolves no theme section of its own any
    /// more, so a locally assigned Style is the only surviving door (see
    /// TabControlSurfaceTests.Render_WhenLocalStyleSetsTheStrip_DrawsTheAuthoredDividerAsync)
    /// and this counter-case is now the entire theme-facing story.</summary>
    [Fact]
    public void Parse_WhenTabControlSectionIsAbsent_KeepsTheCodeOwnedStrip()
    {
        var style = TabControlStyle.Definition.Resolve(null, ThemeCatalog.Parse(ThemeJson.Create()));

        style.DividerGlyph.ShouldBe(TabControlStyle.Default.DividerGlyph);
        style.DividerColor.ShouldBe((ControlColor) SemanticColor.ControlBorder);
        style.SelectionIndicatorColor.ShouldBe((ControlColor) SemanticColor.Accent);
    }

    /// <summary>Verifies the key derives from the style type name like every other section, even
    /// though "tabControl" no longer names an authorable section.</summary>
    [Fact]
    public void Of_WhenTabControlStyle_DerivesTheTabControlKey() =>
        StyleKey.Of<TabControlStyle>().ShouldBe("tabControl");

    /// <summary>Verifies a theme can author the window close chrome, which was code-owned and
    /// unreachable while the glyph lived on the control class. "window" is one of the six
    /// well-known role sections and remains fully authorable.
    ///
    /// <para><c>Window.CloseGlyph</c> was a control property defaulting to the internal
    /// <c>ControlGlyphs</c> registry, which nothing in the theme pipeline parses, so a theme
    /// targeting a terminal without dependable box-drawing coverage could not substitute ASCII
    /// chrome - and the two bracket glyphs had no override at all, so even the per-instance
    /// property could not produce a coherent ASCII frame.</para>
    /// </summary>
    [Fact]
    public void Parse_WhenWindowSectionAuthorsCloseChrome_ResolvesTheAuthoredGlyphs()
    {
        var json = ThemeJson.Create(
            windowExtra: """, "closeGlyph": "x", "closeLeftBracket": "(", "closeRightBracket": ")" """);

        var style = ThemeCatalog.Parse(json).GetWindowStyleSet().Normal;

        style.CloseGlyph.ShouldBe(new Rune('x'));
        style.CloseLeftBracket.ShouldBe(new Rune('('));
        style.CloseRightBracket.ShouldBe(new Rune(')'));
    }

    /// <summary>The counter-case: an unauthored theme keeps the code-owned close chrome.</summary>
    [Fact]
    public void Parse_WhenWindowSectionOmitsCloseChrome_KeepsTheCodeOwnedGlyphs()
    {
        var style = ThemeCatalog.Parse(ThemeJson.Create()).GetWindowStyleSet().Normal;

        style.CloseGlyph.ShouldBe(new Rune('■'));
        style.CloseLeftBracket.ShouldBe(new Rune('['));
        style.CloseRightBracket.ShouldBe(new Rune(']'));
    }
}
