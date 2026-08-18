// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Tests.Styling;

/// <summary>Verifies HyperlinkButton appearance, interaction, and activation on a mounted surface.</summary>
public sealed class HyperlinkButtonSurfaceTests
{
    /// <summary>Verifies a theme's own "hyperlinkButton" section overrides the coded accent
    /// foreground and underline color instead of the fixed SemanticColor.Accent default.</summary>
    [Fact]
    public async Task Render_WhenThemeOverridesLinkColor_UsesThemeColorInsteadOfAccentAsync()
    {
        // Arrange
        var color = Color.Rgb(0x32, 0xC8, 0x78);
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            palette: "\"linkColor\":\"#32c878\"",
            extraStyles: """, "hyperlinkButton": { "normal": { "face": { "foreground": "linkColor", "underlineColor": "linkColor" } } } """));
        var link = new HyperlinkButton("Go")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(4, 1),
            theme,
            TestContext.Current.CancellationToken);

        // Assert
        var expected = TerminalPalette.Project(color, ColorDepth.Basic16);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(expected);
        (surface.Cell(new Point(0, 0)).Style.Attributes & TerminalAttributes.Underline)
            .ShouldBe(TerminalAttributes.Underline);
        link.GetActualFace(VisualState.Normal).UnderlineColor.ShouldBe(color);
    }

    /// <summary>Verifies a theme's own per-state "hyperlinkButton.focused" section still reaches
    /// render output despite HyperlinkButtonStyle.Complete forcing Underline/UnderlineColor
    /// unconditionally across every state to keep them from reverting outside Normal - an explicit
    /// JSON override on the control's own key is applied after that fold, not suppressed by it.</summary>
    [Fact]
    public async Task Render_WhenThemeOverridesFocusedUnderlineColor_UsesThemeColorAsync()
    {
        // Arrange
        var color = Color.Rgb(0xFF, 0x64, 0x00);
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            palette: "\"focusedLinkColor\":\"#ff6400\"",
            extraStyles: """, "hyperlinkButton": { "focused": { "face": { "underlineColor": "focusedLinkColor" } } } """));
        var link = new HyperlinkButton("Go")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(4, 1),
            theme,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveState(link, VisualState.Focused);
        (surface.Cell(new Point(0, 0)).Style.Attributes & TerminalAttributes.Underline)
            .ShouldBe(TerminalAttributes.Underline);
        link.GetActualFace(VisualState.Focused).UnderlineColor.ShouldBe(color);
    }

    /// <summary>Verifies the mounted HyperlinkButton renders accent-colored underlined text and responds to hover.</summary>
    [ComponentBehaviorEvidence(typeof(HyperlinkButton), ComponentBehavior.Mounted | ComponentBehavior.Hover)]
    [Fact]
    public async Task Render_WhenMounted_ShowsAccentUnderlinedTextAsync()
    {
        // Arrange
        var link = new HyperlinkButton("Visit")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Assert — text renders with accent foreground and underline
        surface.ShouldRender("""
                             Visit


                             """);
        // The link's normal-state overlay authors the accent foreground and a straight accent
        // underline, and the transparent caption inherits that ambient face.
        var normalForeground = TerminalPalette.Project(ThemeColorHelper.Accent(ThemeCatalog.Dark), ColorDepth.Basic16);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(normalForeground);
        (surface.Cell(new Point(0, 0)).Style.Attributes & TerminalAttributes.Underline)
            .ShouldBe(TerminalAttributes.Underline);
        surface.Cell(new Point(0, 0)).Style.Background.ShouldBe(ReferenceColors.Get(0));

        // Act — hover
        await surface.Pointer.MoveToAsync(link);

        // Assert — hovered state
        surface.ShouldHaveState(link, VisualState.IsPointerOver);
        var hoveredForeground = TerminalPalette.Project(ThemeColorHelper.HoveredForeground(ThemeCatalog.Dark), ColorDepth.Basic16);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(hoveredForeground);
    }

    /// <summary>Verifies Tab focuses the HyperlinkButton and updates its appearance.</summary>
    [ComponentBehaviorEvidence(
        typeof(HyperlinkButton),
        ComponentBehavior.Tab |
        ComponentBehavior.DirectionalExcluded)]
    [Fact]
    public async Task Keyboard_WhenTabIsPressed_ShowsFocusedAppearanceAsync()
    {
        // Arrange
        var link = new HyperlinkButton("Visit")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var clicks = 0;
        link.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert
        surface.ShouldHaveState(link, VisualState.Focused);
        link.IsFocused.ShouldBeTrue();
        clicks.ShouldBe(0);

        // The transparent caption inherits the link's ambient focused face foreground.
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(ThemeColorHelper.FocusedForeground(ThemeCatalog.Dark), ColorDepth.Basic16));
    }

    /// <summary>Verifies pointer press shows pressed state and disable cleans up.</summary>
    [ComponentBehaviorEvidence(
        typeof(HyperlinkButton),
        ComponentBehavior.Focus |
        ComponentBehavior.PressRelease |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Disabled)]
    [Fact]
    public async Task Pointer_WhenPrimaryButtonIsHeld_ShowsPressedAppearanceAsync()
    {
        // Arrange
        var link = new HyperlinkButton("Visit")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(10, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(link);

        // Act
        await surface.Pointer.PressAsync();

        // Assert
        surface.ShouldHaveState(link, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);
        // Pointer press focuses the link, so its access-key cell keeps the focused hotkey cue
        // while the surrounding link face composes pointer, focus, and press states.
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(ThemeCatalog.Dark.Hotkey, ColorDepth.Basic16));

        // Act — disable while held
        await surface.UpdateAsync(() => link.IsEnabled = false, "disable held HyperlinkButton");

        // Assert — unavailable cleanup and direct disable
        link.IsPressed.ShouldBeFalse();
        link.IsFocused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveState(link, VisualState.Disabled);

        // Act re-enable before proving ancestor inheritance in isolation
        await surface.UpdateAsync(() => link.IsEnabled = true, "re-enable HyperlinkButton directly");
        surface.ShouldHaveState(link, VisualState.Normal);

        // Act and assert a never-directly-disabled link inherits Disabled from a disabled
        // ancestor by mounting an equivalent link under an explicit ancestor container.
        var ancestorLink = new HyperlinkButton("Visit")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var ancestorHost = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { ancestorLink }
        };
        await using var ancestorSurface = await ComponentSurface.MountAsync(
            ancestorHost,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        await ancestorSurface.UpdateAsync(() => ancestorHost.IsEnabled = false, "disable ancestor Stack");

        ancestorLink.IsEnabled.ShouldBeTrue();
        ancestorLink.EffectiveIsEnabled.ShouldBeFalse();
        ancestorSurface.ShouldHaveState(ancestorLink, VisualState.Disabled);

        // Act a genuine resize while disabled and assert geometry stability against an
        // independently mounted, otherwise-identical enabled link at the same new size.
        await surface.UpdateAsync(() => link.IsEnabled = false, "disable HyperlinkButton for geometry check");
        await surface.ResizeAsync(new Size(14, 4));
        var disabledBounds = link.Bounds;
        var disabledDesiredSize = link.DesiredSize;

        var referenceLink = new HyperlinkButton("Visit")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            referenceLink,
            new Size(14, 4),
            TestContext.Current.CancellationToken);

        referenceLink.Bounds.ShouldBe(disabledBounds);
        referenceLink.DesiredSize.ShouldBe(disabledDesiredSize);

        // Act re-enable recovery
        await surface.UpdateAsync(() => link.IsEnabled = true, "re-enable HyperlinkButton");

        // Assert Normal state and resumed interaction
        surface.ShouldHaveState(link, VisualState.Normal);
        await surface.Pointer.MoveToAsync(link);
        link.IsPointerOver.ShouldBeTrue();
    }

    /// <summary>Verifies the click shorthand emits move, press, and release and settles activation.</summary>
    [ComponentBehaviorEvidence(
        typeof(HyperlinkButton),
        ComponentBehavior.Activation | ComponentBehavior.PointerActivation)]
    [Fact]
    public async Task Pointer_WhenLinkIsClicked_ReleasesAndActivatesOnceAsync()
    {
        // Arrange
        var clicks = 0;
        var link = new HyperlinkButton("Visit")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        link.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(link);

        // Assert
        clicks.ShouldBe(1);
        surface.ShouldHaveState(link, VisualState.IsPointerOver | VisualState.Focused);
    }

    /// <summary>Verifies terminal Enter and Space input activate the focused HyperlinkButton.</summary>
    [ComponentBehaviorEvidence(typeof(HyperlinkButton), ComponentBehavior.KeyboardActivation)]
    [Fact]
    public async Task Keyboard_WhenEnterAndSpaceAreCompleted_ActivatesFocusedLinkAsync()
    {
        // Arrange
        var link = new HyperlinkButton("Visit");
        var activations = 0;
        link.Click += (_, _) => activations++;
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(10, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        // Assert
        activations.ShouldBe(2);
        surface.ShouldHaveFocus(link);
    }

    /// <summary>Verifies a disabled HyperlinkButton shows muted appearance.</summary>
    [Fact]
    public async Task Render_WhenDisabled_ShowsMutedAppearanceAsync()
    {
        // Arrange
        var link = new HyperlinkButton("Visit")
        {
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldHaveState(link, VisualState.Disabled);
        var disabledForeground = TerminalPalette.Project(ThemeColorHelper.DisabledForeground(ThemeCatalog.Dark), ColorDepth.Basic16);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(disabledForeground);
    }

    /// <summary>Verifies a disabled HyperlinkButton does not activate on click - the command/
    /// access-key non-invocation leg of its disabled contract.</summary>
    [ComponentBehaviorEvidence(typeof(HyperlinkButton), ComponentBehavior.Disabled)]
    [Fact]
    public async Task Pointer_WhenDisabled_DoesNotActivateAsync()
    {
        // Arrange
        var clicks = 0;
        var link = new HyperlinkButton("Visit")
        {
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        link.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(link);
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        clicks.ShouldBe(0);
    }

    /// <summary>Verifies focus preserves the normal face with a decoration cue while pressed and
    /// disabled continue resolving their theme-owned appearances.</summary>
    [Fact]
    public async Task ActualFace_AcrossVisualStates_ResolvesDistinctThemeBackgroundsAsync()
    {
        // Arrange
        var link = new HyperlinkButton("Visit");
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(10, 3),
            TestContext.Current.CancellationToken);
        var theme = link.Theme.ShouldNotBeNull();
        var normalBackground = link.ActualFace.Background;

        // Assert — normal
        link.ActualFace.Background.ShouldBe(theme.ResolveColor(SemanticColor.Control));

        // Act — focus
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert — focus preserves the face and contributes the non-color decoration cue.
        link.ActualFace.Background.ShouldBe(theme.ResolveColor(SemanticColor.FocusedControl));
        link.ActualFace.Background.ShouldBe(normalBackground);
        link.ActualFace.Attributes.IsLiteral.ShouldBeTrue();
        (link.ActualFace.Attributes.Literal & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.Bold);

        // Act — press while focused
        await surface.Pointer.MoveToAsync(link);
        await surface.Pointer.PressAsync();

        // Assert — pressed resolves the theme's pressed background
        link.ActualFace.Background.ShouldBe(theme.ResolveColor(SemanticColor.PressedControl));

        // Act — release, then disable
        await surface.Pointer.ReleaseAsync();
        await surface.UpdateAsync(() => link.IsEnabled = false, "disable HyperlinkButton");

        // Assert — disabled no longer keeps the active accent-underline presentation
        link.ActualFace.Foreground.ShouldBe(theme.ResolveColor(SemanticColor.DisabledText));
        link.ActualFace.Foreground.ShouldNotBe(theme.ResolveColor(SemanticColor.Accent));
    }

    /// <summary>Verifies a caller-assigned complete Face still wins over every visual state and theme.</summary>
    [Fact]
    public async Task ActualFace_WhenCallerAssignsFace_WinsAcrossEveryStateAsync()
    {
        // Arrange
        var pinned = new Face(Color.Rgb(1, 2, 3), Color.Rgb(4, 5, 6), TerminalAttributes.None, Underline.None, Color.Default);
        var link = new HyperlinkButton("Visit") { Face = pinned };
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Assert — normal
        link.ActualFace.ShouldBe(pinned);

        // Act — focus, hover, and press
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Pointer.MoveToAsync(link);
        await surface.Pointer.PressAsync();

        // Assert — the pinned Face still wins over every state overlay
        link.ActualFace.ShouldBe(pinned);

        // Act — disable
        await surface.Pointer.ReleaseAsync();
        await surface.UpdateAsync(() => link.IsEnabled = false, "disable HyperlinkButton");

        // Assert — and over Disabled too
        link.ActualFace.ShouldBe(pinned);
    }

    /// <summary>Verifies both affixes reserve their own cell column pinned flush to the content
    /// box edges, with the caption confined to the remaining middle box.</summary>
    [Fact]
    public async Task Render_WhenLinkHasBothAffixes_PinsThemToTheContentBoxEdgesAsync()
    {
        // Arrange
        var link = new HyperlinkButton
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(6),
            Height = Length.Cells(1),
            Text = "Go",
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(6, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe(">");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("G");
        surface.Cell(new Point(3, 0)).Text.ShouldBe("o");
        surface.Cell(new Point(5, 0)).Text.ShouldBe("<");
    }

    /// <summary>Verifies the start affix survives and the end affix drops whole - never a partial
    /// cluster - when the content box has room for only one, matching the documented priority:
    /// caption shrinks first, then the end affix, then the start affix.</summary>
    [Fact]
    public async Task Render_WhenContentBoxHasRoomForOnlyOneAffix_DropsTheEndAffixFirstAsync()
    {
        // Arrange
        var link = new HyperlinkButton
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(1),
            Height = Length.Cells(1),
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(1, 1),
            TestContext.Current.CancellationToken);

        // Assert — the content box has exactly one drawable cell, and the start affix claims it.
        surface.Cell(new Point(0, 0)).Text.ShouldBe(">");
    }

    /// <summary>Verifies a same-width content or color swap invalidates rendering only, and the
    /// mounted surface reflects the new glyph without a remeasure.</summary>
    [Fact]
    public async Task StartAffix_WhenContentChangesAtTheSameResolvedWidth_UpdatesRenderOnlyAsync()
    {
        // Arrange
        var link = new HyperlinkButton
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(6),
            Height = Length.Cells(1),
            Text = "Go",
            StartAffix = new Affix("|")
        };
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(6, 1),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("|");
        var impact = Invalidation.None;

        // Act
        await surface.UpdateAsync(
            () =>
            {
                link.Clear(Invalidation.All);
                link.StartAffix = new Affix("/");
                impact = link.Pending;
            },
            "swap start affix content at the same resolved width");

        // Assert
        impact.ShouldBe(Invalidation.Render);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("/");
    }
}
