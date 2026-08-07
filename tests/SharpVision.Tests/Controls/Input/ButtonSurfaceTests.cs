// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using SharpVision.Tests.Styling;

/// <summary>Verifies Button appearance and interaction through a mounted terminal surface.</summary>
public sealed class ButtonSurfaceTests
{
    /// <summary>Verifies mounted Theme swaps use Button-specific shadow layout impact.</summary>
    [Fact]
    public async Task Theme_WhenButtonShadowVisibilityOrOffsetChanges_InvalidatesArrangeAndRenderOnlyAsync()
    {
        var hidden = TestButtonStyles.WithShadow(AppearanceTestValues.Shadow(
            visible: false,
            mode: ShadowMode.Composite,
            offset: new Point(1, 1)));
        var visible = TestButtonStyles.WithShadow(AppearanceTestValues.Shadow(
            visible: true,
            mode: ShadowMode.Composite,
            offset: new Point(1, 1)));
        var moved = TestButtonStyles.WithShadow(AppearanceTestValues.Shadow(
            visible: true,
            mode: ShadowMode.Composite,
            offset: new Point(2, 1)));
        var button = new Button("Run");
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            CreateTheme(hidden.Shadow),
            TestContext.Current.CancellationToken);
        var visibilityImpact = Invalidation.None;
        var offsetImpact = Invalidation.None;

        await surface.UpdateAsync(
            () =>
            {
                button.Clear(Invalidation.All);
                button.PropagateTheme(CreateTheme(visible.Shadow));
                visibilityImpact = button.Pending;
            },
            "show Button shadow through Theme");
        await surface.UpdateAsync(
            () =>
            {
                button.Clear(Invalidation.All);
                button.PropagateTheme(CreateTheme(moved.Shadow));
                offsetImpact = button.Pending;
            },
            "move Button shadow through Theme");

        visibilityImpact.ShouldBe(Invalidation.Arrange | Invalidation.Render);
        offsetImpact.ShouldBe(Invalidation.Arrange | Invalidation.Render);
    }

    /// <summary>Verifies mounted Buttons inherit Theme presentation until a local style is authored.</summary>
    [Fact]
    public async Task Style_WhenMountedAndReset_FollowsThemeOwnershipAsync()
    {
        var button = new Button();
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        button.Style.ShouldBeNull();
        button.Theme.ShouldBeSameAs(surface.Application.Theme);
        button.ActualStyle.ShouldBe(ButtonStyle.Definition.Resolve(null, surface.Application.Theme));

        await surface.UpdateAsync(() => button.Style = ButtonStyle.Filled, "apply local filled style");
        await surface.UpdateAsync(
            () => surface.Application.Theme = ThemeCatalog.Load("default-light"),
            "replace Theme");

        button.ActualStyle.ShouldBe(ButtonStyle.Filled);

        await surface.UpdateAsync(() => button.Style = null, "resume Theme ownership");

        button.Style.ShouldBeNull();
        button.ActualStyle.ShouldBe(ButtonStyle.Definition.Resolve(null, surface.Application.Theme));
    }

    /// <summary>Verifies every curated theme gives Button and ComboBox the same normal frame weight.</summary>
    [Fact]
    public async Task Theme_WhenEachCuratedThemeIsApplied_AlignsButtonAndInputFramesAsync()
    {
        var button = new Button
        {
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Text = "Run"
        };
        var comboBox = new ComboBox
        {
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Items = ["One"],
            SelectedIndex = 0
        };
        var row = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { button, comboBox }
        };
        await using var surface = await ComponentSurface.MountAsync(
            row,
            new Size(17, 3),
            TestContext.Current.CancellationToken);

        foreach (var slug in ThemeCatalog.Slugs)
        {
            await surface.UpdateAsync(
                () => surface.Application.Theme = ThemeCatalog.Load(slug),
                $"apply {slug} theme");

            button.ActualBorder.Sides.ShouldBe(comboBox.ActualBorder.Sides, slug);
            button.ActualBorder.GlyphStyle.ShouldBe(comboBox.ActualBorder.GlyphStyle, slug);
            surface.Cell(new Point(button.Bounds.X, button.Bounds.Y)).Text.ShouldBe(
                surface.Cell(new Point(comboBox.Bounds.X, comboBox.Bounds.Y)).Text,
                slug);
        }
    }

    /// <summary>Verifies the default content alignment centers a short caption inside a fixed-width Button.</summary>
    [Fact]
    public async Task Render_WhenButtonIsWiderThanCaption_CentersCaptionByDefaultAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(12),
            Height = Length.Cells(3),
            Text = "Save"
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ┏━━━━━━━━━━┓
                             ┃   Save   ┃
                             ┗━━━━━━━━━━┛
                             """);
    }

    #region Interaction surfaces

    /// <summary>Verifies initial layout uses one border boundary over a transparent face.</summary>
    [Fact]
    public async Task Render_WhenButtonIsMounted_ShowsBorderOnlyTransparentFaceAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Text = "Save"
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldHaveState(button, VisualState.Normal);
        surface.ShouldRender("""
                             ┏━━━━━━┓
                             ┃ Save ┃
                             ┗━━━━━━┛


                             """);
        var borderColor = TerminalPalette.Project(ThemeColorHelper.InactiveBorder(ThemeCatalog.Dark), ColorDepth.Basic16);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(borderColor);
        surface.Cell(new Point(0, 0)).Style.Background.ShouldBe(ReferenceColors.Get(0));
        surface.Cell(new Point(1, 1)).Style.Background.ShouldBe(ReferenceColors.Get(0));
        surface.Cell(new Point(8, 1)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies the filled profile renders one opaque face and a sculpted fractional shadow.</summary>
    [Fact]
    public async Task Render_WhenFilledButtonIsMounted_DrawsExactFractionalSilhouetteAsync()
    {
        // Arrange
        var button = new Button
        {
            Style = ButtonStyle.Filled,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(14),
            Height = Length.Cells(1),
            Text = "Add"
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(15, 2),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                                  Add      ▄
                              ▀▀▀▀▀▀▀▀▀▀▀▀▀▀
                             """);
        var face = TerminalPalette.Project(
            ThemeColorHelper.InactiveBackground(ThemeCatalog.Dark),
            ColorDepth.Basic16);
        var shadow = TerminalPalette.Project(
            ThemeColorHelper.Shadow(ThemeCatalog.Dark),
            ColorDepth.Basic16);
        surface.Cell(new Point(0, 0)).Style.Background.ShouldBe(face);
        surface.Cell(new Point(5, 0)).Style.Background.ShouldBe(face);
        surface.Cell(new Point(13, 0)).Style.Background.ShouldBe(face);
        surface.Cell(new Point(14, 0)).Style.Foreground.ShouldBe(shadow);
        surface.Cell(new Point(1, 1)).Style.Foreground.ShouldBe(shadow);
        surface.Cell(new Point(1, 1)).Style.Background.ShouldBe(ReferenceColors.Get(0));
        surface.Cell(new Point(14, 1)).Style.Foreground.ShouldBe(shadow);
        surface.Cell(new Point(14, 1)).Style.Background.ShouldBe(ReferenceColors.Get(0));
    }

    /// <summary>Verifies a mounted filled style does not replace the Button's layout alignment.</summary>
    [Fact]
    public async Task Render_WhenFilledButtonSharesTallRow_PreservesStretchAlignmentAsync()
    {
        // Arrange
        var filled = new Button { Style = ButtonStyle.Filled, Text = "Add" };
        var standard = new Button { Text = "Ok" };
        var row = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children = { filled, standard }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            row,
            new Size(14, 3),
            TestContext.Current.CancellationToken);

        // Assert
        filled.Bounds.ShouldBe(new Rect(0, 0, 7, 3));
        standard.Bounds.ShouldBe(new Rect(9, 0, 5, 3));
        filled.TextControl.ShouldNotBeNull().Bounds.ShouldBe(new Rect(2, 0, 3, 3));
        surface.Cell(new Point(2, 0)).Text.ShouldBe("A");
        surface.Cell(new Point(7, 0)).Text.ShouldBe("▄");
        surface.Cell(new Point(9, 0)).Text.ShouldBe("┏");
    }

    /// <summary>Verifies a held filled Button removes depth without translating its opaque face.</summary>
    [Fact]
    public async Task Pointer_WhenFilledButtonIsHeld_CollapsesShadowInPlaceAsync()
    {
        // Arrange
        var clicks = 0;
        var button = new Button
        {
            Style = ButtonStyle.Filled,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(14),
            Height = Length.Cells(1),
            Text = "Add"
        };
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(15, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(button);

        // Act
        await surface.Pointer.PressAsync();

        // Assert held — face shifts right by ShadowOffset.X (1) on press
        button.Pressed.ShouldBeTrue();
        surface.Cell(new Point(6, 0)).Text.ShouldBe("A");
        surface.Cell(new Point(14, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(1, 1)).Text.ShouldBe(" ");
        clicks.ShouldBe(0);

        // Act
        await surface.Pointer.ReleaseAsync();

        // Assert released
        clicks.ShouldBe(1);
        surface.Cell(new Point(5, 0)).Text.ShouldBe("A");
        surface.Cell(new Point(14, 0)).Text.ShouldBe("▄");
        surface.Cell(new Point(1, 1)).Text.ShouldBe("▀");
    }

    /// <summary>Verifies a button keeps its intrinsic bottom shadow when an ordinary parent exactly fits its body.</summary>
    [Fact]
    public async Task Render_WhenButtonFillsParentHeight_PreservesBottomShadowAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(6),
            Height = Length.Cells(3),
            Style = TestButtonStyles.WithShadow(
                AppearanceTestValues.Shadow(visible: true, offset: new Point(1, 1), attributes: TerminalAttributes.Dim)),
            Text = "Go"
        };
        var parent = new Stack
        {
            Orientation = Orientation.Horizontal,
            Height = Length.Cells(3),
            Children = { button }
        };
        var root = new Dock { Children = { parent } };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(8, 4),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("G");
        surface.Cell(new Point(3, 0)).Text.ShouldBe("o");
        surface.Cell(new Point(2, 3)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
    }

    /// <summary>Verifies a complete block shadow survives multiple exact-fit ordinary ancestors.</summary>
    [Fact]
    public async Task Render_WhenButtonIsNestedThroughClippingAncestors_PreservesCompleteShadowAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(6),
            Height = Length.Cells(3),
            Text = "Go",
            Style = TestButtonStyles.WithShadow(
                AppearanceTestValues.Shadow(visible: true, mode: ShadowMode.BlockGlyph, offset: new Point(1, 1), glyph: new Rune('░'), attributes: TerminalAttributes.Dim)),
        };
        var inner = new Stack
        {
            Orientation = Orientation.Horizontal,
            Children = { button }
        };
        var root = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(6),
            Height = Length.Cells(3),
            Children = { inner }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(8, 5),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(6, 1)).Text.ShouldBe("░");
        surface.Cell(new Point(6, 1)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
        surface.Cell(new Point(2, 3)).Text.ShouldBe("░");
        surface.Cell(new Point(2, 3)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
    }

    /// <summary>Verifies press/release interaction follows the drawn, translated face while a
    /// whole-cell shadow is visible, instead of the untranslated Bounds the face no longer
    /// occupies. Releasing on the translated face's own bottom-right cell - outside
    /// the untranslated Bounds - must activate; that cell was a dead band before the fix.</summary>
    [Fact]
    public async Task Pointer_WhenShadowTranslatesThePressedFace_ActivatesFromTheDrawnFaceAsync()
    {
        // Arrange - a surface large enough to address the translated face's cells, which extend
        // one cell beyond the button's own untranslated Bounds (6x3 at the origin, shifted by
        // (1,1)). The button is its own root so Bounds stays exactly (0, 0, 6, 3).
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(6),
            Height = Length.Cells(3),
            Text = "Go",
            Style = TestButtonStyles.WithShadow(
                AppearanceTestValues.Shadow(visible: true, offset: new Point(1, 1)))
        };
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 6),
            TestContext.Current.CancellationToken);

        // Act - press inside the face, then drag to the translated face's own bottom-right cell
        // (6, 3): outside the untranslated Bounds (valid indices 0..5, 0..2) but squarely inside
        // the shadow-translated face the button is actually drawing while pressed.
        await surface.Pointer.MoveToAsync(button, new Point(2, 1));
        await surface.Pointer.PressAsync();
        button.Pressed.ShouldBeTrue();

        await surface.Pointer.MovePressedToAsync(new Point(6, 3));

        // Assert - the drawn face's own cell must still read as pressed.
        button.Pressed.ShouldBeTrue();

        // Act - releasing on the drawn face must activate the button.
        await surface.Pointer.ReleaseAsync();

        // Assert
        clicks.ShouldBe(1);
    }

    /// <summary>Verifies snapshots and cells preserve a wide Button content grapheme.</summary>
    [Fact]
    public async Task Render_WhenButtonContentIsWide_PreservesSurfaceCellGeometryAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(6),
            Height = Length.Cells(3),
            Text = "界"
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 5),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ┏━━━━┓
                             ┃ 界 ┃
                             ┗━━━━┛


                             """);
        var lead = surface.Cell(new Point(2, 1));
        lead.Text.ShouldBe("界");
        lead.Width.ShouldBe(2);
        var continuation = surface.Cell(new Point(3, 1));
        continuation.Continuation.ShouldBeTrue();
        continuation.LeadX.ShouldBe(2);
    }

    /// <summary>Verifies pointer hover changes only the Button foreground and border.</summary>
    [ComponentBehaviorEvidence(typeof(Button), ComponentBehavior.Mounted | ComponentBehavior.Hover)]
    [Fact]
    public async Task Pointer_WhenMovedOverButton_ShowsHoveredAppearanceAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Text = "Save"
        };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(button);

        // Assert
        surface.ShouldHaveState(button, VisualState.PointerOver);
        surface.ShouldRender("""
                             ┏━━━━━━┓
                             ┃ Save ┃
                             ┗━━━━━━┛


                             """);
        var borderColor = TerminalPalette.Project(ThemeColorHelper.HoveredBorder(ThemeCatalog.Dark), ColorDepth.Basic16);
        var inactiveBorderColor = TerminalPalette.Project(ThemeColorHelper.InactiveBorder(ThemeCatalog.Dark), ColorDepth.Basic16);
        var hoveredForeground = TerminalPalette.Project(ThemeColorHelper.HoveredForeground(ThemeCatalog.Dark), ColorDepth.Basic16);
        borderColor.ShouldNotBe(inactiveBorderColor);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(borderColor);
        var contentForeground = surface.Cell(new Point(2, 1)).Style.Foreground;
        contentForeground.ShouldBe(hoveredForeground);
        surface.Cell(new Point(2, 1)).Style.Background.ShouldBe(ReferenceColors.Get(0));
        surface.Cell(new Point(8, 1)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies a state overlay may change border glyphs without borrowing the face background.</summary>
    [Fact]
    public async Task Pointer_WhenBorderStateChanges_RendersResolvedBorderIndependentlyAsync()
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Text = "Save"
        };
        var theme = ThemeCatalog.Parse(ThemeJson.Create(extraStyles:
            """, "button": { "pointerOver": { "face": { "background": "#ff0000" }, "border": { "glyphStyle": "ascii" } } } """));
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            theme,
            TestContext.Current.CancellationToken);

        await surface.Pointer.MoveToAsync(button);

        surface.ShouldRender("""
                             +------+
                             | Save |
                             +------+
                             """);
        surface.Cell(default).Style.Background.ShouldBe(ReferenceColors.Get(0));
        surface.Cell(new Point(1, 1)).Style.Background.ShouldBe(ReferenceColors.Get(9));
    }

    /// <summary>Verifies a non-focusable button keeps normal content while its universal border still tracks hover.</summary>
    [Fact]
    public async Task Pointer_WhenButtonIsNotFocusable_PreservesNormalAppearanceAsync()
    {
        // Arrange
        var button = new Button
        {
            Focusable = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Text = "Save"
        };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(button);

        // Assert
        surface.ShouldHaveState(button, VisualState.PointerOver);
        button.CanFocus.ShouldBeFalse();
        var borderColor = TerminalPalette.Project(ThemeColorHelper.HoveredBorder(ThemeCatalog.Dark), ColorDepth.Basic16);
        surface.Cell(default).Style.Foreground.ShouldBe(borderColor);
        surface.Cell(new Point(2, 1)).Style.Foreground.ShouldBe(ReferenceColors.Get(15));
    }

    /// <summary>Verifies a held default Button changes semantic paint without translating its border.</summary>
    [ComponentBehaviorEvidence(
        typeof(Button),
        ComponentBehavior.Focus |
        ComponentBehavior.PressRelease |
        ComponentBehavior.PressedFrame |
        ComponentBehavior.UnavailableCleanup)]
    [Fact]
    public async Task Pointer_WhenPrimaryButtonIsHeld_ShowsPressedTranslatedFaceAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Text = "Save"
        };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(button);

        // Act
        await surface.Pointer.PressAsync();

        // Assert
        surface.ShouldHaveState(button, VisualState.PointerOver | VisualState.Focused | VisualState.Pressed);
        surface.ShouldRender("""
                             ┏━━━━━━┓
                             ┃ Save ┃
                             ┗━━━━━━┛


                             """);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("┏");
        var pressedBorder = TerminalPalette.Project(ThemeColorHelper.PressedBorder(ThemeCatalog.Dark), ColorDepth.Basic16);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(pressedBorder);
        surface.Cell(new Point(2, 1)).Text.ShouldBe("S");
        surface.Cell(new Point(2, 1)).Style.Foreground.ShouldBe(ReferenceColors.Get(15));
        surface.Cell(new Point(8, 1)).Style.Attributes.ShouldNotBe(TerminalAttributes.Dim);
        surface.Cell(new Point(9, 2)).Style.Attributes.ShouldBe(TerminalAttributes.None);
        surface.Cell(new Point(2, 4)).Style.Attributes.ShouldBe(TerminalAttributes.None);

        // Act unavailable while held
        await surface.UpdateAsync(() => button.Enabled = false, "disable held Button");

        // Assert unavailable cleanup
        button.Pressed.ShouldBeFalse();
        button.Focused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveState(button, VisualState.Disabled);
    }

    /// <summary>Verifies a decoded Tab key focuses the sole mounted Button and updates its cells.</summary>
    [ComponentBehaviorEvidence(
        typeof(Button),
        ComponentBehavior.Tab |
        ComponentBehavior.DirectionalExcluded)]
    [Fact]
    public async Task Keyboard_WhenTabIsPressed_ShowsFocusedAppearanceAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Text = "Save"
        };
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert
        surface.ShouldHaveState(button, VisualState.Focused);
        button.Focused.ShouldBeTrue();
        clicks.ShouldBe(0);
        surface.ShouldRender("""
                             ┏━━━━━━┓
                             ┃ Save ┃
                             ┗━━━━━━┛


                             """);
        var focusedBorder = TerminalPalette.Project(ThemeColorHelper.FocusedBorder(ThemeCatalog.Dark), ColorDepth.Basic16);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(focusedBorder);
        surface.Cell(new Point(2, 1)).Style.Foreground.ShouldBe(ReferenceColors.Get(15));
        surface.Cell(new Point(2, 1)).Style.Background.ShouldBe(ReferenceColors.Get(0));
    }

    /// <summary>Verifies the click shorthand emits move, press, and release and settles activation.</summary>
    [ComponentBehaviorEvidence(
        typeof(Button),
        ComponentBehavior.Activation | ComponentBehavior.PointerActivation)]
    [Fact]
    public async Task Pointer_WhenButtonIsClicked_ReleasesAndActivatesOnceAsync()
    {
        // Arrange
        var clicks = 0;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Text = "Save"
        };
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(button);

        // Assert
        clicks.ShouldBe(1);
        surface.ShouldHaveState(button, VisualState.PointerOver | VisualState.Focused);
        surface.ShouldRender("""
                             ┏━━━━━━┓
                             ┃ Save ┃
                             ┗━━━━━━┛


                             """);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(14));
        surface.Cell(new Point(8, 1)).Style.Attributes.ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies terminal Enter and Space input activate the focused button.</summary>
    [ComponentBehaviorEvidence(typeof(Button), ComponentBehavior.KeyboardActivation)]
    [Fact]
    public async Task Keyboard_WhenEnterAndSpaceAreCompleted_ActivatesFocusedButtonAsync()
    {
        // Arrange
        var button = new Button { Text = "Save" };
        var activations = 0;
        button.Click += (_, _) => activations++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        // Assert
        activations.ShouldBe(2);
        surface.ShouldHaveFocus(button);
    }

    /// <summary>Verifies every border/shadow combination has deterministic released and held geometry.</summary>
    /// <param name="hasBorder">Whether the Button reserves and renders its physical border.</param>
    /// <param name="hasShadow">Whether the Button renders a detached block shadow.</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Pointer_WhenChromeCombinationIsPressed_RendersExpectedGeometryAsync(
        bool hasBorder,
        bool hasShadow)
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Style = hasShadow
                ? TestButtonStyles.WithShadow(
                    AppearanceTestValues.Shadow(visible: true, mode: ShadowMode.BlockGlyph, offset: new Point(1, 1), glyph: new Rune('▓'), attributes: TerminalAttributes.Dim))
                : hasBorder
                    ? TestButtonStyles.WithBorder(new Border(
                        BorderSide.All,
                        BorderGlyphStyle.Heavy,
                        SemanticColor.ControlBorder,
                        Color.Transparent,
                        SemanticDecoration.Border))
                    : TestButtonStyles.Flat,
            Text = "Save"
        };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Act and assert released appearance
        surface.ShouldHaveState(button, VisualState.Normal);
        surface.ShouldRender(ReleasedSnapshot(hasBorder, hasShadow));

        if (hasShadow)
        {
            surface.Cell(new Point(8, 1)).Text.ShouldBe("▓");
            surface.Cell(new Point(8, 1)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
        }
        else
        {
            surface.Cell(new Point(8, 1)).Text.ShouldBe(" ");
        }

        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();

        // Assert held appearance
        surface.ShouldHaveState(button, VisualState.PointerOver | VisualState.Focused | VisualState.Pressed);
        surface.ShouldRender(PressedSnapshot(hasBorder, hasShadow));
        surface.Cell(new Point(9, 2)).Text.ShouldBe(" ");
        surface.Cell(new Point(9, 2)).Style.Attributes.ShouldBe(TerminalAttributes.None);
        surface.Cell(new Point(2, 4)).Text.ShouldBe(" ");
        surface.Cell(new Point(2, 4)).Style.Attributes.ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies Composite shadow preserves underlying glyphs while applying shadow attributes.</summary>
    [Fact]
    public async Task Render_WhenButtonHasCompositeShadow_PreservesUnderlyingGlyphAsync()
    {
        // Arrange — a backdrop with known text sits behind a composite-shadowed button
        var backdrop = new ControlText(
            "xxxxxxxxxxxx\n" +
            "xxxxxxxxxxxx\n" +
            "xxxxxxxxxxxx\n" +
            "xxxxxxxxxxxx\n" +
            "xxxxxxxxxxxx")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Style = TestButtonStyles.WithShadow(
                AppearanceTestValues.Shadow(visible: true, mode: ShadowMode.Composite, offset: new Point(1, 1), attributes: TerminalAttributes.Dim)),
            Text = "Go"
        };
        var root = new Overlay { Children = { backdrop, button } };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 5),
            TestContext.Current.CancellationToken);

        // Assert — shadow cell preserves the backdrop "x" glyph
        surface.Cell(new Point(8, 1)).Text.ShouldBe("x");
        surface.Cell(new Point(8, 1)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);

        // Assert — non-shadow backdrop cell has no Dim attribute
        surface.Cell(new Point(10, 0)).Text.ShouldBe("x");
        surface.Cell(new Point(10, 0)).Style.Attributes.ShouldBe(TerminalAttributes.None);
    }

    #endregion

    #region Test fixtures

    private static Theme CreateTheme(Shadow shadow)
    {
        var offset = shadow.Offset;
        var mode = shadow.Mode.ToString().ToLowerInvariant();
        var json = ThemeJson.Create(extraStyles: $$"""
            ,"button": { "normal": { "border": { "sides": "none" }, "shadow": {
                "visible": {{(shadow.Visible ? "true" : "false")}},
                "mode": "{{mode}}",
                "offset": { "x": {{offset.X}}, "y": {{offset.Y}} }
            } } }
            """);
        return ThemeCatalog.Parse(json);
    }

    private static string ReleasedSnapshot(bool hasBorder, bool hasShadow) =>
        (hasBorder, hasShadow) switch
        {
            (true, true) => """
                            ┏━━━━━━┓
                            ┃ Save ┃▓
                            ┗━━━━━━┛▓
                              ▓▓▓▓▓▓▓

                            """,
            (true, false) => """
                             ┏━━━━━━┓
                             ┃ Save ┃
                             ┗━━━━━━┛


                             """,
            (false, true) => """
                               Save
                                     ▓
                                     ▓
                               ▓▓▓▓▓▓▓

                             """,
            (false, false) => """
                                Save




                              """
        };

    private static string PressedSnapshot(bool hasBorder, bool hasShadow) =>
        (hasBorder, hasShadow) switch
        {
            (true, true) => """

                             ┏━━━━━━┓
                             ┃ Save ┃
                             ┗━━━━━━┛

                            """,
            (true, false) => """
                             ┏━━━━━━┓
                             ┃ Save ┃
                             ┗━━━━━━┛


                             """,
            (false, true) => """

                                Save



                             """,
            (false, false) => """
                                Save




                              """
        };

    #endregion
}
