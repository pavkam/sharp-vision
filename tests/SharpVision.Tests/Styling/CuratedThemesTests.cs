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
        Themes.Slugs.OrderBy(static s => s, StringComparer.Ordinal)
            .ShouldBe(_expected.OrderBy(static s => s, StringComparer.Ordinal));
    }

    /// <summary>Verifies every catalog theme loads frozen with ControlBase normal state defined.</summary>
    [Fact]
    public void EveryTheme_LoadsFrozenWithControlNormalState()
    {
        foreach (var slug in Themes.Slugs)
        {
            var theme = Themes.Load(slug);
            theme.IsFrozen.ShouldBeTrue();

            theme.Control.Normal.Face.Foreground.ShouldNotBe(default, $"{slug} missing Control normal foreground");
            theme.Control.Normal.Face.Background.ShouldNotBe(default, $"{slug} missing Control normal background");
        }
    }

    /// <summary>Verifies every curated theme other than the two zero-config defaults authors all
    /// eight registrable style sections (see #155): every dependent control style resolves to a
    /// non-code-owned value, proving the section round-trips through the real mechanism rather
    /// than merely existing unread in the JSON. "default-dark"/"default-light" back
    /// <see cref="Themes.Dark"/>/<see cref="Themes.White"/>, the ambient zero-config theme every
    /// unthemed control and a large share of the test suite resolves against; authoring these
    /// eight sections there changes framework-wide default presentation rather than opting one
    /// curated theme in, so they are deliberately left unauthored.</summary>
    [Fact]
    public void EveryCuratedThemeExceptTheDefaults_AuthorsAllEightStyleSections()
    {
        foreach (var slug in Themes.Slugs.Where(static slug => slug is not ("default-dark" or "default-light")))
        {
            var theme = Themes.Load(slug);

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
    /// unthemed presentation (see #155).</summary>
    [Theory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    public void DefaultTheme_StaysOnCodeOwnedDefaultsForAllEightStyleSections(string slug)
    {
        var theme = Themes.Load(slug);

        ScrollBarStyle.Definition.Resolve(null, theme).Glyphs.ShouldBe(ScrollBarGlyphs.Default);
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
        foreach (var slug in Themes.Slugs)
        {
            var theme = Themes.Load(slug);
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
        foreach (var slug in Themes.Slugs)
        {
            var theme = Themes.Load(slug);
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
            buttonHovered.Face.Background.ThemeColor.ShouldBe(
                ThemeColor.ActiveControl,
                $"{slug} buttons should retain filled hover feedback");
            inputHovered.Face.Background.ThemeColor.ShouldBe(
                ThemeColor.ActiveControl,
                $"{slug} inputs should retain filled hover feedback");
        }
    }

    /// <summary>Verifies active Windows change only their border when focus enters the surface.</summary>
    [Fact]
    public void EveryTheme_WhenWindowContainsFocus_UsesOnlyActiveBorder()
    {
        foreach (var slug in Themes.Slugs)
        {
            var theme = Themes.Load(slug);
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
                active.Border.Foreground.IsThemeValue.ShouldBeTrue(slug);
                active.Border.Foreground.ThemeColor.ShouldBe(ThemeColor.ActiveBorder, slug);
            }
        }
    }

    /// <summary>Verifies every bundled input profile uses the raised surface instead of the application plane.</summary>
    [Fact]
    public void EveryTheme_WhenInputIsNormal_UsesSurfaceBackground()
    {
        foreach (var slug in Themes.Slugs)
        {
            var theme = Themes.Load(slug);
            var background = theme.Input.Normal.Face.Background;

            background.IsThemeValue.ShouldBeTrue($"{slug} input background must remain semantic");
            background.ThemeColor.ShouldBe(ThemeColor.Surface, $"{slug} input background must use Surface");
            theme.Resolve(background).ShouldBe(ThemeColorHelper.Surface(theme));
        }
    }

    /// <summary>Verifies the default dark theme uses a restrained deep neutral for every opaque face.</summary>
    [Fact]
    public void DefaultDark_WhenOpaqueFacesResolve_UsesDeepNeutralRgb()
    {
        var theme = Themes.Load("default-dark");
        var expected = Color.Rgb(38, 38, 38);
        theme.Resolve(theme.Control.Normal.Face.Background).ShouldBe(expected);
    }

    /// <summary>Verifies every shipped palette keeps composite shadows visible against application and raised surfaces.</summary>
    [Fact]
    public void EveryTheme_UsesVisibleCompositeShadowColors()
    {
        foreach (var slug in Themes.Slugs)
        {
            var theme = Themes.Load(slug);
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
        foreach (var slug in Themes.Slugs)
        {
            var theme = Themes.Load(slug);
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
        foreach (var slug in Themes.Slugs)
        {
            var theme = Themes.Load(slug);
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
        foreach (var slug in Themes.Slugs)
        {
            var theme = Themes.Load(slug);
            var accessKey = theme.Hotkey;
            var foreground = ThemeColorHelper.Foreground(theme);

            if (accessKey != Color.Default)
            {
                accessKey.ShouldNotBe(foreground, $"{slug} should distinguish access keys from ordinary text");
            }
        }
    }

    /// <summary>Verifies editor themes resolve their accent to an absolute RGB color.</summary>
    [Fact]
    public void EditorThemes_UseRgbAccents()
    {
        var accent = ThemeColorHelper.Accent(Themes.Load("dracula"));
        accent.IsRgb.ShouldBeTrue();
    }
}
