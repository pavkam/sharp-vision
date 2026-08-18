// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using System.Reflection;
using System.Text.RegularExpressions;

using SharpVision.Controls.Charts;
using SharpVision.Controls.Collections;
using SharpVision.Controls.Scrolling;
using SharpVision.Dialogs;

/// <summary>Verifies theme section keys are derived from the style types that own them, so a key
/// cannot be spelled one way at its definition and another way in the registry that validates
/// theme documents.</summary>
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

    /// <summary>Verifies leaf control styles drop only the <c>Style</c> suffix and camel-case the
    /// remainder, preserving every key that existed before derivation replaced the literals.</summary>
    [Fact]
    public void Of_WhenTypeIsALeafControlStyle_DerivesTheExistingSectionName()
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

    /// <summary>The regression this file exists to pin. The section registry used to be a
    /// hand-written list of the same literals each style type declared separately, and the two had
    /// drifted: ChartStyle declared "chart" while the registry omitted it, so a theme authoring
    /// styles.chart was rejected outright as an unknown section. Deriving both from the type makes
    /// that class of drift unrepresentable.</summary>
    [Fact]
    public void Parse_WhenThemeAuthorsEveryLibrarySection_IsAccepted()
    {
        foreach (var section in new[]
        {
            "chart", "button", "checkBox", "radioButton", "scrollBar", "chaseIndicator", "slider",
            "progressBar", "spinner", "calendar", "jsonView", "separator", "messageBox",
            "filePickerDialog", "saveFileDialog", "tabControl"
        })
        {
            var json = ThemeJson.Create(
                extraStyles: $$""", "{{section}}": { "normal": { "face": { "foreground": "accent" } } } """);

            var theme = Should.NotThrow(() => ThemeCatalog.Parse(json), $"section '{section}' must be accepted");

            theme.StyleSections.ShouldContainKey(section);
        }
    }

    /// <summary>Verifies an unqualified section no style type owns is still rejected, so the
    /// derived registry has not simply stopped catching typos.</summary>
    [Fact]
    public void Parse_WhenSectionIsAnUnownedUnqualifiedName_Throws()
    {
        var json = ThemeJson.Create(
            extraStyles: """, "buton": { "normal": { "face": { "foreground": "accent" } } } """);

        var exception = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json));

        exception.Message.ShouldContain("buton");
    }

    /// <summary>Verifies a dot-namespaced third-party section is still admitted without any
    /// registry entry - the one case a derived key cannot express, since a type name has no
    /// dot.</summary>
    [Fact]
    public void Parse_WhenSectionIsVendorNamespaced_IsAcceptedWithoutRegistration()
    {
        var json = ThemeJson.Create(
            extraStyles: """, "acme.gauge": { "normal": { "face": { "foreground": "accent" } } } """);

        var theme = ThemeCatalog.Parse(json);

        theme.StyleSections.ShouldContainKey("acme.gauge");
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
        // Section binding is deferred, so the failure surfaces on first resolution rather than
        // at Parse.
        var theme = ThemeCatalog.Parse(ThemeJson.Create(containerSides: "\"top, diagonal\""));

        _ = Should.Throw<InvalidDataException>(() => theme.GetStyleSet(ContainerStyle.Default));
    }

    /// <summary>Verifies a plain enum still requires an exact declared member, so widening the
    /// flags case did not loosen every other enum leaf.</summary>
    [Fact]
    public void Parse_WhenAPlainEnumValueIsUndeclared_Throws()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(inputGlyphStyle: "\"triple\""));

        _ = Should.Throw<InvalidDataException>(() => theme.GetStyleSet(InputStyle.Default));
    }

    /// <summary>Verifies a theme can author the tab strip, which was entirely code-owned.
    ///
    /// <para><c>TabControl</c> kept two one-cell glyphs and two part colors on the control class,
    /// with no style type and no <c>styles.*</c> key. The colors were nullable and resolved to
    /// <c>SemanticColor.ControlBorder</c>/<c>Accent</c> at the draw site when unset, so the default
    /// lived at the render call rather than in a value a theme could overlay - ASCII divider and
    /// underline glyphs, the thing a terminal without box-drawing coverage most needs, had to be
    /// set per instance.</para>
    /// </summary>
    [Fact]
    public void Parse_WhenTabControlSectionAuthorsTheStrip_ResolvesIt()
    {
        var json = ThemeJson.Create(
            extraStyles:
            """, "tabControl": { "normal": { "dividerGlyph": "|", "underlineGlyph": "=", "dividerColor": "warning", "selectionIndicatorColor": "error" } } """);

        var style = TabControlStyle.Definition.Resolve(null, ThemeCatalog.Parse(json));

        style.DividerGlyph.ShouldBe(new Rune('|'));
        style.UnderlineGlyph.ShouldBe(new Rune('='));
        style.DividerColor.ShouldBe((ControlColor) SemanticColor.Warning);
        style.SelectionIndicatorColor.ShouldBe((ControlColor) SemanticColor.Error);
    }

    /// <summary>The counter-case: an unauthored theme keeps the code-owned strip.</summary>
    [Fact]
    public void Parse_WhenTabControlSectionIsAbsent_KeepsTheCodeOwnedStrip()
    {
        var style = TabControlStyle.Definition.Resolve(null, ThemeCatalog.Parse(ThemeJson.Create()));

        style.DividerGlyph.ShouldBe(TabControlStyle.Default.DividerGlyph);
        style.DividerColor.ShouldBe((ControlColor) SemanticColor.ControlBorder);
        style.SelectionIndicatorColor.ShouldBe((ControlColor) SemanticColor.Accent);
    }

    /// <summary>Verifies the key derives from the style type name like every other section.</summary>
    [Fact]
    public void Of_WhenTabControlStyle_DerivesTheTabControlKey() =>
        StyleKey.Of<TabControlStyle>().ShouldBe("tabControl");

    /// <summary>Verifies a theme can author the window close chrome, which was code-owned and
    /// unreachable while the glyph lived on the control class.
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

    /// <summary>Verifies the registered-key table in themes.md lists every own member of every style
    /// it documents.
    ///
    /// <para>That table is the discovery path for theme authoring, and three of its rows misdescribed
    /// their style type: <c>calendar</c> claimed a bare <c>-</c> for a ten-member style, and
    /// <c>jsonView</c> and <c>chart</c> each omitted members. A bare <c>-</c> is a positive assertion
    /// that the section offers nothing beyond the inherited face/border/shadow, so an author wanting to
    /// restyle a calendar's weekday header concluded the section was useless and hand-wrote a local
    /// style per instance - exactly what the section exists to avoid.</para>
    ///
    /// <para>Checked mechanically rather than by eye. Two further rows - <c>scrollBar</c> and
    /// <c>chaseIndicator</c> - were found short only once this ran, which is the argument for the test
    /// over another careful read.</para>
    ///
    /// The regression this test exists to pin.</summary>
    [Fact]
    public void KeyTable_WhenComparedWithTheStyleTypes_ListsEveryOwnMember()
    {
        var rows = ReadKeyTable();
        List<string> problems = [];

        foreach (var (key, listed) in rows)
        {
            var style = StyleTypes.SingleOrDefault(type => StyleKeyFor(type) == key);

            if (style is null)
            {
                problems.Add($"themes.md documents '{key}', which no registrable style type claims");
                continue;
            }

            foreach (var member in OwnMembers(style).Where(member => !listed.Contains(member, StringComparer.Ordinal)))
            {
                problems.Add($"styles.{key} owns '{member}', which the themes.md row omits");
            }
        }

        problems.ShouldBeEmpty();
    }

    // Declared on the leaf itself, and settable: inherited members belong to the fallback style's
    // own row, and a get-only computed property - CheckBoxStyle.MarkWidth, RadioButtonStyle's
    // UncheckedText/CheckedText - is derived from an authorable one rather than authorable itself,
    // so it has no place in a table of what a theme may write.
    private static IEnumerable<string> OwnMembers(Type style) =>
        style
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static property => property is { CanRead: true, CanWrite: true })
            .Select(static property => char.ToLowerInvariant(property.Name[0]) + property.Name[1..]);

    private static IEnumerable<Type> StyleTypes =>
        typeof(ControlStyle).Assembly
            .GetTypes()
            .Where(static type => type.IsSealed && typeof(ControlStyle).IsAssignableFrom(type));

    private static string StyleKeyFor(Type style) =>
        char.ToLowerInvariant(style.Name[0]) + style.Name[1..^"Style".Length];

    private static Dictionary<string, HashSet<string>> ReadKeyTable()
    {
        var path = Path.Combine(RepositoryRoot(), "docs", "concepts", "themes.md");
        var rows = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var line in File.ReadLines(path))
        {
            var match = Regex.Match(line, @"^\| `(\w+)`\s+\|[^|]*\|[^|]*\|(.*)\|\s*$");

            if (!match.Success)
            {
                continue;
            }

            rows[match.Groups[1].Value] = [.. Regex
                .Matches(match.Groups[2].Value, "`(\\w+)`")
                .Select(static glyph => glyph.Groups[1].Value)];
        }

        rows.ShouldNotBeEmpty("the themes.md key table must be readable for this test to mean anything");
        return rows;
    }

    private static string RepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;

        while (!Directory.Exists(Path.Combine(directory, "docs")))
        {
            directory = Path.GetDirectoryName(directory)
                ?? throw new InvalidOperationException("The repository root was not found.");
        }

        return directory;
    }
}
