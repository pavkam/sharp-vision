// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using System.Reflection;

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
            theme.Frozen.ShouldBeTrue();

            theme.Control.Normal.Face.Foreground.ShouldNotBe(default, $"{slug} missing Control normal foreground");
            theme.Control.Normal.Face.Background.ShouldNotBe(default, $"{slug} missing Control normal background");
        }
    }

    /// <summary>Verifies every curated theme's six well-known role sections ("control"/"input"/
    /// "container"/"window"/"popup"/"tooltip") round-trip through the real reflective engine: each
    /// role's resolved Normal face differs from that role's bare code-owned default, proving the
    /// theme's own "styles.&lt;role&gt;.normal" JSON was actually read and applied rather than
    /// silently ignored (the exact class of bug an earlier root-cause fix addressed, and that this
    /// follow-up investigation continues to guard). Unlike <see cref="EveryCuratedThemeExceptTheDefaults_AuthorsAllEightStyleSections"/>,
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

    /// <summary>Verifies every curated theme other than the two zero-config defaults authors all
    /// eight registrable style sections: every dependent control style resolves to a
    /// non-code-owned value, proving the section round-trips through the real mechanism rather
    /// than merely existing unread in the JSON. "default-dark"/"default-light" back
    /// <see cref="ThemeCatalog.Dark"/>/<see cref="ThemeCatalog.White"/>, the ambient zero-config theme every
    /// unthemed control and a large share of the test suite resolves against; authoring these
    /// eight sections there changes framework-wide default presentation rather than opting one
    /// curated theme in, so they are deliberately left unauthored.</summary>
    [Fact]
    public void EveryCuratedThemeExceptTheDefaults_AuthorsAllEightStyleSections()
    {
        foreach (var slug in ThemeCatalog.Slugs.Where(static slug => slug is not ("default-dark" or "default-light")))
        {
            var theme = ThemeCatalog.Load(slug);

            ScrollBarStyle.Definition.Resolve(null, theme).Glyphs
                .ShouldNotBe(ScrollBarGlyphs.Default, $"{slug} scrollBar.glyphs did not round-trip");
            CheckBoxStyle.Definition.Resolve(null, theme).Glyphs
                .ShouldNotBe(CheckBoxStyle.Default.Glyphs, $"{slug} checkBox.glyphs did not round-trip");
            RadioButtonStyle.Definition.Resolve(null, theme).Glyphs
                .ShouldNotBe(RadioButtonStyle.Default.Glyphs, $"{slug} radioButton.glyphs did not round-trip");
            _ = ButtonStyle.Definition.Resolve(null, theme);
            ChaseIndicatorStyle.Definition.Resolve(null, theme).Active
                .ShouldNotBe(ChaseIndicatorStyle.Default.Active, $"{slug} chaseIndicator.active did not round-trip");
            _ = SliderStyle.Definition.Resolve(null, theme);
            ProgressBarStyle.Definition.Resolve(null, theme).Glyphs
                .ShouldNotBe(ProgressBarGlyphs.Default, $"{slug} progressBar.glyphs did not round-trip");
            SpinnerStyle.Definition.Resolve(null, theme).Frames
                .ShouldNotBe(SpinnerStyle.Default.Frames, $"{slug} spinner.frames did not round-trip");
        }
    }

    /// <summary>Verifies the two zero-config default themes stay on code-owned defaults for all
    /// eight registrable style sections, so authoring the curated set never silently changes
    /// unthemed presentation.</summary>
    [Theory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    public void DefaultTheme_StaysOnCodeOwnedDefaultsForAllEightStyleSections(string slug)
    {
        var theme = ThemeCatalog.Load(slug);

        ScrollBarStyle.Definition.Resolve(null, theme).Glyphs.ShouldBe(ScrollBarGlyphs.Default);
        // Compared against the STYLE's default preset (as every other line here does), not against
        // CheckBoxGlyphs/RadioButtonGlyphs.Default - those are the one-cell Square/Circle glyph
        // families, not the glyphs the default bracket/parenthesis presentation resolves. Confusing
        // the two identically-named Defaults is what regressed the resolved defaults to "[☐]"/"(○)".
        CheckBoxStyle.Definition.Resolve(null, theme).Glyphs.ShouldBe(CheckBoxStyle.Default.Glyphs);
        RadioButtonStyle.Definition.Resolve(null, theme).Glyphs.ShouldBe(RadioButtonStyle.Default.Glyphs);
        ChaseIndicatorStyle.Definition.Resolve(null, theme).Active.ShouldBe(ChaseIndicatorStyle.Default.Active);
        ProgressBarStyle.Definition.Resolve(null, theme).Glyphs.ShouldBe(ProgressBarGlyphs.Default);
        SpinnerStyle.Definition.Resolve(null, theme).Frames.ShouldBe(SpinnerStyle.Default.Frames);
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

    /// <summary>Verifies passive surfaces ignore hover while interactive roles retain filled feedback.</summary>
    [Fact]
    public void EveryTheme_WhenPointerIsOver_PreservesPassiveSurfaces()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var controlNormal = theme.Control.Resolve(VisualState.Normal);
            var controlHovered = theme.Control.Resolve(VisualState.PointerOver);
            var containerNormal = theme.Container.Resolve(VisualState.Normal);
            var containerHovered = theme.Container.Resolve(VisualState.PointerOver);
            var windowNormal = theme.Window.Resolve(VisualState.Normal);
            var windowHovered = theme.Window.Resolve(VisualState.PointerOver);
            var buttonHovered = theme.Input.Resolve(VisualState.PointerOver);
            var inputHovered = theme.Input.Resolve(VisualState.PointerOver);

            controlHovered.Face.Background.ShouldBe(
                controlNormal.Face.Background,
                $"{slug} passive controls must not fill on hover");
            containerHovered.ShouldBe(containerNormal, $"{slug} containers must ignore hover");
            windowHovered.ShouldBe(windowNormal, $"{slug} windows must ignore hover");
            buttonHovered.Face.Background.SemanticColor.ShouldBe(
                SemanticColor.ActiveControl,
                $"{slug} buttons should retain filled hover feedback");
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
                active.Border.Foreground.Semantic.ShouldBeTrue(slug);
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

            background.Semantic.ShouldBeTrue($"{slug} input background must remain semantic");
            background.SemanticColor.ShouldBe(SemanticColor.Surface, $"{slug} input background must use Surface");
            theme.Resolve(background).ShouldBe(ThemeColorHelper.Surface(theme));
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

            windowFace.Background.Semantic.ShouldBeTrue($"{slug} window background must remain semantic");
            windowFace.Background.SemanticColor.ShouldBe(
                SemanticColor.WindowSurface,
                $"{slug} window background must use WindowSurface");
            windowFace.Foreground.Semantic.ShouldBeTrue($"{slug} window foreground must remain semantic");
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

            tooltipFace.Background.Semantic.ShouldBeTrue($"{slug} tooltip background must remain semantic");
            tooltipFace.Background.SemanticColor.ShouldBe(SemanticColor.Window, $"{slug} tooltip background must use Window");
            tooltipFace.Foreground.Semantic.ShouldBeTrue($"{slug} tooltip foreground must remain semantic");
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

    /// <summary>Verifies interaction colors preserve the face while focus remains stronger than hover.</summary>
    [Fact]
    public void EveryTheme_UsesRestrainedFocusedAndHoveredControlColors()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var focusedForeground = ThemeColorHelper.FocusedForeground(theme);
            var focusedBorder = ThemeColorHelper.FocusedBorder(theme);
            var hoveredForeground = ThemeColorHelper.HoveredForeground(theme);
            var hoveredBorder = ThemeColorHelper.HoveredBorder(theme);
            var pressedForeground = ThemeColorHelper.PressedForeground(theme);
            var pressedBorder = ThemeColorHelper.PressedBorder(theme);

            if (focusedForeground != Color.Default && hoveredForeground != Color.Default)
            {
                focusedForeground.ShouldNotBe(
                    hoveredForeground,
                    $"{slug} should distinguish focused and hovered control foregrounds");
            }

            if (focusedBorder != Color.Default && hoveredBorder != Color.Default)
            {
                focusedBorder.ShouldNotBe(
                    hoveredBorder,
                    $"{slug} should distinguish focused and hovered control borders");
            }

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
    /// hovered, focused, and selected surfaces all projected to one cube entry, so clicking a
    /// list row or hovering an input produced literally identical cells. Verified live before
    /// this guard existed; most bundled themes collided on at least one state.</summary>
    [Fact]
    public void EveryTheme_KeepsFillFeedbackAtIndexed256Depth()
    {
        var flat = new List<string>();

        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var states = theme.GetStyleSet(InputStyle.Default).ToAppearanceStates();
            var normal = TerminalPalette.Project(Resolve(states, VisualState.Normal, theme), ColorDepth.Indexed256);

            foreach (var state in new[] { VisualState.PointerOver, VisualState.Focused, VisualState.Pressed })
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

    /// <summary>Verifies editor themes resolve their accent to an absolute RGB color.</summary>
    [Fact]
    public void EditorThemes_UseRgbAccents()
    {
        var accent = ThemeColorHelper.Accent(ThemeCatalog.Load("dracula"));
        accent.IsRgb.ShouldBeTrue();
    }
    /// <summary>Verifies every bundled theme gives distinguishable fill feedback for hover, focus,
    /// and press on an input.
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

            foreach (var state in new[] { VisualState.PointerOver, VisualState.Focused, VisualState.Pressed })
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

    /// <summary>Verifies every bundled theme resolves every primary style definition eagerly -
    /// the resolver and the full appearance-states materialization both run against every theme.
    ///
    /// <para>Style-section parsing is deliberately lazy: a section's leaf values convert only the
    /// first time something resolves that style type. The earlier eight-section round-trip test
    /// predates most of the registered set, so a malformed value in a bundled theme's newer
    /// section (or a conversion regression on a member no theme authors yet) would ship silently
    /// and surface as a render-time failure in whichever application first touched it. This walk
    /// discovers the definitions reflectively - the same way the section registry itself does -
    /// so a style type added tomorrow is covered without editing this test.</para>
    /// </summary>
    [Fact]
    public void EveryTheme_ResolvesEveryPrimaryStyleDefinitionEagerly()
    {
        var definitions = new List<(Type Style, object Definition)>();

        foreach (var type in typeof(ControlStyle).Assembly.GetTypes())
        {
            if (type.IsAbstract || !type.IsAssignableTo(typeof(ControlStyle)))
            {
                continue;
            }

            if (type.GetProperty("Definition", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.GetValue(null) is IStyleDefinition { IsControl: true } definition)
            {
                definitions.Add((type, definition));
            }
        }

        // Sanity: the reflective walk must actually find the primary set, or the loop below
        // vacuously passes. Well above the original eight to prove the newer types are included.
        definitions.Count.ShouldBeGreaterThanOrEqualTo(15);

        var failures = new List<string>();

        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);

            foreach (var (style, definition) in definitions)
            {
                var definitionType = definition.GetType();
                var resolveProperty = definitionType.GetProperty("Resolve", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var appearanceProperty = definitionType.GetProperty("Appearance", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var resolve = (Delegate) resolveProperty.GetValue(definition)!;
                var appearance = (Delegate) appearanceProperty.GetValue(definition)!;

                try
                {
                    var resolved = resolve.DynamicInvoke(null, theme);
                    _ = appearance.DynamicInvoke(resolved, theme);
                }
                catch (TargetInvocationException error)
                {
                    failures.Add($"{slug}/{style.Name}: {error.InnerException?.Message ?? error.Message}");
                }
            }
        }

        failures.ShouldBeEmpty();
    }
}
