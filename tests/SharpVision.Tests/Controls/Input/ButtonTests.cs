// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies Button ownership, command ordering, activation, layout, and cells.</summary>
public sealed class ButtonTests
{
    /// <summary>Verifies documented defaults and capacity-one content ownership.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var button = new Button();

        button.Text.ShouldBeEmpty();
        button.Command.ShouldBeNull();
        button.CommandParameter.ShouldBeNull();
        button.IsDefault.ShouldBeFalse();
        button.IsCancel.ShouldBeFalse();
        button.TextAlignment.ShouldBe(Alignment.Center);
        button.CanFocus.ShouldBeTrue();
        button.Style.ShouldBeNull();
        button.ActualStyle.Padding.ShouldBe(ButtonStyle.Standard.Padding);
        button.Padding.ShouldBe(default);
        button.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
        button.ActualShadow.IsVisible.ShouldBeFalse();
        button.ActualShadow.Offset.ShouldBe(default);
        button.Text = "Save";
        button.TextControl!.Parent.ShouldBeSameAs(button);
        typeof(Button).GetProperty("Children").ShouldBeNull();
        button.StartAffix.ShouldBeNull();
        button.EndAffix.ShouldBeNull();
    }

    /// <summary>Verifies content alignment rejects unknown values before changing the committed option.</summary>
    [Fact]
    public void TextAlignment_WhenValueIsUnknown_ThrowsBeforeMutation()
    {
        // Arrange
        using var button = new Button();

        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            button.TextAlignment = (Alignment) 99);

        // Assert
        exception.ParamName.ShouldBe("value");
        button.TextAlignment.ShouldBe(Alignment.Center);
    }

    /// <summary>Verifies IsDefault and IsCancel round-trip independently of one another, matching
    /// their separate documented fallback-activation roles.</summary>
    [Fact]
    public void IsDefault_WhenSet_RoundTripsIndependentlyOfIsCancel()
    {
        // Arrange
        using var button = new Button();

        // Act
        button.IsDefault = true;

        // Assert
        button.IsDefault.ShouldBeTrue();
        button.IsCancel.ShouldBeFalse();
    }

    /// <summary>Verifies IsCancel round-trips independently of IsDefault.</summary>
    [Fact]
    public void IsCancel_WhenSet_RoundTripsIndependentlyOfIsDefault()
    {
        // Arrange
        using var button = new Button();

        // Act
        button.IsCancel = true;

        // Assert
        button.IsCancel.ShouldBeTrue();
        button.IsDefault.ShouldBeFalse();
    }

    /// <summary>Verifies a TextAlignment mutation invalidates arrangement, matching its documented
    /// horizontal-placement contract for retained caption content.</summary>
    [Fact]
    public void TextAlignment_WhenChanged_InvalidatesArrange()
    {
        // Arrange
        using var button = new Button("Save");
        button.Clear(Invalidation.All);

        // Act
        button.TextAlignment = Alignment.Start;

        // Assert
        button.TextAlignment.ShouldBe(Alignment.Start);
        button.Pending.ShouldBe(Invalidation.Arrange | Invalidation.Render);
    }

    /// <summary>Verifies a local filled style selects the compact fractional-shadow profile.</summary>
    [Fact]
    public void Style_WhenFilledIsAssigned_UsesCompactFractionalProfile()
    {
        // Arrange and act
        using var button = new Button { Style = ButtonStyle.Filled };

        // Assert
        button.Style.ShouldBe(ButtonStyle.Filled);
        button.ActualStyle.ShouldBe(ButtonStyle.Filled);
        button.ActualStyle.Padding.ShouldBe(new Thickness(horizontal: 2, vertical: 0));
        button.VerticalAlignment.ShouldBe(VerticalAlignment.Stretch);
        button.ActualBorder.Sides.ShouldBe(BorderSide.None);
        button.ActualFace.Background.ShouldBe(Color.Transparent);
        button.ActualShadow.IsVisible.ShouldBeTrue();
        button.ActualShadow.Mode.ShouldBe(ShadowMode.FractionalBlock);
        button.ActualShadow.Offset.ShouldBe(new Point(1, 1));
    }

    /// <summary>Verifies Button keeps its structural default while a Theme supplies its semantic input profile.</summary>
    [Fact]
    public void Style_WhenThemeChanges_UsesSemanticInputAppearanceWithoutReplacingControlStructure()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            palette: "\"inputFace\":\"#0c2238\"",
            inputGlyphStyle: "\"rounded\"",
            inputExtra: """, "face": { "foreground": "inputFace" }"""));
        using var button = new Button();

        button.SetTheme(theme);
        var expected = ButtonStyle.Definition.Resolve(null, theme);

        button.Style.ShouldBeNull();
        button.ActualStyle.Padding.ShouldBe(ButtonStyle.Standard.Padding);
        // The style keeps the theme's SEMANTIC tokens; ActualFace/ActualBorder are the resolved,
        // render-ready appearance and therefore carry concrete colors. Each side is asserted in its
        // own representation - comparing them directly compares across the resolution boundary,
        // which is what this assertion did after this test was ported from a literal-only AppearanceStates.
        expected.Face.Background.ShouldBe(SemanticColor.Control);
        expected.Border.Foreground.ShouldBe(SemanticColor.ControlBorder);
        button.ActualFace.Foreground.Literal.ShouldBe(Color.Rgb(12, 34, 56));
        button.ActualFace.Background.Literal.ShouldBe(theme.ResolveColor(SemanticColor.Control));
        button.ActualBorder.Foreground.Literal.ShouldBe(theme.ResolveColor(SemanticColor.ControlBorder));
        button.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Rounded);

        button.Style = ButtonStyle.Filled;
        button.ActualStyle.ShouldBe(ButtonStyle.Filled);

        button.Style = null;
        button.Style.ShouldBeNull();
        button.ActualStyle.Padding.ShouldBe(ButtonStyle.Standard.Padding);
    }

    /// <summary>Verifies style structure invalidates measurement while color-only changes invalidate rendering.</summary>
    [Fact]
    public void Style_WhenStructureOrColorChanges_InvalidatesTheExactPhase()
    {
        var coloredFace = new Face(
            Color.Rgb(1, 2, 3),
            ButtonStyle.Standard.Face.Background,
            ButtonStyle.Standard.Face.Attributes,
            ButtonStyle.Standard.Face.Underline,
            ButtonStyle.Standard.Face.UnderlineColor);
        var colored = new ButtonStyle(
            coloredFace,
            ButtonStyle.Standard.Border,
            ButtonStyle.Standard.Shadow,
            ButtonStyle.Standard.Padding);
        using var button = new Button { Style = ButtonStyle.Standard };
        button.Clear(Invalidation.All);

        button.Style = colored;

        button.Pending.ShouldBe(Invalidation.Render);
        button.Clear(Invalidation.All);

        button.Style = ButtonStyle.Filled;

        button.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies style padding remains intrinsic when no visible content contributes size.</summary>
    /// <param name="collapsed">Whether a retained content child is collapsed instead of absent.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Measure_WhenContentIsAbsentOrCollapsed_ReservesStylePadding(bool collapsed)
    {
        var style = TestButtonStyles.FlatWithPadding(new Thickness(horizontal: 2, vertical: 1));
        var button = new Button { Style = style };
        if (collapsed)
        {
            button.Text = "content";
            button.TextControl!.Visibility = Visibility.Collapsed;
        }

        new LayoutEngine().Layout(button, new Size(20, 10));

        button.DesiredSize.ShouldBe(new Size(4, 2));
    }

    /// <summary>Verifies a shadow-mode-only style transition invalidates arrangement without measuring again.</summary>
    [Fact]
    public void Style_WhenPressedShadowModeChanges_InvalidatesArrangeWithoutMeasure()
    {
        var composite = TestButtonStyles.WithShadow(AppearanceTestValues.Shadow(
            visible: true,
            mode: ShadowMode.Composite,
            offset: new Point(1, 1)));
        var fractional = TestButtonStyles.WithShadow(AppearanceTestValues.Shadow(
            visible: true,
            mode: ShadowMode.FractionalBlock,
            offset: new Point(1, 1)));
        var button = new Button
        {
            Style = composite,
            Text = "Hi"
        };
        new LayoutEngine().Layout(button, new Size(10, 5));
        button.SetCapabilities(TestCapabilities.WithKeyReleases);
        _ = Router.Route(button, Events.Key, new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(' '),
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press)));
        button.Clear(Invalidation.All);

        button.Style = fractional;

        button.Pending.ShouldBe(Invalidation.Arrange | Invalidation.Render);
    }

    /// <summary>Verifies shadow footprint changes never invalidate Button measurement.</summary>
    [Fact]
    public void Style_WhenShadowVisibilityOrOffsetChanges_InvalidatesArrangeAndRenderOnly()
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
        var button = new Button { Style = hidden };
        button.Clear(Invalidation.All);

        button.Style = visible;

        button.Pending.ShouldBe(Invalidation.Arrange | Invalidation.Render);
        button.Clear(Invalidation.All);

        button.Style = moved;

        button.Pending.ShouldBe(Invalidation.Arrange | Invalidation.Render);
    }

    /// <summary>Verifies shadow modes with identical pressed translation require rendering only.</summary>
    [Fact]
    public void Style_WhenShadowModePreservesPressedTranslation_InvalidatesRenderOnly()
    {
        var composite = TestButtonStyles.WithShadow(AppearanceTestValues.Shadow(
            visible: true,
            mode: ShadowMode.Composite,
            offset: new Point(1, 1)));
        var block = TestButtonStyles.WithShadow(AppearanceTestValues.Shadow(
            visible: true,
            mode: ShadowMode.BlockGlyph,
            offset: new Point(1, 1)));
        var button = new Button { Style = composite };
        button.Clear(Invalidation.All);

        button.Style = block;

        button.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a filled style does not silently replace the caller's layout alignment.</summary>
    [Fact]
    public void Arrange_WhenFilledButtonSharesTallHorizontalRow_PreservesStretchAlignment()
    {
        // Arrange
        var filled = new Button { Style = ButtonStyle.Filled, Text = "Add" };
        var standard = new Button { Text = "Standard" };
        using var row = new Stack
        {
            Orientation = Orientation.Horizontal,
            Children = { filled, standard }
        };

        // Act
        new LayoutEngine().Layout(row, new Size(30, 3));

        // Assert
        standard.Bounds.Height.ShouldBe(3);
        filled.Bounds.ShouldBe(new Rect(0, 0, 7, 3));
    }

    /// <summary>Verifies callers can explicitly center a filled Button within a taller row.</summary>
    [Fact]
    public void Arrange_WhenFilledButtonAlignmentIsCenter_CentersOneRowFace()
    {
        // Arrange
        var filled = new Button
        {
            Style = ButtonStyle.Filled,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "Add"
        };
        var standard = new Button { Text = "Standard" };
        using var row = new Stack
        {
            Orientation = Orientation.Horizontal,
            Children = { filled, standard }
        };

        // Act
        new LayoutEngine().Layout(row, new Size(30, 3));

        // Assert
        standard.Bounds.Height.ShouldBe(3);
        filled.Bounds.ShouldBe(new Rect(0, 1, 7, 1));
    }

    /// <summary>Verifies the default chrome preserves one content cell on every physical edge.</summary>
    [Fact]
    public void Arrange_WhenDefaultChromeIsUsed_PreservesOneCellContentInset()
    {
        var button = new Button { Width = Length.Cells(10), Height = Length.Cells(3), Text = "X" };
        var content = button.TextControl!;
        content.Width = Length.Cells(6);
        content.Height = Length.Cells(1);

        new LayoutEngine().Layout(button, new Size(10, 3));

        button.Bounds.ShouldBe(new Rect(0, 0, 10, 3));
        content.Bounds.ShouldBe(new Rect(2, 1, 6, 1));
    }

    /// <summary>Verifies a Center-aligned face position saturates at int.MaxValue instead of
    /// wrapping negative when the button's own bounds already sit near the integer coordinate
    /// limit, matching the sibling fix applied to Dock/Expander/CheckBox/RadioButton/MenuItem/
    /// NavigationViewGroup arrange arithmetic.</summary>
    [Fact]
    public void Arrange_WhenFaceXIsNearIntMaxValueAndTextIsCentered_SaturatesInsteadOfWrapping()
    {
        var button = new Button
        {
            Style = TestButtonStyles.FlatWithPadding(default),
            TextAlignment = Alignment.Center,
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            Text = "Hi"
        };
        var content = button.TextControl!;
        content.Width = Length.Cells(2);
        content.Height = Length.Cells(1);

        button.Measure(new Constraint(10, 1));
        button.Arrange(new Rect(int.MaxValue - 2, 0, 10, 1));

        button.Bounds.X.ShouldBe(int.MaxValue - 2);
        content.Bounds.X.ShouldBe(int.MaxValue);
        content.Bounds.Width.ShouldBe(2);
    }

    /// <summary>Verifies pressed shadow translation commits content exactly one cell from its released bounds.</summary>
    [Fact]
    public void Arrange_WhenPressedWithShadow_TranslatesContentByShadowOffset()
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(6),
            Height = Length.Cells(3),
            Style = TestButtonStyles.WithShadow(
                AppearanceTestValues.Shadow(visible: true, offset: new Point(1, 1), attributes: TerminalAttributes.Dim)),
            Text = "X"
        };
        var content = button.TextControl!;
        content.Width = Length.Cells(4);
        content.Height = Length.Cells(3);
        new LayoutEngine().Layout(button, new Size(10, 6));
        var released = content.Bounds;

        button.SetCapabilities(TestCapabilities.WithKeyReleases);
        _ = Router.Route(button, Events.Key, new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(' '),
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press)));

        released.ShouldBe(new Rect(1, 0, 4, 3));
        content.Bounds.ShouldBe(new Rect(2, 1, 4, 3));

        new LayoutEngine().Layout(button, new Size(10, 6));

        content.Bounds.ShouldBe(new Rect(2, 1, 4, 3));
    }

    /// <summary>Verifies a fractional shadow collapses in place instead of moving content by a whole cell.</summary>
    [Fact]
    public void Arrange_WhenFilledButtonIsPressed_PreservesContentBounds()
    {
        // Arrange
        using var button = new Button
        {
            Style = ButtonStyle.Filled,
            Width = Length.Cells(8),
            Height = Length.Cells(1),
            Text = "X"
        };
        var content = button.TextControl!;
        content.Width = Length.Cells(4);
        content.Height = Length.Cells(1);
        new LayoutEngine().Layout(button, new Size(9, 2));
        var released = content.Bounds;

        // Act
        button.SetCapabilities(TestCapabilities.WithKeyReleases);
        _ = Router.Route(button, Events.Key, new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(' '),
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press)));

        // Assert — face shifts right by ShadowOffset.X (1) on press
        button.IsPressed.ShouldBeTrue();
        released.ShouldBe(new Rect(2, 0, 4, 1));
        content.Bounds.ShouldBe(new Rect(3, 0, 4, 1));
    }

    /// <summary>Verifies the default Button draws its border without a detached shadow.</summary>
    [Fact]
    public void Render_WhenDefaultStyleIsUsed_DrawsBorderWithoutShadow()
    {
        var button = new Button { Text = "Apply" };
        var size = new Size(9, 5);
        button.Width = Length.Cells(size.Width);
        button.Height = Length.Cells(size.Height);
        new LayoutEngine().Layout(button, size);
        using Frame frame = new(new Size(10, 6));

        button.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("┏");
        FrameOracle.Get(frame, new Point(8, 4)).ShouldBe("┛");
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("A");
        frame.GetCell(new Point(9, 4)).Style.Attributes.ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies a Button can opt into the visible Turbo Vision block shadow mode.</summary>
    [Fact]
    public void Render_WhenBlockShadowIsSelected_DrawsConfiguredShadowGlyphOutsideTheBody()
    {
        var button = new Button
        {
            Text = "Apply",
            Style = TestButtonStyles.WithShadow(
                AppearanceTestValues.Shadow(visible: true, mode: ShadowMode.BlockGlyph, offset: new Point(1, 1), glyph: new Rune('▓'), attributes: TerminalAttributes.Dim)),
            Width = Length.Cells(9),
            Height = Length.Cells(5)
        };
        var size = new Size(9, 5);
        new LayoutEngine().Layout(button, size);
        using Frame frame = new(new Size(10, 6));

        button.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(9, 4)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(8, 4)).ShouldBeEmpty();
    }

    /// <summary>Verifies a held Button shifts its complete face over its own shadow without styling that shadow as hovered or pressed.</summary>
    [Fact]
    public void Render_WhenPressed_MovesFaceIntoShadow()
    {
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
        var size = new Size(10, 6);
        new LayoutEngine().Layout(button, size);
        using Frame released = new(size);
        button.Render(released.Canvas);

        button.SetCapabilities(TestCapabilities.WithKeyReleases);
        _ = Router.Route(button, Events.Key, new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(' '),
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press)));
        using Frame pressed = new(size);
        button.Render(pressed.Canvas);

        button.IsPressed.ShouldBeTrue();
        FrameOracle.Get(released, new Point(2, 0)).ShouldBe("G");
        FrameOracle.Get(pressed, new Point(0, 0)).ShouldBeEmpty();
        FrameOracle.Get(pressed, new Point(3, 1)).ShouldBe("G");
        pressed.GetCell(new Point(6, 1)).Style.Attributes.ShouldNotBe(TerminalAttributes.Dim);
    }

    /// <summary>Verifies a translated pressed face keeps its content outside the arranged hit box.</summary>
    [Fact]
    public void Render_WhenPressedFaceMovesOutsideArrangedBounds_PreservesContentAndHitTarget()
    {
        var button = new Button
        {
            Width = Length.Cells(6),
            Height = Length.Cells(3),
            Style = TestButtonStyles.WithShadow(
                AppearanceTestValues.Shadow(visible: true, offset: new Point(-5, 0))),
            Text = "Go"
        };
        button.Measure(new Constraint(6, 3));
        button.Arrange(new Rect(5, 0, 6, 3));

        button.SetCapabilities(TestCapabilities.WithKeyReleases);
        _ = Router.Route(button, Events.Key, new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(' '),
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press)));
        using Frame frame = new(new Size(11, 3));
        button.Render(frame.Canvas);

        button.IsPressed.ShouldBeTrue();
        button.TextControl!.Bounds.ShouldBe(new Rect(2, 0, 2, 3));
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("G");
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe("o");
        FrameOracle.Get(frame, new Point(5, 2)).ShouldBeEmpty();
        button.HitTest(new Point(0, 0)).ShouldBeNull();
        button.HitTest(new Point(5, 0)).ShouldBeSameAs(button);
    }

    /// <summary>Verifies signed horizontal offsets preserve exactly one leading bottom-shadow gap.</summary>
    [Theory]
    [InlineData(ShadowMode.Composite)]
    [InlineData(ShadowMode.BlockGlyph)]
    public void Render_WhenShadowOffsetIsNegative_PreservesSingleBottomGap(ShadowMode mode)
    {
        var button = new Button
        {
            Bounds = new Rect(2, 0, 4, 2),
            Style = TestButtonStyles.WithShadow(AppearanceTestValues.Shadow(
                mode: mode,
                offset: new Point(-1, 1),
                glyph: new Rune('▓'),
                attributes: TerminalAttributes.Dim))
        };
        using Frame frame = new(new Size(7, 3));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune('x'));

        button.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 2)).ShouldBe("x");
        frame.GetCell(new Point(1, 2)).Style.Attributes.ShouldBe(TerminalAttributes.None);
        frame.GetCell(new Point(2, 2)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe(
            mode == ShadowMode.BlockGlyph ? "▓" : "x");
    }

    /// <summary>Verifies the preserved bottom-shadow gap still tracks the shadow's own clipped
    /// left edge - not the button's unclipped source bounds - when a negative horizontal offset
    /// pushes the shadow's natural position off the left edge of the canvas.</summary>
    [Fact]
    public void Render_WhenNegativeOffsetShadowClipsAtLeftEdge_PreservesGapAtClippedColumn()
    {
        var button = new Button
        {
            Bounds = new Rect(0, 0, 4, 2),
            Style = TestButtonStyles.WithShadow(AppearanceTestValues.Shadow(
                mode: ShadowMode.BlockGlyph,
                offset: new Point(-1, 2),
                glyph: new Rune('▓'),
                attributes: TerminalAttributes.Dim))
        };
        using Frame frame = new(new Size(5, 4));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune('x'));

        button.Render(frame.Canvas);

        // The shadow's natural left edge (Bounds.X + offset.X == -1) clips to canvas column 0,
        // so the preserved gap must land there too, and the shadow strip must begin drawing at
        // column 1 - not at the button's own unshifted left edge.
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("x");
        frame.GetCell(new Point(0, 2)).Style.Attributes.ShouldBe(TerminalAttributes.None);
        FrameOracle.Get(frame, new Point(1, 2)).ShouldBe("▓");
    }

    /// <summary>Verifies a shadowless Button keeps its position while Space presses its direct appearance.</summary>
    [Fact]
    public void Render_WhenPressedWithoutShadow_UsesPressedAppearanceWithoutTranslation()
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(6),
            Height = Length.Cells(3),
            Style = TestButtonStyles.Flat,
            Face = AppearanceTestValues.Face(foreground: ReferenceColors.Get(255), background: ReferenceColors.Get(24)),
            Text = "Go"
        };
        var size = new Size(10, 6);
        new LayoutEngine().Layout(button, size);

        button.SetCapabilities(TestCapabilities.WithKeyReleases);
        _ = Router.Route(button, Events.Key, new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(' '),
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press)));
        using Frame frame = new(size);
        button.Render(frame.Canvas);

        button.IsPressed.ShouldBeTrue();
        button.TextControl!.Bounds.ShouldBe(new Rect(2, 0, 2, 3));
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBeEmpty();
        frame.GetCell(new Point(0, 0)).Style.Background.ShouldBe(ReferenceColors.Get(24));
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("G");
        FrameOracle.Get(frame, new Point(5, 2)).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(6, 3)).ShouldBeEmpty();
    }

    /// <summary>Verifies direct appearance styles the face while the detached shadow remains dim.</summary>
    [Fact]
    public void Render_WhenHovered_DoesNotApplyStateAttributesToShadow()
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(6),
            Height = Length.Cells(3),
            Style = TestButtonStyles.WithShadow(
                AppearanceTestValues.Shadow(visible: true, offset: new Point(1, 1), attributes: TerminalAttributes.Dim)),
        };
        new LayoutEngine().Layout(button, new Size(10, 6));
        using Frame frame = new(new Size(10, 6));

        button.Render(frame.Canvas);

        frame.GetCell(new Point(0, 0)).Style.Attributes.ShouldNotBe(TerminalAttributes.Dim);
        frame.GetCell(new Point(6, 1)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
    }

    /// <summary>Verifies Click observes released state and precedes command execution.</summary>
    [Fact]
    public void PerformClick_WhenCommandCanExecute_RaisesThenExecutesExactlyOnce()
    {
        List<string> order = [];
        var parameter = new object();
        var command = new ProbeCommand { Executing = _ => order.Add("command") };
        var button = new Button { Command = command, CommandParameter = parameter };
        button.Click += (_, eventArgs) =>
        {
            button.IsPressed.ShouldBeFalse();
            eventArgs.Cause.ShouldBe(ActivationCause.Programmatic);
            order.Add("click");
        };

        button.PerformClick();

        order.ShouldBe(["click", "command"]);
        command.Queries.ShouldBe([parameter]);
        command.Executions.ShouldBe([parameter]);
    }

    /// <summary>Verifies false CanExecute suppresses both Click and execution.</summary>
    [Fact]
    public void PerformClick_WhenCommandCannotExecute_DoesNothing()
    {
        var command = new ProbeCommand { CanExecuteValue = false };
        var button = new Button { Command = command };
        var clicks = 0;
        button.Click += (_, _) => clicks++;

        button.PerformClick();

        clicks.ShouldBe(0);
        command.Executions.ShouldBeEmpty();
    }

    /// <summary>Verifies command replacement raises the standard property notification once.</summary>
    [Fact]
    public void Command_WhenReplacementChanges_RaisesPropertyChangedOnce()
    {
        var button = new Button();
        List<string?> names = [];
        button.PropertyChanged += (_, eventArgs) => names.Add(eventArgs.PropertyName);
        var command = new ProbeCommand();

        button.Command = command;
        button.Command = command;

        names.ShouldBe([nameof(Button.Command)]);
    }

    /// <summary>Verifies keyboard activation reaches the public Button event and command.</summary>
    [Fact]
    public void Route_WhenEnterIsPressed_ActivatesButtonWithKeyboardCause()
    {
        var command = new ProbeCommand();
        var button = new Button { Command = command };
        ActivationCause? cause = null;
        button.Click += (_, eventArgs) => cause = eventArgs.Cause;

        _ = Router.Route(
            button,
            Events.Key,
            new KeyEventArgs(new Stroke(
                Code.Enter,
                character: null,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

        cause.ShouldBe(ActivationCause.Keyboard);
        command.Executions.Count.ShouldBe(1);
    }

    /// <summary>Verifies a Click handler that disposes the Button from an Enter-triggered
    /// activation does not throw ObjectDisposedException: the pressed-frame pulse must fully
    /// resolve before activation runs, since the trailing SetPressed(false) that used to follow
    /// activation would otherwise hit an already-disposed control.</summary>
    [Fact]
    public void Route_WhenEnterActivationDisposesButton_DoesNotThrow()
    {
        var button = new Button();
        button.Click += (_, _) => button.Dispose();

        _ = Should.NotThrow(() => Router.Route(
            button,
            Events.Key,
            new KeyEventArgs(new Stroke(
                Code.Enter,
                character: null,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press))));

        button.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies pressed-state callbacks may dispose before Enter completion without stale activation.</summary>
    [Fact]
    public void Route_WhenPressedCallbackDisposesButton_StopsCompletion()
    {
        var button = new Button();
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        button.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Button.IsPressed) && button.IsPressed)
            {
                button.Dispose();
            }
        };

        _ = Should.NotThrow(() => Router.Route(
            button,
            Events.Key,
            new KeyEventArgs(new Stroke(Code.Enter, null, 0, Modifiers.None, KeyAction.Press))));

        button.IsDisposed.ShouldBeTrue();
        clicks.ShouldBe(0);
    }

    /// <summary>Verifies capture-loss callbacks may disable a button before pointer completion activates it.</summary>
    [Fact]
    public async Task Route_WhenCaptureLossDisablesButton_StopsPointerCompletionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var button = new Button { Bounds = new Rect(0, 0, 6, 1) };
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        button.LostPointerCapture += (_, _) => button.IsEnabled = false;

        await dispatcher.InvokeAsync(() =>
        {
            button.Attach(dispatcher);
            using PointerManager pointer = new(button);
            _ = pointer.Dispatch(Pointer(new Point(2, 0), PointerAction.Press));
            _ = Should.NotThrow(() => pointer.Dispatch(Pointer(new Point(2, 0), PointerAction.Release)));
        }, TestContext.Current.CancellationToken);

        button.IsEnabled.ShouldBeFalse();
        clicks.ShouldBe(0);
    }

    /// <summary>Verifies a throwing pressed observer cannot suppress the derived geometry hook.</summary>
    [Fact]
    public void IsPressed_WhenPropertyObserverThrows_StillUpdatesPressedContentGeometry()
    {
        var button = new Button
        {
            Text = "Push",
            Style = TestButtonStyles.WithShadow(
                AppearanceTestValues.Shadow(visible: true, offset: new Point(1, 1), attributes: TerminalAttributes.Dim))
        };
        new LayoutEngine().Layout(button, new Size(10, 3));
        var content = button.TextControl!;
        var released = content.Bounds;
        button.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Button.IsPressed))
            {
                throw new InvalidOperationException("observer failed");
            }
        };

        _ = Should.Throw<InvalidOperationException>(() => button.SetPressed(true));

        button.IsPressed.ShouldBeTrue();
        content.Bounds.ShouldNotBe(released);
    }

    /// <summary>Verifies a Click handler that disposes the Button from a Space-triggered
    /// activation on a terminal that never delivers key releases does not throw
    /// ObjectDisposedException: the press-only-terminal path pulses SetPressed the same way
    /// Enter does and must resolve that pulse before activation runs.</summary>
    [Fact]
    public void Route_WhenSpaceActivationOnPressOnlyTerminalDisposesButton_DoesNotThrow()
    {
        var button = new Button();
        button.Click += (_, _) => button.Dispose();

        _ = Should.NotThrow(() => Router.Route(
            button,
            Events.Key,
            new KeyEventArgs(new Stroke(
                Code.Character,
                new Rune(' '),
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press))));

        button.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies semantic content hit testing still reaches the owning default behavior.</summary>
    [Fact]
    public async Task Dispatch_WhenContentIsPointerTarget_ActivatesOwningButtonAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var button = new Button { Bounds = new Rect(0, 0, 6, 1), Text = "Click" };
        new LayoutEngine().Layout(button, new Size(6, 1));
        var clicks = 0;
        button.Click += (_, _) => clicks++;

        await dispatcher.InvokeAsync(() =>
        {
            button.Attach(dispatcher);
            using PointerManager capture = new(button);
            _ = capture.Dispatch(Pointer(new Point(2, 0), PointerAction.Press));
            _ = capture.Dispatch(Pointer(new Point(2, 0), PointerAction.Release));
        }, TestContext.Current.CancellationToken);

        clicks.ShouldBe(1);
    }

    /// <summary>Verifies passive motion over owned text resolves hover to the semantic Button.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerMovesOverContent_HoversButtonInsteadOfTextAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var button = new Button { Bounds = new Rect(0, 0, 6, 1), Text = "Hover" };
        var content = button.TextControl!;
        new LayoutEngine().Layout(button, new Size(6, 1));

        await dispatcher.InvokeAsync(() =>
        {
            button.Attach(dispatcher);
            using PointerManager capture = new(button);

            _ = capture.Dispatch(Pointer(new Point(2, 0), PointerAction.Move));

            capture.Hovered.ShouldBeSameAs(button);
            button.IsPointerOver.ShouldBeTrue();
            content.IsPointerOver.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies intrinsic border inset, margin, Unicode content, and semantic rendering.</summary>
    [Fact]
    public void Render_WhenButtonHasUnicodeContent_ComputesExactBoundsAndCells()
    {
        var button = new Button { Text = "界" };
        var content = button.TextControl!;
        content.Margin = new Thickness(1, 0);
        new LayoutEngine().Layout(button, new Size(8, 3));
        using Frame frame = new(new Size(8, 3));

        button.Render(frame.Canvas);

        button.DesiredSize.ShouldBe(new Size(8, 3));
        content.Bounds.ShouldBe(new Rect(3, 1, 2, 1));
        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("界");
        frame.GetCell(new Point(4, 1)).Continuation.ShouldBeTrue();
    }

    /// <summary>Verifies a Button with direct colors owns the complete visible surface behind its content.</summary>
    [Fact]
    public void Render_WhenStyleDefinesBackground_FillsButtonBounds()
    {
        var button = new Button
        {
            Text = "Run",
            Style = TestButtonStyles.Flat,
            Face = AppearanceTestValues.Face(foreground: ReferenceColors.Get(255), background: ReferenceColors.Get(24)),
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };
        var size = new Size(8, 3);
        new LayoutEngine().Layout(button, size);
        using Frame frame = new(size);

        button.Render(frame.Canvas);

        frame.GetCell(new Point(0, 0)).Style.Background.ShouldBe(ReferenceColors.Get(24));
        frame.GetCell(new Point(7, 2)).Style.Background.ShouldBe(ReferenceColors.Get(24));
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("R");
    }

    /// <summary>Verifies unavailable controls reject programmatic activation.</summary>
    [Theory]
    [InlineData(false, Visibility.Visible)]
    [InlineData(true, Visibility.Hidden)]
    public void PerformClick_WhenButtonIsUnavailable_DoesNothing(bool enabled, Visibility visibility)
    {
        var button = new Button { IsEnabled = enabled, Visibility = visibility };
        var clicks = 0;
        button.Click += (_, _) => clicks++;

        button.PerformClick();

        clicks.ShouldBe(0);
    }

    /// <summary>Verifies the string constructor sets text content on a standard-kind Button.</summary>
    [Fact]
    public void Constructor_WithText_SetsTextContent()
    {
        var button = new Button("Save");
        button.Text.ShouldBe("Save");
        button.TextControl.ShouldNotBeNull().Content.ShouldBe("Save");
    }

    /// <summary>Verifies the string constructor rejects null text.</summary>
    [Fact]
    public void Constructor_WithNullText_Throws() =>
        _ = Should.Throw<ArgumentNullException>(() => new Button(null!));

    /// <summary>Verifies disposing the button prevents mutation.</summary>
    [Fact]
    public void Dispose_WhenCalled_PreventsMutation()
    {
        // Arrange
        var button = new Button
        {
            Command = new ProbeCommand()
        };

        // Act
        button.Dispose();

        // Assert
        _ = Should.Throw<ObjectDisposedException>(() => button.Text = "late");
    }

    /// <summary>Verifies setting the same command reference is a no-op.</summary>
    [Fact]
    public void Command_WhenSetToSameInstance_IsNoOp()
    {
        // Arrange
        var command = new ProbeCommand();
        var button = new Button
        {
            Command = command
        };
        var raised = 0;
        button.PropertyChanged += (_, _) => raised++;

        // Act
        button.Command = command;

        // Assert
        raised.ShouldBe(0);
    }

    /// <summary>Verifies an explicit Button style exposes its resolved border glyph family.</summary>
    [Fact]
    public void Glyphs_WhenSet_UpdatesBorderGlyphs()
    {
        // Arrange
        var button = new Button
        {
            Style = TestButtonStyles.WithBorder(
                AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Heavy))
        };

        // Assert
        button.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
    }

    /// <summary>Verifies PerformClick rejects use after disposal.</summary>
    [Fact]
    public void PerformClick_WhenDisposed_Throws()
    {
        // Arrange
        var button = new Button();
        button.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(button.PerformClick);
    }

    /// <summary>Verifies PerformClick on a disabled button does nothing.</summary>
    [Fact]
    public void PerformClick_WhenDisabled_IsNoOp()
    {
        // Arrange
        var button = new Button
        {
            IsEnabled = false
        };
        var raised = 0;
        button.Click += (_, _) => raised++;

        // Act
        button.PerformClick();

        // Assert
        raised.ShouldBe(0);
    }

    /// <summary>Verifies desired width grows by exactly one reserved column per set affix, plus
    /// the shared theme gap, over an equivalent captionless Button with neither set.</summary>
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 4)]
    public void Measure_WhenAffixesAreSet_ReservesCellsPerAffixPlusGap(
        bool hasStart,
        bool hasEnd,
        int expectedExtraWidth)
    {
        var button = new Button
        {
            Style = TestButtonStyles.FlatWithPadding(default),
            StartAffix = hasStart ? new Affix("!") : null,
            EndAffix = hasEnd ? new Affix("!") : null
        };

        new LayoutEngine().Layout(button, new Size(20, 3));

        button.DesiredSize.Width.ShouldBe(expectedExtraWidth);
    }

    /// <summary>Verifies null-to-set and set-to-null affix assignment requires Measure.</summary>
    [Fact]
    public void StartAffix_WhenAssignedOrCleared_InvalidatesMeasure()
    {
        using var button = new Button("Save");
        button.Clear(Invalidation.All);

        button.StartAffix = new Affix("!");

        button.Pending.ShouldBe(Invalidation.All);
        button.Clear(Invalidation.All);

        button.StartAffix = null;

        button.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a same-resolved-width content or color swap invalidates rendering only,
    /// the exact grading an animated affix (a spinner swapping frames) depends on.</summary>
    [Fact]
    public void StartAffix_WhenContentOrColorChangesAtTheSameResolvedWidth_InvalidatesRenderOnly()
    {
        using var button = new Button("Save") { StartAffix = new Affix("|") };
        button.Clear(Invalidation.All);

        button.StartAffix = new Affix("/");

        button.Pending.ShouldBe(Invalidation.Render);
        button.Clear(Invalidation.All);

        button.StartAffix = new Affix("/", "?", SemanticColor.Warning);

        button.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a resolved-width change (one cell to two cells) invalidates Measure again,
    /// not just Render, even though both values are non-null.</summary>
    [Fact]
    public void EndAffix_WhenResolvedWidthChanges_InvalidatesMeasure()
    {
        using var button = new Button("Save") { EndAffix = new Affix("!") };
        button.Clear(Invalidation.All);

        // U+4E16 '世' is a wide CJK ideograph (two cells wide), unlike the one-cell '!' above.
        button.EndAffix = new Affix("世");

        button.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies reassigning the identical affix value is a no-op, matching every other
    /// SetProperty-backed member.</summary>
    [Fact]
    public void StartAffix_WhenReassignedTheSameValue_DoesNotInvalidate()
    {
        var affix = new Affix("!");
        using var button = new Button("Save") { StartAffix = affix };
        button.Clear(Invalidation.All);

        button.StartAffix = affix;

        button.Pending.ShouldBe(Invalidation.None);
    }

    private static Pointer Pointer(Point cells, PointerAction action) => new(
        cells,
        pixels: null,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false);

}
