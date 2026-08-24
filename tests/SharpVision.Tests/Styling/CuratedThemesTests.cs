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
        "solarized-light", "one-dark"
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

    /// <summary>Verifies every curated theme's six well-known role sections ("control"/"input"/
    /// "container"/"window"/"popup"/"tooltip") round-trip through the real reflective engine: each
    /// role's resolved Normal face differs from that role's bare code-owned default, proving the
    /// theme's own "styles.&lt;role&gt;.normal" JSON was actually read and applied rather than
    /// silently ignored (the exact class of bug an earlier root-cause fix addressed, and that this
    /// follow-up investigation continues to guard). Unlike <see cref="EveryCuratedThemeExceptTheDefaults_AuthorsButtonAndResolvesEveryGlyphFamilyStyle"/>,
    /// this covers ALL 15 themes including the two zero-config defaults, since every curated theme
    /// (including "default-dark"/"default-light") authors all six well-known roles - only the eight
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
            ["default-dark"] = GlyphFamily.Default,
            ["default-light"] = GlyphFamily.Default
        };

        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            theme.Glyphs.ShouldBeSameAs(expected[slug], slug);
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
    /// keeping Window, Dialog, and MessageBox bodies distinct from the application backdrop.</summary>
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
            windowFace.Foreground.SemanticColor.ShouldBe(SemanticColor.WindowText, $"{slug} window foreground must use WindowText");

            theme.Resolve(windowFace.Background).ShouldNotBe(
                theme.ResolveColor(SemanticColor.Window),
                $"{slug} a Window must be visually distinct from the application backdrop");
        }
    }

    /// <summary>Verifies every bundled Tooltip profile is framed with a light all-side border
    /// on the same window plane Popup uses, so a passive hint stays visually contained over busy
    /// content while remaining distinct from Popup's rounded frame by glyph style alone.</summary>
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
                $"{slug} tooltip's light border must stay distinct from Popup's rounded frame");
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

    /// <summary>Verifies every shipped palette provides a legible paired selection surface.</summary>
    [Fact]
    public void EveryTheme_UsesContrastingSelectionColors()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var foreground = ThemeColorHelper.SelectionForeground(theme);
            var background = ThemeColorHelper.SelectionBackground(theme);

            if (foreground != Color.Default && background != Color.Default)
            {
                foreground.ShouldNotBe(background, $"{slug} should keep selection text visible");
            }
        }
    }

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
