// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

using System.Text.Json;
using System.Text.Json.Nodes;

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
        expander.GetResolvedAppearance(VisualState.Normal).BackgroundMode.ShouldBe(BackgroundMode.Transparent);
        surface.Cell(default).Style.Background.ShouldBe(ReferenceColors.Get(0));
    }

    /// <summary>Verifies a non-text header renders through the ordinary owned-control pipeline, proving the
    /// header ownership role hosts arbitrary rich content rather than only a text caption.</summary>
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

    /// <summary>Verifies a collapsed text header omits its caption after the disclosure glyph, matching
    /// the plain glyph-only row a non-text header renders through the ordinary
    /// <see cref="ControlBase.Render(TerminalCanvas)"/> gate - and matching what <see cref="Expander"/>'s
    /// own measure pass already assumes by reporting zero header width once collapsed.</summary>
    [Fact]
    public async Task Render_WhenHeaderIsCollapsed_OmitsCaptionAfterDisclosureGlyphAsync()
    {
        // Arrange
        var expander = new Expander
        {
            HeaderText = "Hidden",
            Content = new ControlText("Body"),
            Width = Length.Cells(12),
            Height = Length.Cells(4)
        };
        expander.Header!.Visibility = Visibility.Collapsed;

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(12, 4),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ▼
                               Body


                             """);
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

    /// <summary>Verifies content collapsed directly through Visibility - while IsExpanded stays true,
    /// distinct from collapsing by toggling IsExpanded - clears its committed cells and hit target,
    /// and restoring it through Visibility alone redraws exactly the original cells without ever
    /// touching the expand/collapse toggle.</summary>
    [Fact]
    public async Task UpdateAsync_WhenContentCollapsesViaVisibilityWhileExpanded_ClearsThenRedrawsCellsAsync()
    {
        // Arrange
        var clicked = 0;
        var content = new Button
        {
            Text = "GO",
            Style = TestButtonStyles.FlatWithPadding(default),
            Padding = default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        content.Click += (_, _) => clicked++;
        var expander = new Expander
        {
            HeaderText = "Details",
            Content = content,
            Width = Length.Cells(12),
            Height = Length.Cells(2)
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(content);
        clicked.ShouldBe(1);
        // The button centers "GO" inside its filled 10-cell content face, starting at column 6
        // (content.Bounds.X 2 plus half the 8-cell centering remainder).
        surface.Cell(new Point(6, 1)).Text.ShouldBe("G");
        surface.Cell(new Point(7, 1)).Text.ShouldBe("O");

        // Act collapse content directly via Visibility, leaving IsExpanded untouched
        await surface.UpdateAsync(
            () => content.Visibility = Visibility.Collapsed,
            "collapse Expander content via Visibility");

        // Assert IsExpanded is unaffected while content geometry and cells clear
        expander.IsExpanded.ShouldBeTrue();
        content.Bounds.ShouldBe(default);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("▼");
        surface.Cell(new Point(6, 1)).Text.ShouldBe(" ");
        await surface.Pointer.ClickAsync(expander, new Point(2, 1));
        clicked.ShouldBe(1);

        // Act restore content directly via Visibility
        await surface.UpdateAsync(
            () => content.Visibility = Visibility.Visible,
            "restore Expander content via Visibility");

        // Assert cells and hit target return without an intervening re-expand
        expander.IsExpanded.ShouldBeTrue();
        surface.Cell(new Point(6, 1)).Text.ShouldBe("G");
        surface.Cell(new Point(7, 1)).Text.ShouldBe("O");
        await surface.Pointer.ClickAsync(content);
        clicked.ShouldBe(2);
    }

    /// <summary>Verifies pointer, Space, and Enter activation share focus and changed-event behavior.</summary>
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
        surface.ShouldHaveState(expander, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);
        surface.ShouldHaveCapture(expander);

        // Act release
        await surface.Pointer.ReleaseAsync();

        // Assert pointer
        expander.IsExpanded.ShouldBeFalse();
        changes.ShouldBe(1);
        surface.ShouldHaveState(expander, VisualState.IsPointerOver | VisualState.Focused);

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

        // Rendered cells carry the mounted surface's Basic16 palette depth, so theme-resolved
        // expectations are projected before comparison.
        var normal = TerminalPalette.Project(ThemeColorHelper.InactiveForeground(theme), ColorDepth.Basic16);
        var hovered = TerminalPalette.Project(ThemeColorHelper.HoveredForeground(theme), ColorDepth.Basic16);
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

        // Assert header appearance without leaking the cue into the body subtree
        expander.IsPointerDirectlyOver.ShouldBeTrue();
        body.IsPointerOver.ShouldBeFalse();
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(hovered);
        surface.Cell(new Point(2, 1)).Style.Foreground.ShouldBe(normal);

        // Act over a caption cell, which targets the hit-test-visible header child
        await surface.Pointer.MoveToAsync(expander, new Point(3, 0));

        // Assert the caption itself remains a header hover target
        expander.IsPointerDirectlyOver.ShouldBeFalse();
        expander.IsPointerOver.ShouldBeTrue();
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(hovered);
        surface.Cell(new Point(2, 1)).Style.Foreground.ShouldBe(normal);
    }

    /// <summary>Verifies disabled input refuses toggles and collapsed replacement appears only after expansion.</summary>
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
        surface.ShouldHaveState(expander, VisualState.Disabled);

        // The disable cleanup dropped the control-side capture and press without the test-side
        // pointer driver observing it, so release its own bookkeeping before pressing again.
        await surface.Pointer.ReleaseAsync();

        // Act re-enable and resume interaction
        await surface.UpdateAsync(() => expander.IsEnabled = true, "re-enable Expander");
        surface.ShouldHaveState(expander, VisualState.Normal);
        await surface.Pointer.ClickAsync(expander, new Point(1, 0));

        // Assert normal interaction resumes
        expander.IsExpanded.ShouldBeFalse();
    }

    /// <summary>Verifies an Expander inherits its disabled state from an ancestor and keeps stable
    /// geometry across a genuine resize while disabled, matching an independently-mounted enabled
    /// instance arranged at the same size.</summary>
    [Fact]
    public async Task Input_WhenAncestorDisablesExpanderAndResized_InheritsStateAndPreservesGeometryAsync()
    {
        // Arrange an Expander disabled only through its ancestor
        var expander = new Expander
        {
            HeaderText = "Details",
            Content = new ControlText("Body"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { expander }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(12, 4),
            TestContext.Current.CancellationToken);

        // Act disable the ancestor, not the Expander itself
        await surface.UpdateAsync(() => overlay.IsEnabled = false, "disable Expander's ancestor");

        // Assert the disabled state is inherited
        expander.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(expander, VisualState.Disabled);

        // Act resize to a genuinely different size while disabled
        await surface.ResizeAsync(new Size(20, 6));

        // Assert geometry matches an independently-mounted enabled instance at the same size
        var reference = new Expander
        {
            HeaderText = "Details",
            Content = new ControlText("Body"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            reference,
            new Size(20, 6),
            TestContext.Current.CancellationToken);

        expander.Bounds.ShouldBe(reference.Bounds);
        expander.DesiredSize.ShouldBe(reference.DesiredSize);
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
        surface.Cell(new Point(3, 0)).Continuation.ShouldBeTrue();

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
        surface.Cell(new Point(3, 0)).Continuation.ShouldBeTrue();
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
            Style = ExpanderStyle.Default with
            {
                ExpandedGlyph = new Rune('-'),
                CollapsedGlyph = new Rune('+')
            },
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

    /// <summary>Verifies ContentIndent controls the indentation of expanded content on a surface,
    /// and that the header label moves in lockstep with it rather than staying fixed - the two
    /// used to desync whenever ContentIndent departed from its coincidentally-matching default.</summary>
    [Fact]
    public async Task Render_WhenContentIndentChanges_ShiftsContentHorizontallyAsync()
    {
        // Arrange
        var expander = new Expander
        {
            HeaderText = "Details",
            Content = new ControlText("Body"),
            Style = ExpanderStyle.Default with { ContentIndent = 4 },
            Width = Length.Cells(14),
            Height = Length.Cells(2)
        };
        await using var surface = await ComponentSurface.MountAsync(
            expander,
            new Size(14, 2),
            TestContext.Current.CancellationToken);

        // Assert the header label shares the content's four-cell indent
        surface.ShouldRender("""
                             ▼   Details
                                 Body
                             """);

        // Act change indent
        await surface.UpdateAsync(() => expander.Style = ExpanderStyle.Default with { ContentIndent = 0 }, "remove content indent");

        // Assert the header slot still keeps its one-cell structural floor even though the
        // content itself sits flush left
        surface.ShouldRender("""
                             ▼Details
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

    /// <summary>Verifies direct focus uses the focused-border accent without painting a focused
    /// face behind either the header row or the indented content region.</summary>
    [Fact]
    public async Task Surface_WhenFocused_UsesAccentWithoutPaintingBackgroundAsync()
    {
        // Arrange
        var focusedBorder = Color.Rgb(10, 200, 220);
        var theme = WithColor(SemanticColor.FocusedBorder, focusedBorder);
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
            theme,
            TestContext.Current.CancellationToken);
        var headerBackground = surface.Cell(default).Style.Background;
        var contentBackground = surface.Cell(new Point(0, 1)).Style.Background;

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.Cell(default).Style.Foreground.ShouldBe(TerminalPalette.Project(focusedBorder, ColorDepth.Basic16));
        surface.Cell(new Point(2, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(focusedBorder, ColorDepth.Basic16));
        surface.Cell(default).Style.Background.ShouldBe(headerBackground);
        surface.Cell(new Point(0, 1)).Style.Background.ShouldBe(contentBackground);
    }

    /// <summary>Verifies a theme-authored focused face may still contribute text decorations while
    /// its background remains transparent and the focused border owns the focus color.</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsDistinctFocusedControlFace_PreservesTransparentSurfaceAsync()
    {
        // Arrange
        var authoredBackground = Color.Rgb(220, 90, 30);
        var theme = WithFocusedInputFace(authoredBackground);
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
            theme,
            TestContext.Current.CancellationToken);
        var background = surface.Cell(default).Style.Background;

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.Cell(default).Style.Background.ShouldBe(background);
        surface.Cell(default).Style.Background.ShouldNotBe(
            TerminalPalette.Project(authoredBackground, ColorDepth.Basic16));
        surface.Cell(default).Style.Foreground.ShouldBe(
            TerminalPalette.Project(theme.ResolveColor(SemanticColor.FocusedBorder), ColorDepth.Basic16));
        surface.Cell(default).Style.Attributes.ShouldBe(TerminalAttributes.None);
    }

    // Copies every well-known theme value from ThemeCatalog.Dark unchanged, then overlays a
    // "styles.input.focused.face" whose background is a literal color distinct from
    // "focusedControl" and whose attributes drop the bold every shipped theme authors for that
    // role - so a render path bypassing the resolved style couldn't coincidentally produce the
    // same result the way it does against the 15 shipped themes.
    private static Theme WithFocusedInputFace(Color background)
    {
        var source = ThemeCatalog.Dark;
        var palette = new Dictionary<string, Color>(source.Palette, StringComparer.Ordinal)
        {
            ["__focusedControlFace"] = background
        };
        var theme = new Theme(
            palette,
            source.Name,
            source.Slug,
            source.ColorScheme,
            source.Author,
            source.License,
            source.Source);

        foreach (var color in Enum.GetValues<SemanticColor>())
        {
            theme.SetColor(color, source.ResolveColor(color));
        }

        foreach (var decoration in Enum.GetValues<SemanticDecoration>())
        {
            theme.SetAttributes(decoration, source.ResolveAttributes(decoration));
        }

        var sections = new Dictionary<string, JsonElement>(source.StyleSections);
        var input = JsonNode.Parse(sections["input"].GetRawText())!.AsObject();
        input["focused"]!["face"] = JsonNode.Parse(
            """{ "foreground": "focusedText", "background": "__focusedControlFace", "attributes": [] }""");

        using (var inputDocument = JsonDocument.Parse(input.ToJsonString()))
        {
            sections["input"] = inputDocument.RootElement.Clone();
        }

        theme.SetStyleSections(sections);
        theme.Freeze();
        return theme;
    }

    private static Theme WithColor(SemanticColor role, Color value)
    {
        var source = ThemeCatalog.Dark;
        var theme = new Theme(
            source.Palette,
            source.Name,
            source.Slug,
            source.ColorScheme,
            source.Author,
            source.License,
            source.Source);

        foreach (var color in Enum.GetValues<SemanticColor>())
        {
            theme.SetColor(color, color == role ? value : source.ResolveColor(color));
        }

        foreach (var decoration in Enum.GetValues<SemanticDecoration>())
        {
            theme.SetAttributes(decoration, source.ResolveAttributes(decoration));
        }

        theme.SetStyleSections(new Dictionary<string, JsonElement>(source.StyleSections));
        theme.Freeze();
        return theme;
    }
}
