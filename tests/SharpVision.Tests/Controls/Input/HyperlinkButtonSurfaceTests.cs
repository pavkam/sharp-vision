// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies HyperlinkButton appearance, interaction, and activation on a mounted surface.</summary>
public sealed class HyperlinkButtonSurfaceTests
{
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
        surface.ShouldHaveState(link, VisualState.PointerOver);
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
        link.Focused.ShouldBeTrue();
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
        ComponentBehavior.UnavailableCleanup)]
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
        surface.ShouldHaveState(link, VisualState.PointerOver | VisualState.Focused | VisualState.Pressed);
        // Held-with-pointer composes PointerOver, whose foreground cue survives the press.
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(ThemeColorHelper.HoveredForeground(ThemeCatalog.Dark), ColorDepth.Basic16));

        // Act — disable while held
        await surface.UpdateAsync(() => link.Enabled = false, "disable held HyperlinkButton");

        // Assert — unavailable cleanup
        link.Pressed.ShouldBeFalse();
        link.Focused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveState(link, VisualState.Disabled);
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
        surface.ShouldHaveState(link, VisualState.PointerOver | VisualState.Focused);
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
            Enabled = false,
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

    /// <summary>Verifies a disabled HyperlinkButton does not activate on click.</summary>
    [Fact]
    public async Task Pointer_WhenDisabled_DoesNotActivateAsync()
    {
        // Arrange
        var clicks = 0;
        var link = new HyperlinkButton("Visit")
        {
            Enabled = false,
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

    /// <summary>Verifies focused, pressed, and disabled resolve the theme's own state background,
    /// proving the presentation is no longer identical across every visual state.</summary>
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

        // Assert — normal
        link.ActualFace.Background.ShouldBe(theme.ResolveColor(SemanticColor.Control));

        // Act — focus
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert — focused resolves the theme's focused background, not the normal one
        link.ActualFace.Background.ShouldBe(theme.ResolveColor(SemanticColor.FocusedControl));
        link.ActualFace.Background.ShouldNotBe(theme.ResolveColor(SemanticColor.Control));

        // Act — press while focused
        await surface.Pointer.MoveToAsync(link);
        await surface.Pointer.PressAsync();

        // Assert — pressed resolves the theme's pressed background
        link.ActualFace.Background.ShouldBe(theme.ResolveColor(SemanticColor.PressedControl));

        // Act — release, then disable
        await surface.Pointer.ReleaseAsync();
        await surface.UpdateAsync(() => link.Enabled = false, "disable HyperlinkButton");

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
        await surface.UpdateAsync(() => link.Enabled = false, "disable HyperlinkButton");

        // Assert — and over Disabled too
        link.ActualFace.ShouldBe(pinned);
    }
}
