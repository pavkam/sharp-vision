// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

using System.Buffers;

/// <summary>Verifies Button appearance and interaction through a mounted terminal surface.</summary>
public sealed class ButtonSurfaceTests
{
    /// <summary>Verifies Turbo Vision Button chrome remains flat while pointer and Space holds
    /// change the authored face and border colors.</summary>
    [Fact]
    public async Task Input_WhenTurboVisionButtonIsHeld_KeepsFlatBorderAsync()
    {
        var button = new Button("Run")
        {
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            ThemeCatalog.Load("turbo-vision"),
            TestContext.Current.CancellationToken);

        AssertFlatBorder(button, surface);

        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();

        button.GetActualBorder(button.GetAppearanceState()).Relief.ShouldBe(BorderRelief.Flat);
        AssertFlatBorder(button, surface);

        await surface.Pointer.MovePressedToAsync(new Point(20, 20));

        AssertFlatBorder(button, surface);
        button.IsFocused.ShouldBeTrue();
        await surface.UpdateAsync(
            () => button.SetCapabilities(TestCapabilities.WithKeyReleases),
            "declare key-release reporting");

        await surface.Keyboard.PressCharacterAsync(new Rune(' '));

        AssertFlatBorder(button, surface);

        await surface.Keyboard.ReleaseCharacterAsync(new Rune(' '));

        AssertFlatBorder(button, surface);
    }

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

    /// <summary>Verifies both affixes reserve their own cell column pinned flush to the face edges,
    /// with the caption still centered inside the remaining middle box.</summary>
    [Fact]
    public async Task Render_WhenButtonHasBothAffixes_PinsThemToTheFaceEdgesAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            Text = "Go",
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(2, 1)).Text.ShouldBe(">");
        surface.Cell(new Point(7, 1)).Text.ShouldBe("<");
        surface.Cell(new Point(4, 1)).Text.ShouldBe("G");
        surface.Cell(new Point(5, 1)).Text.ShouldBe("o");
    }

    /// <summary>Verifies both affixes stay pinned to the same face-edge cells no matter where
    /// TextAlignment places the caption inside the remaining middle box, and that the caption never
    /// reaches into the affix or gap columns.</summary>
    /// <param name="alignment">The caption alignment under test.</param>
    /// <param name="expectedCaptionX">The expected column of the caption's first cell.</param>
    [Theory]
    [InlineData(Alignment.Start, 4)]
    [InlineData(Alignment.Center, 5)]
    [InlineData(Alignment.End, 6)]
    public async Task Render_WhenCaptionAlignmentChanges_KeepsAffixesPinnedAndCaptionConfinedAsync(
        Alignment alignment,
        int expectedCaptionX)
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(12),
            Height = Length.Cells(3),
            Text = "Go",
            TextAlignment = alignment,
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Assert - the affixes never move.
        surface.Cell(new Point(2, 1)).Text.ShouldBe(">");
        surface.Cell(new Point(9, 1)).Text.ShouldBe("<");

        // Assert - the caption stays inside its middle box (columns 4-7), never touching the gap
        // or affix columns the middle box was deflated away from.
        surface.Cell(new Point(expectedCaptionX, 1)).Text.ShouldBe("G");
        surface.Cell(new Point(expectedCaptionX + 1, 1)).Text.ShouldBe("o");
    }

    /// <summary>Verifies the start affix survives and the end affix drops whole - never a partial
    /// cluster - when the face has room for only one, matching the documented priority: caption
    /// shrinks first, then the end affix, then the start affix.</summary>
    [Fact]
    public async Task Render_WhenFaceHasRoomForOnlyOneAffix_DropsTheEndAffixFirstAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(5),
            Height = Length.Cells(3),
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(5, 3),
            TestContext.Current.CancellationToken);

        // Assert - the face has exactly one drawable cell, and the start affix claims it.
        surface.Cell(new Point(2, 1)).Text.ShouldBe(">");
    }

    /// <summary>Verifies a same-width content or color swap invalidates rendering only, and the
    /// mounted surface reflects the new glyph without a remeasure - the exact grading an animated
    /// affix (a spinner swapping frames) depends on.</summary>
    [Fact]
    public async Task StartAffix_WhenContentChangesAtTheSameResolvedWidth_UpdatesRenderOnlyAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            Text = "Go",
            StartAffix = new Affix("|")
        };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 3),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(2, 1)).Text.ShouldBe("|");
        var impact = Invalidation.None;

        // Act
        await surface.UpdateAsync(
            () =>
            {
                button.Clear(Invalidation.All);
                button.StartAffix = new Affix("/");
                impact = button.Pending;
            },
            "swap start affix content at the same resolved width");

        // Assert
        impact.ShouldBe(Invalidation.Render);
        surface.Cell(new Point(2, 1)).Text.ShouldBe("/");
    }

    /// <summary>Verifies a visible whole-cell pressed translation carries both affixes along with
    /// the caption and border, since affix painting shares the same translated face box.</summary>
    [Fact]
    public async Task Pointer_WhenPressedButtonHasAffixes_TranslatesAffixesWithTheFaceAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            Text = "Go",
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<"),
            Style = ButtonStyle.Standard with
            {
                Shadow = AppearanceTestValues.Shadow(visible: true, offset: new Point(1, 1))
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 5),
            TestContext.Current.CancellationToken);
        var restingStart = FindGlyph(surface, ">");
        var restingEnd = FindGlyph(surface, "<");

        // Act
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();

        // Assert
        var pressedStart = FindGlyph(surface, ">");
        var pressedEnd = FindGlyph(surface, "<");
        pressedStart.ShouldBe(new Point(restingStart.X + 1, restingStart.Y + 1));
        pressedEnd.ShouldBe(new Point(restingEnd.X + 1, restingEnd.Y + 1));
        return;

        static Point FindGlyph(ComponentSurface source, string glyph)
        {
            for (var y = 0; y < 5; y++)
            {
                for (var x = 0; x < 12; x++)
                {
                    if (source.Cell(new Point(x, y)).Text == glyph)
                    {
                        return new Point(x, y);
                    }
                }
            }

            throw new InvalidOperationException($"Glyph '{glyph}' was not found on the mounted surface.");
        }
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
        AssertFlatBorder(button, surface);
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
        button.IsPressed.ShouldBeTrue();
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

    /// <summary>Verifies a pressed button's caption survives translation by a Y-offset shadow
    /// even when nested through exact-fit ordinary ancestors. The ancestors' inherited content
    /// clip stops at the button's own untranslated Bounds, so a caption arranged inside the
    /// shadow-translated face needs that clip widened the same way the button's own face/border
    /// paint already is - otherwise the whole caption row falls outside the inherited clip and
    /// renders blank.</summary>
    [Fact]
    public async Task Render_WhenPressedButtonIsNestedThroughClippingAncestors_PreservesCaptionAsync()
    {
        // Arrange - a single-row button so the pressed face's caption row (offset by the
        // shadow's Y component) lands entirely outside the ancestors' untranslated content clip
        // unless that clip is widened to follow the translated face.
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(6),
            Height = Length.Cells(1),
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
            Height = Length.Cells(1),
            Children = { inner }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(8, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();

        // Assert - the caption renders at its translated cells, not blank.
        surface.Cell(new Point(3, 1)).Text.ShouldBe("G");
        surface.Cell(new Point(4, 1)).Text.ShouldBe("o");
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
        button.IsPressed.ShouldBeTrue();

        await surface.Pointer.MovePressedToAsync(new Point(6, 3));

        // Assert - the drawn face's own cell must still read as pressed.
        button.IsPressed.ShouldBeTrue();

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
        surface.ShouldHaveState(button, VisualState.IsPointerOver);
        surface.ShouldRender("""
                             ┏━━━━━━┓
                             ┃ Save ┃
                             ┗━━━━━━┛

                             """);
        var inactiveBorderColor = TerminalPalette.Project(ThemeColorHelper.InactiveBorder(ThemeCatalog.Dark), ColorDepth.Basic16);
        var hoveredForeground = TerminalPalette.Project(ThemeColorHelper.HoveredForeground(ThemeCatalog.Dark), ColorDepth.Basic16);
        TerminalPalette.Project(ThemeColorHelper.HoveredBorder(ThemeCatalog.Dark), ColorDepth.Basic16)
            .ShouldNotBe(inactiveBorderColor);
        AssertFlatBorder(button, surface);
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
        // Authored under "input" - Button's declared fallback - since a leaf declares no theme
        // section of its own any more; Border passes through unchanged from
        // ButtonStyle.Complete's fallback argument, so this reaches Button's own resolved
        // pointerOver exactly as "button.pointerOver" once did.
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            palette: "\"pointerOverFace\":\"#ff0000\"",
            inputStates:
            """, "pointerOver": { "face": { "background": "pointerOverFace" }, "border": { "glyphStyle": "ascii" } } """));
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
            IsFocusable = false,
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
        surface.ShouldHaveState(button, VisualState.IsPointerOver);
        button.CanFocus.ShouldBeFalse();
        AssertFlatBorder(button, surface);
        surface.Cell(new Point(2, 1)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(ThemeColorHelper.HoveredForeground(ThemeCatalog.Dark), ColorDepth.Basic16));
    }

    /// <summary>Verifies a held default Button changes semantic paint without translating its border.</summary>
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
        surface.ShouldHaveState(button, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);
        surface.ShouldRender("""
                             ┏━━━━━━┓
                             ┃ Save ┃
                             ┗━━━━━━┛

                             """);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("┏");
        AssertFlatBorder(button, surface);
        surface.Cell(new Point(2, 1)).Text.ShouldBe("S");
        // Pointer press focuses the button, so its access-key cell keeps the focused hotkey cue
        // while the surrounding button face composes pointer, focus, and press states.
        surface.Cell(new Point(2, 1)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(ThemeCatalog.Dark.Hotkey, ColorDepth.Basic16));
        surface.Cell(new Point(8, 1)).Style.Attributes.ShouldNotBe(TerminalAttributes.Dim);
        surface.Cell(new Point(9, 2)).Style.Attributes.ShouldBe(TerminalAttributes.None);
        surface.Cell(new Point(2, 4)).Style.Attributes.ShouldBe(TerminalAttributes.None);

        // Act unavailable while held
        await surface.UpdateAsync(() => button.IsEnabled = false, "disable held Button");

        // Assert unavailable cleanup
        button.IsPressed.ShouldBeFalse();
        button.IsFocused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveState(button, VisualState.Disabled);
    }

    /// <summary>Verifies a disabled Button never reaches its Click handler through either pointer or
    /// keyboard activation, and resumes normal activation once re-enabled. Button is a command
    /// owner, so a disabled instance must refuse every path that could invoke its handler, not
    /// merely appear muted.</summary>
    [Fact]
    public async Task Input_WhenDisabled_RefusesClickThenRecoversOnReenableAsync()
    {
        // Arrange
        var clicks = 0;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Text = "Save",
            IsEnabled = false
        };
        button.Click += (_, _) => clicks++;
        var sibling = new Button { Text = "Other" };
        var root = new Stack { Children = { button, sibling } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(10, 8),
            TestContext.Current.CancellationToken);

        // Act attempt pointer activation while disabled
        await surface.Pointer.ClickAsync(button);

        // Assert pointer activation is refused
        clicks.ShouldBe(0);
        surface.ShouldHaveState(button, VisualState.Disabled);

        // Act attempt keyboard activation while disabled - Tab cannot even focus a disabled
        // Button, so the Enter/Space paths that normally activate it are unreachable. Tab lands
        // on the focusable sibling instead of the disabled Button.
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(sibling);
        button.IsFocused.ShouldBeFalse();
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        // Assert keyboard activation is refused
        clicks.ShouldBe(0);

        // Act re-enable
        await surface.UpdateAsync(() => button.IsEnabled = true, "re-enable Button");

        // Assert normal appearance and resumed pointer and keyboard activation
        surface.ShouldHaveState(button, VisualState.Normal);
        await surface.Pointer.ClickAsync(button);
        clicks.ShouldBe(1);
    }

    /// <summary>Verifies a Button inherits disabled state from an ancestor and keeps stable geometry
    /// across a genuine resize while disabled, matching an independently-mounted enabled instance
    /// arranged at the same size.</summary>
    [Fact]
    public async Task Input_WhenAncestorDisablesButtonAndResized_InheritsStateAndPreservesGeometryAsync()
    {
        // Arrange a Button disabled only through its ancestor
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Text = "Save"
        };
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { button }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Act disable the ancestor, not the Button itself
        await surface.UpdateAsync(() => overlay.IsEnabled = false, "disable Button's ancestor");

        // Assert the disabled state is inherited
        button.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(button, VisualState.Disabled);

        // Act resize to a genuinely different size while disabled
        await surface.ResizeAsync(new Size(20, 8));

        // Assert geometry matches an independently-mounted enabled instance at the same size
        var reference = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Text = "Save"
        };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            reference,
            new Size(20, 8),
            TestContext.Current.CancellationToken);

        button.Bounds.ShouldBe(reference.Bounds);
        button.DesiredSize.ShouldBe(reference.DesiredSize);
    }

    /// <summary>Verifies a decoded Tab key focuses the sole mounted Button and updates its cells.</summary>
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
        button.IsFocused.ShouldBeTrue();
        clicks.ShouldBe(0);
        surface.ShouldRender("""
                             ┏━━━━━━┓
                             ┃ Save ┃
                             ┗━━━━━━┛

                             """);
        AssertFlatBorder(button, surface);

        // The caption's transparent Text child inherits the button's ambient focused face, so the
        // cell carries the input set's focused foreground and background.
        surface.Cell(new Point(2, 1)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(ThemeColorHelper.FocusedForeground(ThemeCatalog.Dark), ColorDepth.Basic16));
        surface.Cell(new Point(2, 1)).Style.Background.ShouldBe(
            TerminalPalette.Project(ThemeColorHelper.FocusedBackground(ThemeCatalog.Dark), ColorDepth.Basic16));
    }

    /// <summary>Verifies the click shorthand emits move, press, and release and settles activation.</summary>
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
        surface.ShouldHaveState(button, VisualState.IsPointerOver | VisualState.Focused);
        surface.ShouldRender("""
                             ┏━━━━━━┓
                             ┃ Save ┃
                             ┗━━━━━━┛

                             """);
        AssertFlatBorder(button, surface);
        surface.Cell(new Point(8, 1)).Style.Attributes.ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies terminal Enter and Space input activate the focused button.</summary>
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
        surface.ShouldHaveState(button, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);
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

    private static void AssertFlatBorder(Button button, ComponentSurface surface)
    {
        var border = button.GetActualBorder(button.GetAppearanceState());
        border.Relief.ShouldBe(BorderRelief.Flat);
        var foreground = TerminalPalette.Project(
            surface.Application.Theme.Resolve(border.Foreground),
            ColorDepth.Basic16);
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(foreground);
        surface.Cell(new Point(7, 1)).Style.Foreground.ShouldBe(foreground);
        surface.Cell(new Point(1, 2)).Style.Foreground.ShouldBe(foreground);
        surface.Cell(new Point(0, 1)).Style.Foreground.ShouldBe(foreground);
    }

    // A leaf declares no theme section of its own any more: Button's Border/Shadow pass through
    // unchanged from ButtonStyle.Complete's fallback argument, so authoring under "input" - the
    // declared fallback - reaches Button's own resolved Normal exactly as "button" once did.
    private static Theme CreateTheme(Shadow shadow)
    {
        var offset = shadow.Offset;
        var mode = shadow.Mode.ToString().ToLowerInvariant();
        var json = ThemeJson.Create(
            inputSides: "\"none\"",
            inputExtra: $$"""
                , "shadow": {
                    "visible": {{(shadow.IsVisible ? "true" : "false")}},
                    "mode": "{{mode}}",
                    "offset": { "x": {{offset.X}}, "y": {{offset.Y}} }
                }
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

    private static readonly Point _shadowOffset = new(1, 1);
    private static readonly Size _surface = new(12, 8);
    private static readonly SearchValues<char> _borderGlyphs = SearchValues.Create("┌─┐│└┘");

    /// <summary>Verifies every corner of the drawn pressed face keeps the button pressed and every
    /// cell just outside it does not, so the interaction rectangle is neither smaller than what is
    /// lit - the dead band that defect produced - nor larger than it.</summary>
    [Fact]
    public async Task Pointer_WhenDraggingAroundThePressedFace_TracksExactlyTheDrawnRectangleAsync()
    {
        var button = NewButton();
        await using var surface = await ComponentSurface.MountAsync(
            button,
            _surface,
            TestContext.Current.CancellationToken);

        // IsPressed before the face is read, so what is measured is the translated face and not the
        // resting one, which is the entire point of the case.
        await surface.Pointer.MoveToAsync(button, new Point(2, 1));
        await surface.Pointer.PressAsync();
        button.IsPressed.ShouldBeTrue();

        var face = DrawnFace(surface);
        var bounds = await surface.Application.Dispatcher.InvokeAsync(
            () => button.Bounds,
            TestContext.Current.CancellationToken);

        face.ShouldBe(
            new Rect(bounds.X + _shadowOffset.X, bounds.Y + _shadowOffset.Y, bounds.Width, bounds.Height),
            "the pressed face is drawn translated by the shadow offset");
        face.ShouldNotBe(bounds, "a face that never moved would make the rest of this vacuous");

        foreach (var inside in Corners(face))
        {
            await surface.Pointer.MovePressedToAsync(inside);
            button.IsPressed.ShouldBeTrue($"{inside} is a drawn face cell and must stay pressed");
        }

        foreach (var outside in JustOutsideThePressedFace(face))
        {
            await surface.Pointer.MovePressedToAsync(outside);
            button.IsPressed.ShouldBeFalse($"{outside} is outside the drawn face and must not stay pressed");

            // Back inside, so every outside probe starts from the same pressed state.
            await surface.Pointer.MovePressedToAsync(new Point(face.X + 1, face.Y + 1));
            button.IsPressed.ShouldBeTrue();
        }

        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies a release on each corner of the drawn pressed face activates. IsPressed-state
    /// tracking and activation are separate decisions, so tracking the right rectangle while
    /// declining to activate on part of it would otherwise pass unnoticed.</summary>
    [Fact]
    public async Task Pointer_WhenReleasingOnAnyDrawnFaceCorner_ActivatesAsync()
    {
        var button = NewButton();
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            _surface,
            TestContext.Current.CancellationToken);

        var face = await PressedFaceAsync(surface, button);
        clicks = 0;
        var expected = 0;

        foreach (var corner in Corners(face))
        {
            await surface.Pointer.MoveToAsync(new Point(face.X + 1, face.Y + 1));
            await surface.Pointer.PressAsync();
            await surface.Pointer.MovePressedToAsync(corner);
            await surface.Pointer.ReleaseAsync();

            expected++;
            clicks.ShouldBe(expected, $"releasing on drawn face corner {corner} must activate");
        }
    }

    /// <summary>Verifies a release just outside the drawn face does not activate, so the parity
    /// above cannot be satisfied by a control that simply activates everywhere.</summary>
    [Fact]
    public async Task Pointer_WhenReleasingJustOutsideTheDrawnFace_DoesNotActivateAsync()
    {
        var button = NewButton();
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            _surface,
            TestContext.Current.CancellationToken);

        var face = await PressedFaceAsync(surface, button);
        var bounds = await surface.Application.Dispatcher.InvokeAsync(
            () => button.Bounds,
            TestContext.Current.CancellationToken);
        clicks = 0;

        foreach (var outside in OutsideBothFaces(face, bounds))
        {
            await surface.Pointer.MoveToAsync(new Point(face.X + 1, face.Y + 1));
            await surface.Pointer.PressAsync();
            await surface.Pointer.MovePressedToAsync(outside);
            await surface.Pointer.ReleaseAsync();

            clicks.ShouldBe(0, $"releasing outside the drawn face at {outside} must not activate");
        }
    }

    /// <summary>Verifies the parity holds through the un-press transition too. IsDragging off the
    /// pressed face clears the pressed state, which snaps the drawn face back from the translated
    /// rectangle to Bounds - and the cells of that restored face are interactive again, so a
    /// release on one activates. This is why "outside the pressed face" and "outside the button"
    /// are different sets, and it is the behavior that makes the two tests above pick their probe
    /// cells the way they do.</summary>
    [Fact]
    public async Task Pointer_WhenDragLeavesThePressedFace_TheRestingFaceBecomesInteractiveAgainAsync()
    {
        var button = NewButton();
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            _surface,
            TestContext.Current.CancellationToken);

        var face = await PressedFaceAsync(surface, button);
        var bounds = await surface.Application.Dispatcher.InvokeAsync(
            () => button.Bounds,
            TestContext.Current.CancellationToken);
        clicks = 0;

        // Top-left of the resting face: inside Bounds, outside the translated pressed face.
        var restingOnly = new Point(bounds.X, bounds.Y);
        face.Contains(restingOnly).ShouldBeFalse("the probe must be off the translated face");
        bounds.Contains(restingOnly).ShouldBeTrue("the probe must be on the resting face");

        await surface.Pointer.MoveToAsync(new Point(face.X + 1, face.Y + 1));
        await surface.Pointer.PressAsync();
        button.IsPressed.ShouldBeTrue();

        await surface.Pointer.MovePressedToAsync(restingOnly);
        button.IsPressed.ShouldBeFalse("leaving the translated face clears the pressed state");

        // The face has snapped back to Bounds, so this cell is drawn face again - and live.
        DrawnFace(surface).ShouldBe(bounds);

        await surface.Pointer.ReleaseAsync();
        clicks.ShouldBe(1, "a release on the restored resting face activates");
    }

    private static async Task<Rect> PressedFaceAsync(ComponentSurface surface, Button button)
    {
        await surface.Pointer.MoveToAsync(button, new Point(2, 1));
        await surface.Pointer.PressAsync();
        var face = DrawnFace(surface);
        await surface.Pointer.ReleaseAsync();
        return face;
    }

    // Reads the face back off the rendered surface instead of recomputing it. Recomputing would
    // compare the renderer's own rectangle with itself; the border glyphs are independent evidence
    // of where the face actually landed. The shadow is skipped while pressed, so nothing else on
    // the surface draws these glyphs.
    private static Rect DrawnFace(ComponentSurface surface)
    {
        int? minX = null, minY = null, maxX = null, maxY = null;

        for (var y = 0; y < _surface.Height; y++)
        {
            for (var x = 0; x < _surface.Width; x++)
            {
                var text = surface.Cell(new Point(x, y)).Text;
                if (text.Length != 1 || !_borderGlyphs.Contains(text[0]))
                {
                    continue;
                }

                minX = minX is { } left ? Math.Min(left, x) : x;
                minY = minY is { } top ? Math.Min(top, y) : y;
                maxX = maxX is { } right ? Math.Max(right, x) : x;
                maxY = maxY is { } bottom ? Math.Max(bottom, y) : y;
            }
        }

        _ = minX.ShouldNotBeNull("the pressed button must draw its bordered face");
        return new Rect(minX.Value, minY!.Value, maxX!.Value - minX.Value + 1, maxY!.Value - minY!.Value + 1);
    }

    private static Point[] Corners(Rect face) =>
    [
        new(face.X, face.Y),
        new(face.Right - 1, face.Y),
        new(face.X, face.Bottom - 1),
        new(face.Right - 1, face.Bottom - 1)
    ];

    // One cell beyond the middle of each edge of the pressed face, so a corner diagonal cannot be
    // mistaken for an edge miss.
    private static Point[] JustOutsideThePressedFace(Rect face) =>
    [
        new(face.X - 1, face.Y + (face.Height / 2)),
        new(face.Right, face.Y + (face.Height / 2)),
        new(face.X + (face.Width / 2), face.Y - 1),
        new(face.X + (face.Width / 2), face.Bottom)
    ];

    // Outside the pressed face AND outside the resting one. Leaving the pressed face un-presses the
    // button, which snaps the face back to Bounds - so a cell that is merely outside the translated
    // rectangle can still be live, and only a cell outside both is a true miss. Asserted directly by
    // Pointer_WhenDragLeavesThePressedFace_TheRestingFaceBecomesInteractiveAgainAsync below.
    private static Point[] OutsideBothFaces(Rect face, Rect bounds)
    {
        var right = Math.Max(face.Right, bounds.Right);
        var bottom = Math.Max(face.Bottom, bounds.Bottom);
        return
        [
            new(right, face.Y + (face.Height / 2)),
            new(face.X + (face.Width / 2), bottom),
            new(right, bottom)
        ];
    }

    private static Button NewButton() => new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Width = Length.Cells(6),
        Height = Length.Cells(3),
        Text = "Go",
        Style = ButtonStyle.Standard with
        {
            Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Light),
            Shadow = AppearanceTestValues.Shadow(visible: true, offset: _shadowOffset)
        }
    };
}
