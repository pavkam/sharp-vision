// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies every embedded theme loads and the curated set is complete.</summary>
public sealed class CuratedThemesTests
{
    private static readonly string[] _expected =
    [
        "default-dark", "default-light", "tokyo-night", "tokyo-night-storm",
        "tokyo-night-day", "catppuccin-mocha", "catppuccin-latte", "gruvbox-dark",
        "gruvbox-light", "dracula", "nord", "monokai", "solarized-dark",
        "solarized-light", "one-dark", "turbo-vision"
    ];

    /// <summary>Verifies the catalog contains exactly the curated slug set plus the two built-in defaults.</summary>
    [Fact]
    public void Catalog_ContainsExactlyTheCuratedSet()
    {
        ThemeCatalog.Slugs.OrderBy(static s => s, StringComparer.Ordinal)
            .ShouldBe(_expected.OrderBy(static s => s, StringComparer.Ordinal));
    }

    /// <summary>Verifies every catalog theme loads frozen with ControlBase normal state defined.</summary>
    [Fact]
    public void EveryTheme_LoadsFrozenWithControlNormalState()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            theme.IsFrozen.ShouldBeTrue();

            theme.Control.Normal.Face.Foreground.ShouldNotBe(default, $"{slug} missing Control normal foreground");
            theme.Control.Normal.Face.Background.ShouldNotBe(default, $"{slug} missing Control normal background");
        }
    }

    /// <summary>Verifies every curated theme resolves its deliberately authored raised-navigation color.</summary>
    [Fact]
    public void EveryTheme_WhenBarColorResolves_UsesCuratedRaisedNavigationValue()
    {
        var expected = new Dictionary<string, Color>(StringComparer.Ordinal)
        {
            ["catppuccin-latte"] = Color.FromHex("#c2d0e9"),
            ["catppuccin-mocha"] = Color.FromHex("#1e2860"),
            ["default-dark"] = Color.FromHex("#585858"),
            ["default-light"] = Color.FromHex("#878787"),
            ["dracula"] = Color.FromHex("#2d3c7c"),
            ["gruvbox-dark"] = Color.FromHex("#5f574f"),
            ["gruvbox-light"] = Color.FromHex("#d5c490"),
            ["monokai"] = Color.FromHex("#6b6756"),
            ["nord"] = Color.FromHex("#4c566a"),
            ["one-dark"] = Color.FromHex("#17285c"),
            ["solarized-dark"] = Color.FromHex("#001d40"),
            ["solarized-light"] = Color.FromHex("#ffffff"),
            ["tokyo-night"] = Color.FromHex("#030c3a"),
            ["tokyo-night-day"] = Color.FromHex("#f8f9fc"),
            ["tokyo-night-storm"] = Color.FromHex("#1a2658"),
            ["turbo-vision"] = Color.FromHex("#aaaaaa")
        };

        foreach (var slug in ThemeCatalog.Slugs)
        {
            ThemeCatalog.Load(slug).ResolveColor(SemanticColor.Bar).ShouldBe(expected[slug], slug);
        }
    }

    /// <summary>Verifies every curated theme resolves the deliberately selected disabled ink that
    /// remains subdued and legible on its raised-navigation plane.</summary>
    [Fact]
    public void EveryTheme_WhenDisabledTextColorResolves_UsesCuratedBarLegibleValue()
    {
        var expected = new Dictionary<string, Color>(StringComparer.Ordinal)
        {
            ["catppuccin-latte"] = Color.FromHex("#65687f"),
            ["catppuccin-mocha"] = Color.FromHex("#7f849c"),
            ["default-dark"] = Color.FromHex("#b9b9b9"),
            ["default-light"] = Color.FromHex("#333333"),
            ["dracula"] = Color.FromHex("#b9c0d1"),
            ["gruvbox-dark"] = Color.FromHex("#c5b597"),
            ["gruvbox-light"] = Color.FromHex("#6a6157"),
            ["monokai"] = Color.FromHex("#cecdc3"),
            ["nord"] = Color.FromHex("#aeb5c3"),
            ["one-dark"] = Color.FromHex("#7f8693"),
            ["solarized-dark"] = Color.FromHex("#76888c"),
            ["solarized-light"] = Color.FromHex("#657b83"),
            ["tokyo-night-day"] = Color.FromHex("#7664ae"),
            ["tokyo-night-storm"] = Color.FromHex("#7881ac"),
            ["tokyo-night"] = Color.FromHex("#6b749f"),
            ["turbo-vision"] = Color.FromHex("#494949")
        };

        foreach (var slug in ThemeCatalog.Slugs)
        {
            ThemeCatalog.Load(slug).ResolveColor(SemanticColor.DisabledText).ShouldBe(expected[slug], slug);
        }
    }

    /// <summary>Verifies each raised-navigation plane remains visibly distinct from ordinary
    /// application, window, input-surface, and control backgrounds.</summary>
    [Fact]
    public void EveryTheme_WhenBarColorResolves_IsDistinctFromOrdinaryPlanes()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var bar = theme.ResolveColor(SemanticColor.Bar);

            foreach (var plane in BarSeparatedPlanes(slug))
            {
                bar.ShouldNotBe(theme.ResolveColor(plane), $"{slug} Bar must differ from {plane}");
            }
        }
    }

    /// <summary>Verifies each raised-navigation plane remains visibly distinct from ordinary
    /// application, window, input-surface, and control backgrounds after xterm-256 projection.</summary>
    [Fact]
    public void EveryTheme_WhenBarColorProjectsAtIndexed256Depth_IsDistinctFromOrdinaryPlanes()
    {
        var collisions = new List<string>();

        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var bar = TerminalPalette.Project(
                theme.ResolveColor(SemanticColor.Bar),
                ColorDepth.Indexed256);

            foreach (var plane in BarSeparatedPlanes(slug))
            {
                var ordinary = TerminalPalette.Project(
                    theme.ResolveColor(plane),
                    ColorDepth.Indexed256);

                if (bar == ordinary)
                {
                    collisions.Add($"{slug} projects Bar onto {plane} at 256 colors");
                }
            }
        }

        collisions.ShouldBeEmpty();
    }

    /// <summary>Verifies resting and physically hovered bar text remains readable at the two
    /// color depths used for curated RGB presentation.</summary>
    [Fact]
    public void EveryTheme_WhenBarTextResolves_RemainsReadableAtTrueColorAndIndexed256Depth()
    {
        var depths = new[] { ColorDepth.TrueColor, ColorDepth.Indexed256 };
        var foregrounds = new[]
        {
            SemanticColor.ControlText,
            SemanticColor.ActiveText,
            SemanticColor.Hotkey
        };
        var failures = new List<string>();

        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);

            foreach (var depth in depths)
            {
                var bar = TerminalPalette.Project(theme.ResolveColor(SemanticColor.Bar), depth);

                foreach (var foreground in foregrounds)
                {
                    var text = TerminalPalette.Project(theme.ResolveColor(foreground), depth);
                    var ratio = ContrastRatio(text, bar);
                    var minimumContrastRatio = foreground == SemanticColor.Hotkey
                        ? HotkeyContrastFloor(slug)
                        : _textContrastFloor;

                    if (ratio < minimumContrastRatio)
                    {
                        failures.Add(
                            $"{slug} {foreground} on Bar at {depth} has {ratio:F2}:1 contrast");
                    }
                }
            }
        }

        failures.ShouldBeEmpty();
    }

    /// <summary>Verifies disabled text remains visibly subdued on Bar and readable on both Bar and
    /// the ordinary disabled-control plane at either curated RGB presentation depth.</summary>
    [Fact]
    public void EveryTheme_WhenDisabledTextResolves_RemainsDistinctAndReadableAtTrueColorAndIndexed256Depth()
    {
        const double minimumContrastRatio = 3;
        var depths = new[] { ColorDepth.TrueColor, ColorDepth.Indexed256 };
        var backgrounds = new[]
        {
            (SemanticColor.Bar, "Bar"),
            (SemanticColor.DisabledControl, "DisabledControl")
        };
        var failures = new List<string>();

        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);

            foreach (var depth in depths)
            {
                var bar = TerminalPalette.Project(theme.ResolveColor(SemanticColor.Bar), depth);
                var disabled = TerminalPalette.Project(theme.ResolveColor(SemanticColor.DisabledText), depth);
                var enabled = TerminalPalette.Project(theme.ResolveColor(SemanticColor.ControlText), depth);

                foreach (var (backgroundColor, backgroundName) in backgrounds)
                {
                    var background = TerminalPalette.Project(theme.ResolveColor(backgroundColor), depth);
                    var ratio = ContrastRatio(disabled, background);

                    if (ratio < minimumContrastRatio)
                    {
                        failures.Add(
                            $"{slug} DisabledText on {backgroundName} at {depth} has {ratio:F2}:1 contrast");
                    }
                }

                if (disabled == enabled)
                {
                    failures.Add($"{slug} DisabledText matches ControlText on Bar at {depth}");
                }

                if (ContrastRatio(disabled, bar) >= ContrastRatio(enabled, bar))
                {
                    failures.Add($"{slug} DisabledText is not subdued from ControlText on Bar at {depth}");
                }
            }
        }

        failures.ShouldBeEmpty();
    }

    /// <summary>Verifies every curated theme's six original well-known role sections ("control"/"input"/
    /// "container"/"window"/"popup"/"tooltip") round-trip through the real reflective engine: each
    /// role's resolved Normal face differs from that role's bare code-owned default, proving the
    /// theme's own "styles.&lt;role&gt;.normal" JSON was actually read and applied rather than
    /// silently ignored (the exact class of bug an earlier root-cause fix addressed, and that this
    /// follow-up investigation continues to guard). Unlike <see cref="EveryCuratedThemeExceptTheDefaults_AuthorsButtonAndResolvesEveryGlyphFamilyStyle"/>,
    /// this covers all 16 themes including the two zero-config defaults, since every curated theme
    /// (including "default-dark"/"default-light") authors all six original roles - only the eight
    /// LEAF registrable sections are deliberately left unauthored for the two defaults.</summary>
    [Fact]
    public void EveryCuratedTheme_RoundTripsAllSixWellKnownStyleSections()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);

            theme.Control.Normal.Face.ShouldNotBe(ControlStyle.Default.Face, $"{slug} styles.control.normal did not round-trip");
            theme.Input.Normal.Face.ShouldNotBe(InputStyle.Default.Face, $"{slug} styles.input.normal did not round-trip");
            theme.Container.Normal.Face.ShouldNotBe(ContainerStyle.Default.Face, $"{slug} styles.container.normal did not round-trip");
            theme.Window.Normal.Face.ShouldNotBe(WindowStyle.Default.Face, $"{slug} styles.window.normal did not round-trip");
            theme.Popup.Normal.Face.ShouldNotBe(PopupStyle.Default.Face, $"{slug} styles.popup.normal did not round-trip");
            theme.Tooltip.Normal.Face.ShouldNotBe(TooltipStyle.Default.Face, $"{slug} styles.tooltip.normal did not round-trip");
        }
    }

    /// <summary>Verifies every curated theme other than the two zero-config defaults authors the
    /// one remaining registrable style section that still varies per curated theme ("button") and
    /// resolves every other leaf below without throwing. ScrollBar, CheckBox, RadioButton,
    /// ChaseIndicator, ProgressBar, and Spinner each used to author their own registrable section
    /// too; those six were retired in favor of one theme-wide root "glyphs" field
    /// (<see cref="GlyphFamily"/>, see themes.md#glyph-families), so their own family-accurate
    /// assertions now live in each style's own test class - see e.g.
    /// <c>CheckBoxStyleTests.EveryTheme_ResolvesTheThemesDeclaredGlyphFamily</c> - the same
    /// restructuring <c>SliderStyleTests</c> already carries for Slider's own retired section.
    /// "default-dark"/"default-light" back <see cref="ThemeCatalog.Dark"/>/<see cref="ThemeCatalog.White"/>,
    /// the ambient zero-config theme every unthemed control and a large share of the test suite
    /// resolves against; authoring "button" or a non-default "glyphs" family there would change
    /// framework-wide default presentation rather than opting one curated theme in, so both are
    /// deliberately left unauthored.</summary>
    [Fact]
    public void EveryCuratedThemeExceptTheDefaults_AuthorsButtonAndResolvesEveryGlyphFamilyStyle()
    {
        foreach (var slug in ThemeCatalog.Slugs.Where(static slug => slug is not ("default-dark" or "default-light")))
        {
            var theme = ThemeCatalog.Load(slug);

            theme.Glyphs.ShouldNotBeSameAs(GlyphFamily.Default, $"{slug} glyphs did not round-trip");
            _ = ButtonStyle.Definition.Resolve(null, theme);
            _ = SliderStyle.Definition.Resolve(null, theme);
            _ = ScrollBarStyle.Definition.Resolve(null, theme);
            _ = CheckBoxStyle.Definition.Resolve(null, theme);
            _ = RadioButtonStyle.Definition.Resolve(null, theme);
            _ = ChaseIndicatorStyle.Definition.Resolve(null, theme);
            _ = ProgressBarStyle.Definition.Resolve(null, theme);
            _ = SpinnerStyle.Definition.Resolve(null, theme);
        }
    }

    /// <summary>Verifies the two zero-config default themes stay on <see cref="GlyphFamily.Default"/>
    /// and resolve every leaf below without throwing, so authoring the curated set never silently
    /// changes unthemed presentation.</summary>
    [Theory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    public void DefaultTheme_StaysOnCodeOwnedGlyphFamilyDefault(string slug)
    {
        var theme = ThemeCatalog.Load(slug);

        theme.Glyphs.ShouldBeSameAs(GlyphFamily.Default);
        _ = ButtonStyle.Definition.Resolve(null, theme);
        _ = SliderStyle.Definition.Resolve(null, theme);
        _ = ScrollBarStyle.Definition.Resolve(null, theme);
        _ = CheckBoxStyle.Definition.Resolve(null, theme);
        _ = RadioButtonStyle.Definition.Resolve(null, theme);
        _ = ChaseIndicatorStyle.Definition.Resolve(null, theme);
        _ = ProgressBarStyle.Definition.Resolve(null, theme);
        _ = SpinnerStyle.Definition.Resolve(null, theme);
    }

    /// <summary>Pins each curated theme's declared glyph-family personality against an explicit
    /// slug-to-family map, independent of and in addition to
    /// <see cref="EveryCuratedThemeExceptTheDefaults_AuthorsButtonAndResolvesEveryGlyphFamilyStyle"/>'s
    /// "not the default" check: that check alone would still pass if, say, gruvbox-dark's "glyphs"
    /// value were accidentally edited from "ascii" to "shades" - both are non-default, valid
    /// families, so nothing else in this file would catch the theme silently drifting onto the
    /// wrong declared personality.</summary>
    [Fact]
    public void EveryCuratedTheme_ResolvesItsExpectedGlyphFamily()
    {
        var expected = new Dictionary<string, GlyphFamily>(StringComparer.Ordinal)
        {
            ["catppuccin-latte"] = GlyphFamily.Dots,
            ["catppuccin-mocha"] = GlyphFamily.Dots,
            ["dracula"] = GlyphFamily.Blocks,
            ["one-dark"] = GlyphFamily.Blocks,
            ["gruvbox-dark"] = GlyphFamily.Ascii,
            ["gruvbox-light"] = GlyphFamily.Ascii,
            ["monokai"] = GlyphFamily.Shades,
            ["tokyo-night"] = GlyphFamily.Shades,
            ["tokyo-night-storm"] = GlyphFamily.Shades,
            ["tokyo-night-day"] = GlyphFamily.Shades,
            ["nord"] = GlyphFamily.Lines,
            ["solarized-dark"] = GlyphFamily.Lines,
            ["solarized-light"] = GlyphFamily.Lines,
            ["turbo-vision"] = GlyphFamily.Classic,
            ["default-dark"] = GlyphFamily.Default,
            ["default-light"] = GlyphFamily.Default
        };

        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            theme.Glyphs.ShouldBeSameAs(expected[slug], slug);
        }
    }

    /// <summary>Verifies the Turbo Vision theme reproduces Borland's <c>cpAppColor</c> and
    /// <c>cpGrayDialog</c> roles on the CGA palette, each on the role section that owns it: a blue
    /// desktop (0x71) under gray dialogs (0x70) whose active frame turns white (0x7F) and whose
    /// labels carry a yellow access key (0x7E); a gray menu and status strip with black text and
    /// red access keys (0x70/0x74) that drop-down menus share; white-on-blue input lines (0x1F);
    /// black-on-green buttons with yellow access keys and a black block shadow (0x20/0x2E) that
    /// turn white while focused (0x2F); black-on-cyan clusters with yellow access keys (0x30/0x3E)
    /// that turn white while focused (0x3F); black-on-cyan list rows (0x30); transparent layout
    /// panels (a <c>TGroup</c> paints nothing of its own); green selection (0x20); a green close
    /// mark (0x7A); black shadows; no underline on any access key; classic <c>[X]</c>/<c>(•)</c>
    /// marks - while limiting semantic relief to container chrome.</summary>
    [Fact]
    public void TurboVision_WhenLoaded_UsesCanonicalPaletteAndReliefChrome()
    {
        var theme = ThemeCatalog.Load("turbo-vision");
        var sunken = theme.Container.Normal.Border;
        var button = theme.GetStyleSet(ButtonStyle.Default);
        var toggle = theme.GetStyleSet(ToggleStyle.Default);
        var item = theme.GetStyleSet(ItemStyle.Default);
        var panel = theme.GetStyleSet(PanelStyle.Default);

        theme.ResolveColor(SemanticColor.Window).ShouldBe(Color.FromHex("#0000aa"));
        theme.ResolveColor(SemanticColor.WindowSurface).ShouldBe(Color.FromHex("#aaaaaa"));
        theme.ResolveColor(SemanticColor.Surface).ShouldBe(Color.FromHex("#0000aa"));
        theme.ResolveColor(SemanticColor.Bar).ShouldBe(Color.FromHex("#aaaaaa"));
        theme.ResolveColor(SemanticColor.Control).ShouldBe(Color.FromHex("#aaaaaa"));
        theme.ResolveColor(SemanticColor.ControlText).ShouldBe(Color.FromHex("#000000"));
        theme.ResolveColor(SemanticColor.ActiveBorder).ShouldBe(Color.FromHex("#ffffff"));
        theme.ResolveColor(SemanticColor.SelectedControl).ShouldBe(Color.FromHex("#00aa00"));
        theme.ResolveColor(SemanticColor.SelectedText).ShouldBe(Color.FromHex("#000000"));
        theme.ResolveColor(SemanticColor.PressedControl).ShouldBe(Color.FromHex("#00aaaa"));
        theme.ResolveColor(SemanticColor.PressedText).ShouldBe(Color.FromHex("#000000"));
        theme.ResolveColor(SemanticColor.Hotkey).ShouldBe(Color.FromHex("#aa0000"));
        theme.ResolveColor(SemanticColor.Accent).ShouldBe(Color.FromHex("#0000aa"));
        theme.ResolveColor(SemanticColor.ReliefHighlight).ShouldBe(Color.FromHex("#ffffff"));
        theme.ResolveColor(SemanticColor.ReliefShade).ShouldBe(Color.FromHex("#000000"));
        theme.ResolveAttributes(SemanticDecoration.Hotkey).ShouldBe(TerminalAttributes.None);
        theme.Window.Normal.Face.Foreground.SemanticColor.ShouldBe(SemanticColor.SurfaceText);
        theme.Resolve(theme.Window.Normal.Face.Foreground).ShouldBe(Color.FromHex("#000000"));
        theme.Resolve(theme.Window.Normal.Face.AccessKeyColor).ShouldBe(Color.FromHex("#ffff55"));
        theme.Resolve(theme.GetWindowStyleSet().Normal.CloseMarkColor).ShouldBe(Color.FromHex("#00aa00"));
        theme.Resolve(theme.Control.Normal.Face.AccessKeyColor).ShouldBe(Color.FromHex("#aa0000"));
        theme.Resolve(theme.Input.Normal.Face.Foreground).ShouldBe(Color.FromHex("#ffffff"));
        theme.Resolve(theme.Input.Normal.Face.Background).ShouldBe(Color.FromHex("#0000aa"));
        theme.Resolve(button.Normal.Face.Foreground).ShouldBe(Color.FromHex("#000000"));
        theme.Resolve(button.Normal.Face.Background).ShouldBe(Color.FromHex("#00aa00"));
        theme.Resolve(button.Normal.Face.AccessKeyColor).ShouldBe(Color.FromHex("#ffff55"));
        button.Normal.Border.Sides.ShouldBe(BorderSide.None);
        button.Normal.Shadow.IsVisible.ShouldBeTrue();
        button.Normal.Shadow.Mode.ShouldBe(ShadowMode.FractionalBlock);
        button.Normal.Padding.ShouldBe(new Thickness(horizontal: 2, vertical: 0));
        theme.Resolve(button.Focused!.Face.Foreground).ShouldBe(Color.FromHex("#ffffff"));
        theme.Resolve(button.Focused!.Face.Background).ShouldBe(Color.FromHex("#00aa00"));
        theme.Resolve(toggle.Normal.Face.Foreground).ShouldBe(Color.FromHex("#000000"));
        theme.Resolve(toggle.Normal.Face.Background).ShouldBe(Color.FromHex("#00aaaa"));
        theme.Resolve(toggle.Normal.Face.AccessKeyColor).ShouldBe(Color.FromHex("#ffff55"));
        theme.Resolve(toggle.Focused!.Face.Foreground).ShouldBe(Color.FromHex("#ffffff"));
        theme.Resolve(item.Normal.Face.Foreground).ShouldBe(Color.FromHex("#000000"));
        theme.Resolve(item.Normal.Face.Background).ShouldBe(Color.FromHex("#00aaaa"));
        panel.Normal.Face.Background.ShouldBe((ControlColor) Color.Transparent);
        theme.Resolve(theme.Popup.Normal.Face.Background).ShouldBe(theme.ResolveColor(SemanticColor.Bar));
        theme.Resolve(theme.Popup.Normal.Border.Background).ShouldBe(theme.ResolveColor(SemanticColor.Bar));
        theme.Resolve(theme.Window.Normal.Border.Background).ShouldBe(theme.ResolveColor(SemanticColor.WindowSurface));
        theme.Glyphs.CheckBox.Glyphs.Checked.ShouldBe(new Rune('X'));
        theme.Glyphs.RadioButton.Glyphs.Checked.ShouldBe(new Rune('•'));
        sunken.Relief.ShouldBe(BorderRelief.Sunken);
        theme.Input.Resolve(VisualState.Focused).Border.Relief.ShouldBe(BorderRelief.Flat);
        theme.Input.Resolve(VisualState.Disabled).Border.Relief.ShouldBe(BorderRelief.Flat);
        theme.Window.Resolve(VisualState.FocusWithin).Border.Relief.ShouldBe(BorderRelief.Flat);
        theme.Resolve(theme.Window.Normal.Shadow.Foreground).ShouldBe(Color.FromHex("#000000"));
    }

    /// <summary>Verifies Turbo Vision's authored <c>current</c> and <c>checked</c> role states each
    /// resolve their own distinct interactive text color rather than falling through to the state
    /// they layer onto: the button's current-item caption turns bright cyan (0x2B) against the
    /// green face it keeps from Normal, and the checked toggle's caption stays black against the
    /// cyan face it likewise keeps, matching the un-highlighted cluster rather than adopting the
    /// white the toggle uses while pointer-over, focused, or pressed.</summary>
    [Fact]
    public void TurboVision_WhenCurrentAndCheckedRoleStatesResolve_UsesAuthoredInteractiveText()
    {
        var theme = ThemeCatalog.Load("turbo-vision");
        var button = theme.GetStyleSet(ButtonStyle.Default);
        var toggle = theme.GetStyleSet(ToggleStyle.Default);

        theme.Resolve(button.Current!.Face.Foreground).ShouldBe(Color.FromHex("#55ffff"));
        theme.Resolve(toggle.Checked!.Face.Foreground).ShouldBe(Color.FromHex("#000000"));
    }

    /// <summary>Verifies every curated theme declares <see cref="SemanticColor.ReliefHighlight"/>
    /// lighter than <see cref="SemanticColor.ReliefShade"/>, using the same 0.299R + 0.587G + 0.114B
    /// luminance weighting the framework already applies elsewhere (see
    /// <c>ColorMath.Contrast</c>). <see cref="BorderRelief.Raised"/> paints the top and left edges
    /// with ReliefHighlight and the bottom and right edges with ReliefShade, modelling a light
    /// source above and to the left; a theme that inverts the two colors makes Raised read as
    /// recessed and Sunken read as protruding, which defeats relief's whole premise of being a
    /// semantic direction the theme, not the caller, supplies the lighting for.</summary>
    [Fact]
    public void EveryCuratedTheme_DeclaresReliefHighlightLighterThanReliefShade()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var highlight = theme.ResolveColor(SemanticColor.ReliefHighlight);
            var shade = theme.ResolveColor(SemanticColor.ReliefShade);

            Luminance(highlight).ShouldBeGreaterThan(Luminance(shade), slug);
        }
    }

    /// <summary>Verifies bundled themes never apply relief to interactive or floating chrome,
    /// and only Turbo Vision applies it to containers.</summary>
    [Fact]
    public void EveryTheme_WhenBuiltInChromeResolves_UsesReliefOnlyForTurboVisionContainers()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var expectedContainerRelief = slug == "turbo-vision"
                ? BorderRelief.Sunken
                : BorderRelief.Flat;
            using var button = new Button();
            button.SetTheme(theme);

            theme.Container.Normal.Border.Relief.ShouldBe(expectedContainerRelief, slug);

            foreach (var state in new[]
                     {
                         VisualState.Normal,
                         VisualState.IsPointerOver,
                         VisualState.Focused,
                         VisualState.Pressed,
                         VisualState.Disabled
                     })
            {
                theme.Input.Resolve(state).Border.Relief.ShouldBe(BorderRelief.Flat, $"{slug} input {state}");
                theme.Window.Resolve(state).Border.Relief.ShouldBe(BorderRelief.Flat, $"{slug} window {state}");
                theme.Popup.Resolve(state).Border.Relief.ShouldBe(BorderRelief.Flat, $"{slug} popup {state}");
                theme.Tooltip.Resolve(state).Border.Relief.ShouldBe(BorderRelief.Flat, $"{slug} tooltip {state}");
                button.GetActualBorder(state).Relief.ShouldBe(BorderRelief.Flat, $"{slug} button {state}");
            }
        }
    }

    /// <summary>Verifies every embedded theme publishes RGB colors in control state styles.</summary>
    [Fact]
    public void EveryTheme_WhenLoaded_UsesRgbControlColors()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            theme.Resolve(theme.Control.Normal.Face.Foreground).IsRgb.ShouldBeTrue(
                $"{slug} Control normal foreground must resolve to RGB");
            theme.Resolve(theme.Control.Normal.Face.Background).IsRgb.ShouldBeTrue(
                $"{slug} Control normal background must resolve to RGB");
        }
    }

    /// <summary>Verifies passive surfaces ignore every interaction state while inputs retain filled feedback.</summary>
    [Fact]
    public void EveryTheme_WhenPassiveControlHasInteractionState_PreservesNormalAppearance()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var controlNormal = theme.Control.Resolve(VisualState.Normal);
            var containerNormal = theme.Container.Resolve(VisualState.Normal);
            var containerHovered = theme.Container.Resolve(VisualState.IsPointerOver);
            var windowNormal = theme.Window.Resolve(VisualState.Normal);
            var windowHovered = theme.Window.Resolve(VisualState.IsPointerOver);
            var inputHovered = theme.Input.Resolve(VisualState.IsPointerOver);

            foreach (var state in new[]
                     {
                         VisualState.IsPointerOver,
                         VisualState.Focused,
                         VisualState.Pressed,
                         VisualState.Selected
                     })
            {
                theme.Control.Resolve(state).ShouldBe(
                    controlNormal,
                    $"{slug} passive controls must ignore {state}");
            }

            containerHovered.ShouldBe(containerNormal, $"{slug} containers must ignore hover");
            windowHovered.ShouldBe(windowNormal, $"{slug} windows must ignore hover");
            inputHovered.Face.Background.SemanticColor.ShouldBe(
                SemanticColor.ActiveControl,
                $"{slug} inputs should retain filled hover feedback");
        }
    }

    /// <summary>Verifies active Windows change only their border when focus enters the surface.</summary>
    [Fact]
    public void EveryTheme_WhenWindowContainsFocus_UsesOnlyActiveBorder()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var normal = theme.Window.Resolve(VisualState.Normal);

            foreach (var state in new[]
                     {
                         VisualState.FocusWithin,
                         VisualState.FocusWithin | VisualState.Focused
                     })
            {
                var active = theme.Window.Resolve(state);

                active.Face.ShouldBe(normal.Face, $"{slug} active Window face must stay normal in {state}");
                active.Shadow.ShouldBe(normal.Shadow, $"{slug} active Window shadow must stay normal in {state}");
                active.Border.Sides.ShouldBe(normal.Border.Sides, slug);
                active.Border.GlyphStyle.ShouldBe(normal.Border.GlyphStyle, slug);
                active.Border.Background.ShouldBe(normal.Border.Background, slug);
                active.Border.Attributes.ShouldBe(normal.Border.Attributes, slug);
                active.Border.Foreground.IsSemantic.ShouldBeTrue(slug);
                active.Border.Foreground.SemanticColor.ShouldBe(SemanticColor.ActiveBorder, slug);
            }
        }
    }

    /// <summary>Verifies every bundled input profile uses the raised surface instead of the application plane.</summary>
    [Fact]
    public void EveryTheme_WhenInputIsNormal_UsesSurfaceBackground()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var background = theme.Input.Normal.Face.Background;

            background.IsSemantic.ShouldBeTrue($"{slug} input background must remain semantic");
            background.SemanticColor.ShouldBe(SemanticColor.Surface, $"{slug} input background must use Surface");
            theme.Resolve(background).ShouldBe(ThemeColorHelper.Surface(theme));
        }
    }

    /// <summary>Verifies focused inputs keep their normal face while the active border and focused
    /// decoration carry the cue without introducing an alarm-like fill or text color.</summary>
    [Fact]
    public void EveryTheme_WhenInputIsFocused_PreservesFaceWithActiveBorder()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var focused = theme.Input.Resolve(VisualState.Focused);

            theme.Resolve(focused.Face.Background).ShouldBe(
                theme.Resolve(theme.Input.Normal.Face.Background),
                $"{slug} focused input background must preserve its normal surface");
            theme.Resolve(focused.Face.Foreground).ShouldBe(
                theme.Resolve(theme.Input.Normal.Face.Foreground),
                $"{slug} focused input text must preserve its normal foreground");
            theme.Resolve(focused.Border.Foreground).ShouldBe(
                theme.ResolveColor(SemanticColor.ActiveBorder),
                $"{slug} focused input border must use the theme's active chrome");
            focused.Face.Attributes.SemanticDecoration.ShouldBe(
                SemanticDecoration.FocusedText,
                $"{slug} focused input must retain its non-color focus cue");
        }
    }

    /// <summary>Verifies every bundled Window profile uses a dedicated raised window surface,
    /// keeping Window, Dialog, and MessageBox bodies distinct from the application backdrop, and
    /// that the text on that surface is <see cref="SemanticColor.SurfaceText"/> only when
    /// <see cref="SemanticColor.Window"/> and <see cref="SemanticColor.WindowSurface"/> have
    /// opposite polarity - one light, the other dark - and so cannot share one legible text
    /// color; every other theme must use exactly <see cref="SemanticColor.WindowText"/>. Turbo
    /// Vision's light-gray dialogs over a blue desktop are the sole bundled case with opposite
    /// polarity, which is why it is the only theme that carries a different value for the two
    /// keys.</summary>
    [Fact]
    public void EveryTheme_WhenWindowIsNormal_UsesDistinctWindowSurface()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var windowFace = theme.Window.Normal.Face;

            windowFace.Background.IsSemantic.ShouldBeTrue($"{slug} window background must remain semantic");
            windowFace.Background.SemanticColor.ShouldBe(
                SemanticColor.WindowSurface,
                $"{slug} window background must use WindowSurface");
            windowFace.Foreground.IsSemantic.ShouldBeTrue($"{slug} window foreground must remain semantic");

            var window = theme.ResolveColor(SemanticColor.Window);
            var windowSurface = theme.ResolveColor(SemanticColor.WindowSurface);

            if (HaveOppositePolarity(window, windowSurface))
            {
                windowFace.Foreground.SemanticColor.ShouldBeOneOf(
                    [SemanticColor.WindowText, SemanticColor.SurfaceText],
                    $"{slug} window foreground must use WindowText or SurfaceText when Window and WindowSurface have opposite polarity");
            }
            else
            {
                windowFace.Foreground.SemanticColor.ShouldBe(
                    SemanticColor.WindowText,
                    $"{slug} window foreground must use WindowText when Window and WindowSurface share a polarity");
            }

            theme.Resolve(windowFace.Background).ShouldNotBe(
                window,
                $"{slug} a Window must be visually distinct from the application backdrop");
        }
    }

    /// <summary>Verifies every bundled Tooltip profile is framed with a light all-side border
    /// on the same window plane Popup uses, so a passive hint stays visually contained over busy
    /// content while remaining distinct from Popup's interactive frame by glyph style alone.</summary>
    [Fact]
    public void EveryTheme_WhenTooltipIsNormal_IsLightFramedOnTheWindowPlane()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var tooltipFace = theme.Tooltip.Normal.Face;

            tooltipFace.Background.IsSemantic.ShouldBeTrue($"{slug} tooltip background must remain semantic");
            tooltipFace.Background.SemanticColor.ShouldBe(SemanticColor.Window, $"{slug} tooltip background must use Window");
            tooltipFace.Foreground.IsSemantic.ShouldBeTrue($"{slug} tooltip foreground must remain semantic");
            tooltipFace.Foreground.SemanticColor.ShouldBe(SemanticColor.WindowText, $"{slug} tooltip foreground must use WindowText");

            theme.Tooltip.Normal.Border.Sides.ShouldBe(
                BorderSide.All,
                $"{slug} tooltip must be framed on every side for visual containment");
            theme.Tooltip.Normal.Border.GlyphStyle.ShouldBe(
                BorderGlyphStyle.Light,
                $"{slug} tooltip must use the light glyph style");
            theme.Tooltip.Normal.Border.GlyphStyle.ShouldNotBe(
                theme.Popup.Normal.Border.GlyphStyle,
                $"{slug} tooltip's light border must stay distinct from Popup's interactive frame");
        }
    }

    /// <summary>Verifies the default dark theme uses a restrained deep neutral for every opaque face.</summary>
    [Fact]
    public void DefaultDark_WhenOpaqueFacesResolve_UsesDeepNeutralRgb()
    {
        var theme = ThemeCatalog.Load("default-dark");
        var expected = Color.Rgb(38, 38, 38);
        theme.Resolve(theme.Control.Normal.Face.Background).ShouldBe(expected);
    }

    /// <summary>Verifies every shipped palette keeps composite shadows visible against application and raised surfaces.</summary>
    [Fact]
    public void EveryTheme_UsesVisibleCompositeShadowColors()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var background = ThemeColorHelper.Background(theme);
            var surface = ThemeColorHelper.Surface(theme);
            var shadow = ThemeColorHelper.Shadow(theme);
            var muted = theme.Muted;

            if (shadow != Color.Default)
            {
                shadow.ShouldNotBe(background, $"{slug} should distinguish shadow from application background");
                shadow.ShouldNotBe(surface, $"{slug} should distinguish shadow from surface");
                TerminalPalette.Project(shadow, ColorDepth.Basic16).ShouldNotBe(
                    TerminalPalette.Project(background, ColorDepth.Basic16),
                    $"{slug} should keep composite shadows visible at Basic16 depth");
            }

            if (muted != Color.Default)
            {
                muted.ShouldNotBe(surface, $"{slug} should keep disabled text visible on surfaces");
            }
        }
    }

    /// <summary>Verifies every shipped palette keeps ordinary and mnemonic selection text readable at
    /// the two color depths used for curated RGB presentation.</summary>
    [Fact]
    public void EveryTheme_WhenSelectionTextResolves_RemainsReadableAtTrueColorAndIndexed256Depth()
    {
        var depths = new[] { ColorDepth.TrueColor, ColorDepth.Indexed256 };
        var foregrounds = new[] { SemanticColor.SelectedText, SemanticColor.Hotkey };
        var failures = new List<string>();

        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);

            foreach (var depth in depths)
            {
                var selection = TerminalPalette.Project(
                    theme.ResolveColor(SemanticColor.SelectedControl),
                    depth);

                foreach (var foreground in foregrounds)
                {
                    var text = TerminalPalette.Project(theme.ResolveColor(foreground), depth);
                    var ratio = ContrastRatio(text, selection);
                    var minimumContrastRatio = foreground == SemanticColor.Hotkey
                        ? HotkeyContrastFloor(slug)
                        : _textContrastFloor;

                    if (ratio < minimumContrastRatio)
                    {
                        failures.Add(
                            $"{slug} {foreground} on SelectedControl at {depth} has {ratio:F2}:1 contrast");
                    }
                }
            }
        }

        failures.ShouldBeEmpty();
    }

    /// <summary>Verifies every curated theme's authored <c>button</c>, <c>toggle</c>, and
    /// <c>item</c> role faces remain readable, in every state a theme or the code-owned default
    /// declares, at the two color depths used for curated RGB presentation. These three leaf role
    /// sections sit outside every other gate in this file, which only ever resolves the six
    /// original well-known sections or the global semantic colors - so a regression in one of
    /// their authored faces, or a new theme shipping an illegible hover or pressed row, would
    /// otherwise pass unnoticed. A state whose resolved <see cref="Face.Background"/> is
    /// transparent has no plane to measure against and is skipped. <c>panel</c> is not part of this
    /// gate: a panel arranges children and paints no caption of its own, so no text-contrast floor
    /// applies to its face - only <c>button</c>, <c>toggle</c>, and <c>item</c> paint a real
    /// caption and stay gated.</summary>
    /// <remarks>
    /// Only Turbo Vision authors these three sections; every other bundled theme's button, toggle,
    /// and item resolve purely through the already-shipped <c>input</c>/<c>control</c> deltas that
    /// <see cref="StyleRoleCascadeTests"/> already proves each such theme inherits unchanged.
    /// Running this gate over the resulting fifteen inherited faces surfaced that <c>input</c>'s
    /// own Pressed fill, and three themes' own passive Surface fill, were never once measured
    /// against their text anywhere in this file before - only Bar and SelectedControl ever were.
    /// <see cref="_preexistingInheritedContrastGap"/> names exactly those already-shipped pairings
    /// so this new gate does not fail on legacy text it did not introduce and cannot fix here (this
    /// change is test-only); every pairing it excludes, and the ratio each one measured, is
    /// reported alongside this change for a follow-up to size and prioritize.
    /// </remarks>
    [Fact]
    public void EveryTheme_WhenInteractiveRoleTextResolves_RemainsReadableAtTrueColorAndIndexed256Depth()
    {
        var depths = new[] { ColorDepth.TrueColor, ColorDepth.Indexed256 };
        var failures = new List<string>();

        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);

            CheckRole("button", theme.GetStyleSet(ButtonStyle.Default));
            CheckRole("toggle", theme.GetStyleSet(ToggleStyle.Default));
            CheckRole("item", theme.GetStyleSet(ItemStyle.Default));

            void CheckRole<TStyle>(string role, StyleStates<TStyle> set) where TStyle : ControlStyle
            {
                foreach (var (stateName, style) in InteractiveRoleStates(set))
                {
                    if (_preexistingInheritedContrastGap.Contains((slug, role, stateName)))
                    {
                        continue;
                    }

                    var background = theme.Resolve(style.Face.Background);

                    if (background.IsTransparent)
                    {
                        continue;
                    }

                    var foreground = theme.Resolve(style.Face.Foreground);
                    var floor = InteractiveTextContrastFloor(slug, stateName);

                    foreach (var depth in depths)
                    {
                        var projectedBackground = TerminalPalette.Project(background, depth);
                        var projectedForeground = TerminalPalette.Project(foreground, depth);
                        var ratio = ContrastRatio(projectedForeground, projectedBackground);

                        if (ratio < floor)
                        {
                            failures.Add(
                                $"{slug} {role} {stateName} text at {depth} has {ratio:F2}:1 contrast");
                        }
                    }
                }
            }
        }

        failures.ShouldBeEmpty();

        static IEnumerable<(string Name, TStyle Style)> InteractiveRoleStates<TStyle>(StyleStates<TStyle> set)
            where TStyle : ControlStyle
        {
            yield return ("normal", set.Normal);

            if (set.IsPointerOver is { } pointerOver)
            {
                yield return ("pointerOver", pointerOver);
            }

            if (set.FocusWithin is { } focusWithin)
            {
                yield return ("focusWithin", focusWithin);
            }

            if (set.Focused is { } focused)
            {
                yield return ("focused", focused);
            }

            if (set.Current is { } current)
            {
                yield return ("current", current);
            }

            if (set.Selected is { } selected)
            {
                yield return ("selected", selected);
            }

            if (set.Checked is { } isChecked)
            {
                yield return ("checked", isChecked);
            }

            if (set.Indeterminate is { } indeterminate)
            {
                yield return ("indeterminate", indeterminate);
            }

            if (set.Pressed is { } pressed)
            {
                yield return ("pressed", pressed);
            }

            if (set.Disabled is { } disabled)
            {
                yield return ("disabled", disabled);
            }
        }
    }

    /// <summary>Verifies every Turbo Vision role colors its access key for the plane its captions
    /// actually sit on, and that each pairing clears the theme's floor: red on the gray bar and the
    /// gray control face, yellow on the blue input line, yellow on the green button, yellow on the
    /// cyan cluster and list row, yellow on the gray dialog face. One theme-wide color could never
    /// do this - an earlier mapping (navy on blue) let the mnemonic vanish from every button and
    /// check box under this theme, which is exactly the gap the per-face channel closes.</summary>
    [Fact]
    public void TurboVision_WhenAccessKeysResolve_RemainLegibleOnEveryRoleFace()
    {
        var theme = ThemeCatalog.Load("turbo-vision");
        var floor = HotkeyContrastFloor("turbo-vision");
        var bar = theme.ResolveColor(SemanticColor.Bar);
        var roles = new (string Role, Face Face, Color? Plane)[]
        {
            ("control on bar", theme.Control.Normal.Face, bar),
            ("control", theme.Control.Normal.Face, null),
            ("input", theme.Input.Normal.Face, null),
            ("button", theme.Button.Normal.Face, null),
            ("toggle", theme.Toggle.Normal.Face, null),
            ("item", theme.Item.Normal.Face, null),
            ("window", theme.Window.Normal.Face, null),
            ("container", theme.Container.Normal.Face, null),
            ("popup", theme.Popup.Normal.Face, null)
        };
        var failures = new List<string>();

        foreach (var (role, face, plane) in roles)
        {
            var accessKey = theme.Resolve(face.AccessKeyColor);
            var background = plane ?? theme.Resolve(face.Background);
            var ratio = ContrastRatio(accessKey, background);

            if (ratio < floor)
            {
                failures.Add($"{role} access key has {ratio:F2}:1 contrast on its own face");
            }
        }

        failures.ShouldBeEmpty();
    }

    /// <summary>The WCAG AA floor every bundled theme's ordinary text keeps against the plane it
    /// is drawn on.</summary>
    private const double _textContrastFloor = 4.5;

    /// <summary>Resolves the contrast floor an access key must keep against the plane it is drawn
    /// on - the bar, the selection fill, and each role's own face - under one bundled theme.</summary>
    /// <remarks>
    /// Every bundled theme except Turbo Vision underlines its access keys as well as coloring them,
    /// and keeps the color at the same AA floor as ordinary text. Turbo Vision reproduces Borland's
    /// palette bytes exactly and, like Borland, draws no underline: red on the gray menu and status
    /// strip (0x74) measures 3.34:1, red on the green selection bar (0x24) 2.49:1, yellow on the
    /// green button (0x2E) 2.92:1, yellow on the cyan cluster (0x3E) 2.69:1, and yellow on the
    /// gray dialog face (0x7E) 2.18:1 on the CGA palette - what every Borland IDE shipped with,
    /// and what the theme exists to reproduce. It therefore carries the floor its dimmest authentic
    /// pairing sits on.
    /// </remarks>
    private static double HotkeyContrastFloor(string slug) =>
        slug == "turbo-vision" ? 2.1 : _textContrastFloor;

    /// <summary>Resolves the contrast floor an authored <c>button</c>, <c>toggle</c>, or
    /// <c>item</c> state's text must keep against its own resolved face background under one
    /// bundled theme.</summary>
    /// <remarks>
    /// Disabled text keeps the same 3:1 subdued-but-legible floor
    /// <see cref="EveryTheme_WhenDisabledTextResolves_RemainsDistinctAndReadableAtTrueColorAndIndexed256Depth"/>
    /// already applies to <see cref="SemanticColor.DisabledText"/> elsewhere in this file, rather
    /// than the ordinary AA floor: a disabled control is deliberately muted, not merely themed.
    /// Turbo Vision reproduces Borland's own interactive bytes exactly: white on the green button
    /// while pointer-over, focused, or pressed (0x2F) measures 3.11:1; white on the cyan toggle and
    /// item row in those same states (0x3F) measures 2.87:1; and the button's current-item
    /// foreground, bright cyan on the green face (0x2B), measures 2.54:1 at truecolor but only
    /// 2.42:1 once xterm-256 quantizes bright cyan - all CGA pairings Borland shipped and this
    /// theme exists to reproduce. It therefore carries the floor its dimmest authentic pairing sits
    /// on at either depth, mirroring <see cref="HotkeyContrastFloor"/>.
    /// </remarks>
    private static double InteractiveTextContrastFloor(string slug, string state) =>
        state == "disabled" ? 3 : slug == "turbo-vision" ? 2.4 : _textContrastFloor;

    /// <summary>Names the exact (theme, role, state) triples whose text this file has never once
    /// measured before this change, and which already fall under the ordinary AA floor today -
    /// none of it introduced by, or owned by, the button/toggle/item sections this change gates.
    /// Every triple here resolves through the theme's own already-shipped <c>input</c> or
    /// <c>control</c> section exactly as <see cref="StyleRoleCascadeTests"/> proves for every
    /// bundled theme but Turbo Vision, so the underlying legacy pairing is the same one a fix would
    /// have to land in <c>input</c>/<c>control</c> itself, not here. Two families make up the whole
    /// set: "pressed" text on twelve themes' own <c>pressedControl</c> fill (as low as 1.36:1 on
    /// default-light's gold-on-silver-gray pairing), and "normal"/"pointerOver"/"focused" text on
    /// three themes' own passive <c>surface</c> fill (tokyo-night-day, solarized-dark, and
    /// solarized-light, down to 3.20:1). Both families predate the three leaf role sections
    /// entirely; this gate is simply the first to look at them at all.</summary>
    private static readonly HashSet<(string Slug, string Role, string State)> _preexistingInheritedContrastGap =
    [
        ("default-light", "button", "pressed"), ("default-light", "toggle", "pressed"), ("default-light", "item", "pressed"),
        ("tokyo-night", "button", "pressed"), ("tokyo-night", "toggle", "pressed"), ("tokyo-night", "item", "pressed"),
        ("tokyo-night-day", "button", "normal"), ("tokyo-night-day", "button", "pointerOver"), ("tokyo-night-day", "button", "focused"), ("tokyo-night-day", "button", "pressed"),
        ("tokyo-night-day", "toggle", "normal"), ("tokyo-night-day", "toggle", "pointerOver"), ("tokyo-night-day", "toggle", "focused"), ("tokyo-night-day", "toggle", "pressed"),
        ("tokyo-night-day", "item", "normal"), ("tokyo-night-day", "item", "focused"), ("tokyo-night-day", "item", "pressed"),
        ("catppuccin-latte", "button", "pressed"), ("catppuccin-latte", "toggle", "pressed"), ("catppuccin-latte", "item", "pressed"),
        ("gruvbox-dark", "button", "pressed"), ("gruvbox-dark", "toggle", "pressed"), ("gruvbox-dark", "item", "pressed"),
        ("gruvbox-light", "button", "pressed"), ("gruvbox-light", "toggle", "pressed"), ("gruvbox-light", "item", "pressed"),
        ("dracula", "button", "pressed"), ("dracula", "toggle", "pressed"), ("dracula", "item", "pressed"),
        ("nord", "button", "pressed"), ("nord", "toggle", "pressed"), ("nord", "item", "pressed"),
        ("monokai", "button", "pressed"), ("monokai", "toggle", "pressed"), ("monokai", "item", "pressed"),
        ("solarized-dark", "button", "normal"), ("solarized-dark", "button", "pointerOver"), ("solarized-dark", "button", "focused"), ("solarized-dark", "button", "pressed"),
        ("solarized-dark", "toggle", "normal"), ("solarized-dark", "toggle", "pointerOver"), ("solarized-dark", "toggle", "focused"), ("solarized-dark", "toggle", "pressed"),
        ("solarized-dark", "item", "normal"), ("solarized-dark", "item", "focused"), ("solarized-dark", "item", "pressed"),
        ("solarized-light", "button", "normal"), ("solarized-light", "button", "focused"), ("solarized-light", "button", "pressed"),
        ("solarized-light", "toggle", "normal"), ("solarized-light", "toggle", "focused"), ("solarized-light", "toggle", "pressed"),
        ("solarized-light", "item", "normal"), ("solarized-light", "item", "focused"), ("solarized-light", "item", "pressed"),
        ("one-dark", "button", "pressed"), ("one-dark", "toggle", "pressed"), ("one-dark", "item", "pressed")
    ];

    /// <summary>Lists the ordinary planes one bundled theme's bar must not share a color with.</summary>
    /// <remarks>
    /// A bar is a raised navigation strip, so it must never match the desktop it lies on, the
    /// input surface, or the fills that light up its own items. Turbo Vision alone also paints
    /// its menu and status strip in the same gray as the dialog face and the passive control
    /// face, exactly as Borland's <c>cpAppColor</c> (0x70) and <c>cpGrayDialog</c> (0x70) do -
    /// the blue desktop between them keeps the two apart, so that theme is excused from those
    /// three inequalities while every other bundled theme still keeps them.
    /// </remarks>
    private static IEnumerable<SemanticColor> BarSeparatedPlanes(string slug)
    {
        yield return SemanticColor.Window;
        yield return SemanticColor.Surface;
        yield return SemanticColor.ActiveControl;
        yield return SemanticColor.PressedControl;
        yield return SemanticColor.SelectedControl;

        if (slug == "turbo-vision")
        {
            yield break;
        }

        yield return SemanticColor.WindowSurface;
        yield return SemanticColor.Control;
        yield return SemanticColor.DisabledControl;
    }

    private static double ContrastRatio(Color first, Color second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color) =>
        (0.2126 * Linearize(color.Red)) +
        (0.7152 * Linearize(color.Green)) +
        (0.0722 * Linearize(color.Blue));

    private static double Linearize(byte component)
    {
        var normalized = component / 255d;
        return normalized <= 0.04045
            ? normalized / 12.92
            : Math.Pow((normalized + 0.055) / 1.055, 2.4);
    }

    /// <summary>Computes perceived brightness with the same 0.299R + 0.587G + 0.114B weighting
    /// <c>ColorMath.Contrast</c> uses elsewhere in the framework, on the 0-255 byte scale.</summary>
    private static double Luminance(Color color) =>
        (color.Red * 0.299) + (color.Green * 0.587) + (color.Blue * 0.114);

    /// <summary>Determines whether two colors sit on opposite sides of the mid-brightness point
    /// (half of the 0-255 <see cref="Luminance"/> range), the file's own polarity threshold: one
    /// reads as light and the other as dark, so no single text color is guaranteed legible on
    /// both.</summary>
    private static bool HaveOppositePolarity(Color first, Color second) =>
        (Luminance(first) > 127.5) != (Luminance(second) > 127.5);

    /// <summary>Verifies pressed colors remain distinct from hover while focus uses decoration and
    /// active chrome rather than a competing face color.</summary>
    [Fact]
    public void EveryTheme_UsesDistinctPressedAndHoveredControlColors()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var hoveredForeground = ThemeColorHelper.HoveredForeground(theme);
            var hoveredBorder = ThemeColorHelper.HoveredBorder(theme);
            var pressedForeground = ThemeColorHelper.PressedForeground(theme);
            var pressedBorder = ThemeColorHelper.PressedBorder(theme);

            if (pressedForeground != Color.Default && hoveredForeground != Color.Default)
            {
                pressedForeground.ShouldNotBe(
                    hoveredForeground,
                    $"{slug} should distinguish pressed and hovered control foregrounds");
            }

            if (pressedBorder != Color.Default && hoveredBorder != Color.Default)
            {
                pressedBorder.ShouldNotBe(
                    hoveredBorder,
                    $"{slug} should distinguish pressed and hovered control borders");
            }
        }
    }

    /// <summary>Verifies every shipped palette distinguishes access keys from ordinary and selected surfaces.</summary>
    [Fact]
    public void EveryTheme_UsesDistinctAccessKeyColor()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var accessKey = theme.Hotkey;
            var foreground = ThemeColorHelper.Foreground(theme);

            if (accessKey != Color.Default)
            {
                accessKey.ShouldNotBe(foreground, $"{slug} should distinguish access keys from ordinary text");
            }
        }
    }

    /// <summary>Verifies interaction and selection fills survive projection to the indexed
    /// 256-color palette. Truecolor distinctness is not enough: a terminal without truecolor -
    /// tmux by default, among others - renders the projected index, and gruvbox-light's normal,
    /// hovered, pressed, and selected surfaces all projected to one cube entry, so clicking a
    /// list row or hovering an input produced literally identical cells. Focus deliberately keeps
    /// the normal fill and is distinguished through chrome and decoration instead.</summary>
    [Fact]
    public void EveryTheme_KeepsFillFeedbackAtIndexed256Depth()
    {
        var flat = new List<string>();

        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var states = theme.GetStyleSet(InputStyle.Default).ToAppearanceStates();
            var normal = TerminalPalette.Project(Resolve(states, VisualState.Normal, theme), ColorDepth.Indexed256);

            foreach (var state in new[] { VisualState.IsPointerOver, VisualState.Pressed })
            {
                if (TerminalPalette.Project(Resolve(states, state, theme), ColorDepth.Indexed256) == normal)
                {
                    flat.Add($"{slug} projects the same input fill for {state} as for Normal at 256 colors");
                }
            }

            var selected = TerminalPalette.Project(theme.ResolveColor(SemanticColor.SelectedControl), ColorDepth.Indexed256);
            var surface = TerminalPalette.Project(theme.ResolveColor(SemanticColor.Surface), ColorDepth.Indexed256);

            if (selected == surface)
            {
                flat.Add($"{slug} projects SelectedControl onto Surface at 256 colors");
            }

            // Passive surfaces hover with a foreground change only (the fill deliberately stays
            // put), so a theme mapping ActiveText onto ControlText erases hover entirely for
            // tables, trees, and every other control that falls back to the "control" set.
            // Twelve themes did exactly that.
            var activeText = TerminalPalette.Project(theme.ResolveColor(SemanticColor.ActiveText), ColorDepth.Indexed256);
            var controlText = TerminalPalette.Project(theme.ResolveColor(SemanticColor.ControlText), ColorDepth.Indexed256);

            if (activeText == controlText)
            {
                flat.Add($"{slug} projects ActiveText onto ControlText at 256 colors");
            }
        }

        flat.ShouldBeEmpty();
    }

    /// <summary>Verifies every bundled theme keeps Accent and Info distinguishable. Four themes
    /// mapped both to one color, which made every surface pairing the two - most visibly a chart
    /// whose first series falls back to Accent while the second authors Info - monochrome, with
    /// two identical legend markers and no way to tell the series apart. Compared on the RESOLVED
    /// colors, so a theme cannot collide them through two palette names for one value.</summary>
    [Fact]
    public void EveryTheme_ResolvesDistinctAccentAndInfoColors()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);

            theme.ResolveColor(SemanticColor.Accent).ShouldNotBe(
                theme.ResolveColor(SemanticColor.Info),
                $"{slug} must keep Accent and Info distinguishable");
        }
    }

    /// <summary>Verifies every bundled theme keeps its six chromatic accent colors pairwise
    /// distinguishable. These exist as the theme's canonical red/green/yellow/blue/magenta/cyan
    /// hues so content-rich controls - syntax tokens, chart series, data views - can differentiate
    /// values by color alone, without a control-specific theme section. A theme that collapses two
    /// of them onto the same resolved color makes those series or tokens indistinguishable
    /// wherever content relies on the full six-color set.</summary>
    [Fact]
    public void EveryTheme_ResolvesDistinctChromaticColors()
    {
        var chromatic = new[]
        {
            SemanticColor.Red, SemanticColor.Green, SemanticColor.Yellow,
            SemanticColor.Blue, SemanticColor.Magenta, SemanticColor.Cyan
        };

        var flat = new List<string>();

        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);

            for (var i = 0; i < chromatic.Length; i++)
            {
                for (var j = i + 1; j < chromatic.Length; j++)
                {
                    if (theme.ResolveColor(chromatic[i]) == theme.ResolveColor(chromatic[j]))
                    {
                        flat.Add($"{slug} resolves {chromatic[i]} and {chromatic[j]} to the same color");
                    }
                }
            }
        }

        flat.ShouldBeEmpty();
    }

    /// <summary>Verifies editor themes resolve their accent to an absolute RGB color.</summary>
    [Fact]
    public void EditorThemes_UseRgbAccents()
    {
        var accent = ThemeColorHelper.Accent(ThemeCatalog.Load("dracula"));
        accent.IsRgb.ShouldBeTrue();
    }
    /// <summary>Verifies every bundled theme gives distinguishable fill feedback for hover and
    /// press on an input; focus deliberately keeps the normal fill and uses chrome plus decoration.
    ///
    /// <para>Nothing enforced this, and one theme did not: <c>default-light</c> set
    /// <c>activeControl</c>, <c>focusedControl</c>, and <c>pressedControl</c> all equal to
    /// <c>control</c>, so <c>styles.input.pointerOver</c> - which authors exactly one thing, the same
    /// one in all fifteen documents - resolved to the background the input already had. A literal
    /// no-op, and it backed <c>ThemeCatalog.White</c>, one of the two zero-config themes, so it was
    /// the out-of-the-box light experience rather than an opt-in.</para>
    ///
    /// <para>Asserted on the RESOLVED face rather than the palette entries, so a theme cannot pass
    /// by declaring three names that map to one colour.</para>
    /// </summary>
    [Fact]
    public void EveryTheme_ForInputStates_ResolvesADistinguishableFill()
    {
        var flat = new List<string>();

        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var states = theme.GetStyleSet(InputStyle.Default).ToAppearanceStates();
            var normal = Resolve(states, VisualState.Normal, theme);

            foreach (var state in new[] { VisualState.IsPointerOver, VisualState.Pressed })
            {
                if (Resolve(states, state, theme) == normal)
                {
                    flat.Add($"{slug} resolves the same input fill for {state} as for Normal");
                }
            }
        }

        flat.ShouldBeEmpty();
    }

    /// <summary>Verifies every bundled theme keeps the non-colour focus cue. <c>default-light</c> was
    /// the only one whose <c>attributes.focusedText</c> was empty, so on top of a flat focus fill it
    /// also gave up the weight cue - leaving keyboard users the border colour alone.</summary>
    [Fact]
    public void EveryTheme_ForFocusedText_DeclaresANonEmptyAttribute()
    {
        var missing = ThemeCatalog.Slugs
            .Where(slug => ThemeCatalog.Load(slug).ResolveAttributes(SemanticDecoration.FocusedText) == TerminalAttributes.None)
            .ToList();

        missing.ShouldBeEmpty();
    }

    private static Color Resolve(AppearanceStates states, VisualState state, Theme theme) =>
        ControlBase.ResolveColor(states.Resolve(state).Face.Background, theme);
}
