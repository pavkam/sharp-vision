// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies Expander appearance, activation, focus, content exclusion, replacement, and resize through mounted surfaces.</summary>
public sealed class ExpanderSurfaceTests
{
    /// <summary>Verifies the default disclosure header and indentation need no surrounding frame.</summary>
    [Fact]
    public async Task Render_WhenDefaultChromeIsUsed_DrawsBorderlessTransparentSectionAsync()
    {
        // Arrange
        var expander = new Expander
        {
            HeaderText = "Details",
            Content = new ControlText("Body"),
            Width = Length.Cells(12),
            Height = Length.Cells(4)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(12, 4),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ▼ Details
                               Body


                             """);
        expander.Content.Bounds.ShouldBe(new Rect(2, 1, 10, 3));
        expander.GetResolvedAppearance(VisualState.Normal).BackgroundMode.ShouldBe(BackgroundMode.Opaque);
        surface.Cell(default).Style.Background.ShouldBe(ReferenceColors.Get(0));
    }

    /// <summary>Verifies a non-text header renders through the ordinary owned-control pipeline, proving the
    /// header ownership role hosts arbitrary rich content rather than only a text caption (see #70).</summary>
    [Fact]
    public async Task Render_WhenHeaderIsARichControl_RendersThroughTheOwnedControlPipelineAsync()
    {
        // Arrange
        var expander = new Expander
        {
            Header = new ProbeControl(new Size(1, 1)) { Content = "R".AsMemory() },
            Content = new ControlText("Body"),
            Width = Length.Cells(12),
            Height = Length.Cells(4)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(12, 4),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ▼ R
                               Body


                             """);
        expander.Header.ShouldBeOfType<ProbeControl>().Bounds.ShouldBe(new Rect(2, 0, 1, 1));
    }

    /// <summary>Verifies expanded and collapsed states draw exact header/content cells and remove stale rows.</summary>
    [Fact]
    public async Task UpdateAsync_WhenExpansionChanges_DrawsExactStateAndClearsContentAsync()
    {
        // Arrange
        var expander = new Expander
        {
            HeaderText = "Details",
            Content = new ControlText("Body\nMore"),
            Width = Length.Cells(12),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                             ▼ Details
                               Body
                               More
                             """);

        // Act
        await surface.UpdateAsync(() => expander.IsExpanded = false, "collapse Expander");

        // Assert
        expander.Content.Bounds.ShouldBe(default);
        expander.Content.Parent.ShouldBeSameAs(expander);
        surface.ShouldRender("▶ Details");
    }

    /// <summary>Verifies pointer, Space, and Enter activation share focus and changed-event behavior.</summary>
    [ComponentBehaviorEvidence(
        typeof(Expander),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressRelease |
        ComponentBehavior.Activation |
        ComponentBehavior.PointerActivation |
        ComponentBehavior.KeyboardActivation)]
    [Fact]
    public async Task Input_WhenHeaderIsActivated_TogglesThroughEverySupportedPathAsync()
    {
        // Arrange
        var changes = 0;
        var expander = new Expander
        {
            HeaderText = "Input",
            Content = new ControlText("Content"),
            Width = Length.Cells(12),
            Height = Length.Cells(2)
        };
        expander.ExpandedChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(12, 2),
            TestContext.Current.CancellationToken);

        // Act and assert keyboard focus without directional activation
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);
        expander.IsExpanded.ShouldBeTrue();

        // Act pointer hover and held press
        await surface.Pointer.MoveToAsync(expander, new Point(1, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveState(expander, VisualState.PointerOver | VisualState.Focused | VisualState.Pressed);
        surface.ShouldHaveCapture(expander);

        // Act release
        await surface.Pointer.ReleaseAsync();

        // Assert pointer
        expander.IsExpanded.ShouldBeFalse();
        changes.ShouldBe(1);
        surface.ShouldHaveState(expander, VisualState.PointerOver | VisualState.Focused);

        // Act Space then Enter
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        expander.IsExpanded.ShouldBeTrue();
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert keyboard
        expander.IsExpanded.ShouldBeFalse();
        changes.ShouldBe(3);
        surface.ShouldRender("▶ Input");
    }

    /// <summary>Verifies the visible borderless header remains the pointer activation target.</summary>
    [Fact]
    public async Task Pointer_WhenBorderlessHeaderIsClicked_TogglesExpansionAsync()
    {
        // Arrange
        var expander = new Expander
        {
            HeaderText = "Details",
            Content = new ControlText("Body"),
            Width = Length.Cells(12),
            Height = Length.Cells(4)
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(12, 4),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(expander, new Point(1, 0));

        // Assert
        expander.IsExpanded.ShouldBeFalse();
        expander.Content.Bounds.ShouldBe(default);
    }

    /// <summary>Verifies descendant hover remains observable without applying header hover appearance.</summary>
    [Fact]
    public async Task Pointer_WhenMovingBetweenBodyAndHeader_AppliesHoverOnlyToHeaderAsync()
    {
        // Arrange
        var body = new ControlText("Body");
        var expander = new Expander
        {
            HeaderText = "Details",
            Content = body,
            Width = Length.Cells(12),
            Height = Length.Cells(4)
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(12, 4),
            TestContext.Current.CancellationToken);
        var theme = expander.Theme.ShouldNotBeNull();
        var normal = ThemeColorHelper.InactiveForeground(theme);
        var hovered = ThemeColorHelper.HoveredForeground(theme);
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(normal);

        // Act over the retained body child
        await surface.Pointer.MoveToAsync(expander, new Point(3, 1));

        // Assert ancestry state without header appearance
        expander.IsPointerOver.ShouldBeTrue();
        expander.IsPointerDirectlyOver.ShouldBeFalse();
        body.IsPointerDirectlyOver.ShouldBeTrue();
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(normal);

        // Act over the directly rendered header
        await surface.Pointer.MoveToAsync(expander, new Point(1, 0));

        // Assert header appearance
        expander.IsPointerDirectlyOver.ShouldBeTrue();
        body.IsPointerOver.ShouldBeFalse();
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(hovered);
    }

    /// <summary>Verifies disabled input refuses toggles and collapsed replacement appears only after expansion.</summary>
    [ComponentBehaviorEvidence(
        typeof(Expander),
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Input_WhenDisabledOrContentReplaced_PreservesAvailabilityAndOwnershipPolicyAsync()
    {
        // Arrange disabled collapsed Expander
        var first = new ControlText("First");
        var second = new ControlText("Second");
        var expander = new Expander
        {
            HeaderText = "Policy",
            IsExpanded = false,
            IsEnabled = false,
            Content = first,
            Width = Length.Cells(12),
            Height = Length.Cells(2)
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(12, 2),
            TestContext.Current.CancellationToken);

        // Act disabled input
        await surface.Pointer.ClickAsync(expander, new Point(1, 0));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert disabled refusal
        expander.IsExpanded.ShouldBeFalse();
        surface.ShouldHaveState(expander, VisualState.Disabled);
        surface.ShouldRender("▶ Policy");

        // Act replace while collapsed, enable, and expand
        await surface.UpdateAsync(() => expander.Content = second, "replace collapsed Expander content");
        await surface.UpdateAsync(() => expander.IsEnabled = true, "enable Expander");
        await surface.Pointer.ClickAsync(expander, new Point(1, 0));

        // Assert replacement reveal
        first.Parent.ShouldBeNull();
        second.Parent.ShouldBeSameAs(expander);
        expander.IsExpanded.ShouldBeTrue();
        surface.ShouldRender("""
                             ▼ Policy
                               Second
                             """);

        // Act unavailable while held
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(expander);
        await surface.UpdateAsync(() => expander.IsEnabled = false, "disable held Expander");

        // Assert cleanup retains completed expansion and ownership
        expander.IsExpanded.ShouldBeTrue();
        expander.IsPressed.ShouldBeFalse();
        expander.IsFocused.ShouldBeFalse();
        second.Parent.ShouldBeSameAs(expander);
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
    }

    /// <summary>Verifies Unicode header geometry and content reflow remain correct after resize.</summary>
    [Fact]
    public async Task ResizeAsync_WhenUnicodeHeaderGrows_PreservesWideCellsAndReflowsContentAsync()
    {
        // Arrange
        var expander = new Expander
        {
            HeaderText = "界 Tools",
            Content = new ControlText("abcdefgh") { Overflow = Overflow.WrapAnywhere },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(6, 3),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(2, 0)).Text.ShouldBe("界");
        surface.Cell(new Point(3, 0)).IsContinuation.ShouldBeTrue();

        // Act
        await surface.ResizeAsync(new Size(12, 2));

        // Assert
        expander.Bounds.ShouldBe(new Rect(0, 0, 12, 2));
        expander.Content.Bounds.ShouldBe(new Rect(2, 1, 10, 1));
        surface.ShouldRender("""
                             ▼ 界 Tools
                               abcdefgh
                             """);
        surface.Cell(new Point(2, 0)).Text.ShouldBe("界");
        surface.Cell(new Point(3, 0)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies custom disclosure glyphs render in both expanded and collapsed states.</summary>
    [Fact]
    public async Task Render_WhenCustomGlyphsAreSet_UsesOverrideGlyphsAsync()
    {
        // Arrange
        var expander = new Expander
        {
            HeaderText = "Section",
            Content = new ControlText("Body"),
            ExpandedGlyph = new Rune('-'),
            CollapsedGlyph = new Rune('+'),
            Width = Length.Cells(12),
            Height = Length.Cells(2)
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(12, 2),
            TestContext.Current.CancellationToken);

        // Assert expanded with custom glyph
        surface.ShouldRender("""
                             - Section
                               Body
                             """);

        // Act collapse
        await surface.UpdateAsync(() => expander.IsExpanded = false, "collapse with custom glyph");

        // Assert collapsed with custom glyph
        surface.ShouldRender("+ Section");
    }

    /// <summary>Verifies ContentIndent controls the indentation of expanded content on a surface.</summary>
    [Fact]
    public async Task Render_WhenContentIndentChanges_ShiftsContentHorizontallyAsync()
    {
        // Arrange
        var expander = new Expander
        {
            HeaderText = "Details",
            Content = new ControlText("Body"),
            ContentIndent = 4,
            Width = Length.Cells(14),
            Height = Length.Cells(2)
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(14, 2),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ▼ Details
                                 Body
                             """);

        // Act change indent
        await surface.UpdateAsync(() => expander.ContentIndent = 0, "remove content indent");

        // Assert flush left
        surface.ShouldRender("""
                             ▼ Details
                             Body
                             """);
    }

    /// <summary>Verifies a one-cell collapsed header clips safely and resize reveals the retained label.</summary>
    [Fact]
    public async Task ResizeAsync_WhenCollapsedHeaderStartsTiny_RevealsLabelWithoutExpandingAsync()
    {
        // Arrange
        var expander = new Expander
        {
            HeaderText = "Details",
            IsExpanded = false,
            Content = new ControlText("Hidden"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(1, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("▶");

        // Act
        await surface.ResizeAsync(new Size(9, 1));

        // Assert
        expander.IsExpanded.ShouldBeFalse();
        expander.Content.Bounds.ShouldBe(default);
        surface.ShouldRender("▶ Details");
    }

    /// <summary>Verifies a theme swap confined to the directly-resolved FocusedControl role still
    /// repaints the focused header background, instead of the base role-profile comparison alone
    /// under-invalidating it (see #161).</summary>
    [Fact]
    public async Task Surface_WhenThemeSwapChangesOnlyFocusedControl_RepaintsFocusedHeaderAsync()
    {
        // Arrange
        var themeA = WithColor(ThemeColor.FocusedControl, Color.Rgb(10, 20, 30));
        var themeB = WithColor(ThemeColor.FocusedControl, Color.Rgb(200, 210, 220));
        var expander = new Expander
        {
            HeaderText = "Details",
            Content = new ControlText("Body"),
            Width = Length.Cells(12),
            Height = Length.Cells(4)
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(12, 4),
            themeA,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.Cell(default).Style.Background.ShouldBe(Palette.Project(Color.Rgb(10, 20, 30), ColorDepth.Basic16));

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = themeB, "swap FocusedControl-only theme");

        // Assert
        surface.Cell(default).Style.Background.ShouldBe(Palette.Project(Color.Rgb(200, 210, 220), ColorDepth.Basic16));
    }

    private static Theme WithColor(ThemeColor role, Color value)
    {
        var source = Themes.Dark;
        var theme = new Theme(
            source.Palette,
            source.Name,
            source.Slug,
            source.ColorScheme,
            source.Author,
            source.License,
            source.Source);

        foreach (var color in Enum.GetValues<ThemeColor>())
        {
            theme.SetColor(color, color == role ? value : source.ResolveColor(color));
        }

        foreach (var decoration in Enum.GetValues<ThemeDecoration>())
        {
            theme.SetAttributes(decoration, source.ResolveAttributes(decoration));
        }

        theme.SetStatusColors(Enum.GetValues<StatusColor>()
            .ToDictionary(status => status, source.ResolveStatusColor));
        theme.SetProfiles(source.Control, source.Input, source.Container, source.Window, source.Popup);
        theme.Freeze();
        return theme;
    }
}
