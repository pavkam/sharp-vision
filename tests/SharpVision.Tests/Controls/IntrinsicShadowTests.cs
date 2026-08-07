// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies intrinsic shadow validation, layout, overflow, clipping, and compositing.</summary>
public sealed class IntrinsicShadowTests
{
    #region Contract

    /// <summary>Verifies unknown modes and non-cell glyphs fail before mutation.</summary>
    [Fact]
    public void Properties_WhenValueIsInvalid_ThrowBeforeMutation()
    {
        var control = CreateSurface(ShadowMode.Composite, new Point(2, 1));

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            control.Shadow = AppearanceTestValues.Shadow(mode: (ShadowMode) 99));
        _ = Should.Throw<ArgumentException>(() =>
            control.Shadow = AppearanceTestValues.Shadow(glyph: new Rune('界')));

        control.Shadow.Mode.ShouldBe(ShadowMode.Composite);
        control.Shadow.Glyph.ShouldBe(new Rune('▓'));
    }

    /// <summary>Verifies intrinsic shadow overflow does not change desired size or the child slot.</summary>
    [Fact]
    public void Layout_WhenChildIsPresent_DoesNotReserveShadowOffset()
    {
        var child = new ProbeControl(new Size(3, 2));
        var control = CreateSurface(ShadowMode.Composite, new Point(2, 1));
        control.Children.Add(child);

        new LayoutEngine().Layout(control, new Size(3, 2));

        control.DesiredSize.ShouldBe(new Size(3, 2));
        control.Bounds.ShouldBe(new Rect(0, 0, 3, 2));
        child.Bounds.ShouldBe(control.Bounds);
    }

    #endregion

    #region Rendering

    /// <summary>Verifies block mode draws only the shifted non-overlapping footprint.</summary>
    [Fact]
    public void Render_WhenModeIsBlockGlyph_DrawsTurboVisionFootprint()
    {
        var control = CreateArranged(ShadowMode.BlockGlyph, new Point(2, 1));
        using Frame frame = new(new Size(5, 3));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(4, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(3, 2)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(4, 2)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBeEmpty();
        frame.GetCell(new Point(3, 1)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
    }

    /// <summary>Verifies fractional mode sculpts the canonical lower-right half-cell footprint.</summary>
    [Fact]
    public void Render_WhenModeIsFractionalBlock_DrawsSculptedFootprint()
    {
        // Arrange
        var control = CreateSurface(ShadowMode.FractionalBlock, new Point(1, 1));
        control.Bounds = new Rect(0, 0, 4, 1);
        using Frame frame = new(new Size(5, 2));

        // Act
        control.Render(frame.Canvas);

        // Assert
        FrameOracle.Get(frame, new Point(4, 0)).ShouldBe("▄");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("▀");
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("▀");
        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("▀");
        FrameOracle.Get(frame, new Point(4, 1)).ShouldBe("▀");
        frame.GetCell(new Point(4, 0)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
        frame.GetCell(new Point(4, 1)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
    }

    /// <summary>Verifies a negative half-row offset mirrors the fractional vertical boundary.</summary>
    [Fact]
    public void Render_WhenFractionalOffsetIsNegative_MirrorsVerticalCaps()
    {
        // Arrange
        var control = CreateSurface(ShadowMode.FractionalBlock, new Point(1, -1));
        control.Bounds = new Rect(0, 1, 4, 1);
        using Frame frame = new(new Size(5, 2));

        // Act
        control.Render(frame.Canvas);

        // Assert
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("▄");
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe("▄");
        FrameOracle.Get(frame, new Point(4, 0)).ShouldBe("▄");
        FrameOracle.Get(frame, new Point(4, 1)).ShouldBe("▀");
    }

    /// <summary>Verifies fractional half-block shadow cells use transparent background, not the shadow color.</summary>
    [Fact]
    public void Render_WhenModeIsFractionalBlock_UsesTransparentBackgroundForHalfBlocks()
    {
        // Arrange
        var control = CreateSurface(ShadowMode.FractionalBlock, new Point(1, 1));
        control.Shadow = AppearanceTestValues.Shadow(
            mode: control.Shadow.Mode,
            offset: control.Shadow.Offset,
            glyph: control.Shadow.Glyph,
            foreground: ReferenceColors.Get(5),
            attributes: control.Shadow.Attributes);
        control.Bounds = new Rect(0, 0, 4, 1);
        using Frame frame = new(new Size(5, 2));
        var existingBackground = ReferenceColors.Get(7);
        frame.Canvas.Fill(
            frame.Canvas.Bounds,
            new Rune(' '),
            new TerminalStyle(Color.Default, existingBackground));

        // Act
        control.Render(frame.Canvas);

        // Assert: half-block glyphs preserve the destination background (transparent mode)
        FrameOracle.Get(frame, new Point(4, 0)).ShouldBe("▄");
        frame.GetCell(new Point(4, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(5));
        frame.GetCell(new Point(4, 0)).Style.Background.ShouldBe(existingBackground);
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("▀");
        frame.GetCell(new Point(1, 1)).Style.Foreground.ShouldBe(ReferenceColors.Get(5));
        frame.GetCell(new Point(1, 1)).Style.Background.ShouldBe(existingBackground);
        FrameOracle.Get(frame, new Point(4, 1)).ShouldBe("▀");
        frame.GetCell(new Point(4, 1)).Style.Foreground.ShouldBe(ReferenceColors.Get(5));
        frame.GetCell(new Point(4, 1)).Style.Background.ShouldBe(existingBackground);
    }

    /// <summary>Verifies a zero fractional offset produces no detached footprint.</summary>
    [Fact]
    public void Render_WhenFractionalOffsetIsZero_DrawsNoShadow()
    {
        // Arrange
        var control = CreateSurface(ShadowMode.FractionalBlock, default);
        control.Bounds = new Rect(0, 0, 4, 1);
        using Frame frame = new(new Size(5, 2));

        // Act
        control.Render(frame.Canvas);

        // Assert
        FrameOracle.Get(frame, new Point(4, 0)).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBeEmpty();
        frame.GetCell(new Point(4, 0)).Style.Attributes.ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies fractional glyph replacement repairs a wide destination owner.</summary>
    [Fact]
    public void Render_WhenFractionalShadowReplacesWideGlyph_RepairsContinuation()
    {
        // Arrange
        var control = CreateSurface(ShadowMode.FractionalBlock, new Point(1, 1));
        control.Bounds = new Rect(0, 0, 1, 1);
        using Frame frame = new(new Size(3, 2));
        _ = frame.Canvas.Draw("界", new Point(1, 0));

        // Act
        control.Render(frame.Canvas);

        // Assert
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("▄");
        frame.GetCell(new Point(2, 0)).Continuation.ShouldBeFalse();
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBeEmpty();
    }

    /// <summary>Verifies transparent composite mode preserves glyphs and backgrounds while applying shadow attributes.</summary>
    [Fact]
    public void Render_WhenModeIsComposite_PreservesUnderlyingGlyphsAndBackgrounds()
    {
        var control = CreateArranged(ShadowMode.Composite, new Point(2, 1));
        var destinationBackground = ReferenceColors.Get(7);
        using Frame frame = new(new Size(5, 3));
        frame.Canvas.Fill(
            frame.Canvas.Bounds,
            new Rune('x'),
            new TerminalStyle(Color.Default, destinationBackground));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("x");
        frame.GetCell(new Point(3, 1)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
        frame.GetCell(new Point(3, 1)).Style.Background.ShouldBe(destinationBackground);
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("x");
        frame.GetCell(new Point(2, 1)).Style.Attributes.ShouldBe(TerminalAttributes.None);
        frame.GetCell(new Point(2, 1)).Style.Background.ShouldBe(destinationBackground);
    }

    /// <summary>Verifies ordinary chrome fills the body separately and applies the explicit shadow background outside it.</summary>
    [Fact]
    public void Render_WhenShadowBackgroundMemberIsSet_SeparatesBodyAndShadowStyles()
    {
        var control = CreateArranged(ShadowMode.Composite, new Point(2, 1));
        control.Face = AppearanceTestValues.Face(background: ReferenceColors.Get(1));
        control.Shadow = AppearanceTestValues.Shadow(
            mode: control.Shadow.Mode,
            offset: control.Shadow.Offset,
            glyph: control.Shadow.Glyph,
            foreground: ReferenceColors.Get(3),
            background: ReferenceColors.Get(4),
            attributes: control.Shadow.Attributes);
        using Frame frame = new(new Size(5, 3));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune('x'));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBeEmpty();
        frame.GetCell(new Point(0, 0)).Style.Background.ShouldBe(ReferenceColors.Get(1));
        frame.GetCell(new Point(0, 0)).Style.Attributes.ShouldBe(TerminalAttributes.None);
        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("x");
        frame.GetCell(new Point(3, 1)).Style.Foreground.ShouldBe(ReferenceColors.Get(3));
        frame.GetCell(new Point(3, 1)).Style.Background.ShouldBe(ReferenceColors.Get(4));
        frame.GetCell(new Point(3, 1)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
    }

    /// <summary>Verifies composite mode restyles a complete wide grapheme owner.</summary>
    [Fact]
    public void Render_WhenShadowTouchesWideGlyph_StylesCompleteOwner()
    {
        var control = CreateArranged(ShadowMode.Composite, new Point(2, 1));
        using Frame frame = new(new Size(5, 3));
        _ = frame.Canvas.Draw("界", new Point(3, 1));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("界");
        frame.GetCell(new Point(4, 1)).Continuation.ShouldBeTrue();
        frame.GetCell(new Point(3, 1)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
        frame.GetCell(new Point(4, 1)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
        frame.GetCell(new Point(3, 1)).Style.Background.ShouldBe(Color.Default);
        frame.GetCell(new Point(4, 1)).Style.Background.ShouldBe(Color.Default);
    }

    /// <summary>Verifies negative offsets draw above and left without expanding hit targets.</summary>
    [Fact]
    public void Render_WhenOffsetIsNegative_DrawsVisualOverflowWithoutHitTarget()
    {
        var control = CreateSurface(ShadowMode.BlockGlyph, new Point(-1, -1));
        control.Children.Add(new ProbeControl(new Size(3, 2))
        {
            Face = AppearanceTestValues.Face(background: Color.Transparent)
        });
        control.Measure(new Constraint(3, 2));
        control.Arrange(new Rect(1, 1, 3, 2));
        using Frame frame = new(new Size(4, 3));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("▓");
        control.HitTest(new Point(0, 0)).ShouldBeNull();
        control.HitTest(new Point(0, 1)).ShouldBeNull();
    }

    /// <summary>Verifies an ancestor canvas clip contains intrinsic visual overflow.</summary>
    [Fact]
    public void Render_WhenAncestorCanvasClipsShadow_DoesNotEscapeClip()
    {
        var control = CreateArranged(ShadowMode.BlockGlyph, new Point(2, 1));
        using Frame frame = new(new Size(5, 3));

        control.Render(frame.Canvas.Clip(new Rect(0, 0, 4, 3)));

        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("▓");
        frame.GetCell(new Point(4, 1)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(4, 2)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>Verifies positive shadow overflow survives every ordinary clipping ancestor.</summary>
    [Fact]
    public void Render_WhenShadowIsNestedThroughClippingAncestors_DrawsCompletePositiveFootprint()
    {
        var (root, shadowed) = CreateNestedSurface(ShadowMode.BlockGlyph, new Point(2, 1));
        new LayoutEngine().Layout(root, new Size(3, 2));
        using Frame frame = new(new Size(5, 3));

        root.Render(frame.Canvas);

        shadowed.Bounds.ShouldBe(new Rect(0, 0, 3, 2));
        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(4, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(3, 2)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(4, 2)).ShouldBe("▓");
    }

    /// <summary>Verifies negative shadow overflow survives ancestors without enlarging hit targets.</summary>
    [Fact]
    public void Render_WhenNestedShadowOffsetIsNegative_DrawsCompleteFootprintWithoutHitTarget()
    {
        var (root, shadowed) = CreateNestedSurface(ShadowMode.BlockGlyph, new Point(-1, -1));
        root.Measure(new Constraint(3, 2));
        root.Arrange(new Rect(1, 1, 3, 2));
        using Frame frame = new(new Size(4, 3));

        root.Render(frame.Canvas);

        shadowed.Bounds.ShouldBe(new Rect(1, 1, 3, 2));
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("▓");
        shadowed.HitTest(new Point(0, 1)).ShouldBeNull();
    }

    /// <summary>Verifies Overlay's explicit clip policy remains authoritative for nested shadows.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Render_WhenNestedShadowCrossesOverlayBoundary_RespectsClipToBounds(
        bool clipToBounds,
        bool expectedShadow)
    {
        var shadowed = CreateSurface(ShadowMode.BlockGlyph, new Point(2, 1));
        shadowed.Children.Add(new ProbeControl(new Size(3, 2))
        {
            Face = AppearanceTestValues.Face(background: Color.Transparent)
        });
        var middle = new LayoutProbe { Children = { shadowed } };
        var overlay = new Overlay
        {
            ClipToBounds = clipToBounds,
            Children = { middle }
        };
        new LayoutEngine().Layout(overlay, new Size(3, 2));
        using Frame frame = new(new Size(5, 3));

        overlay.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe(expectedShadow ? "▓" : string.Empty);
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe(expectedShadow ? "▓" : string.Empty);
    }

    /// <summary>Verifies one child's visual overflow never exposes an unrelated clipped sibling.</summary>
    [Fact]
    public void Render_WhenChildShadowExpandsVisualClip_DoesNotExposeOutsideSibling()
    {
        var shadowed = CreateSurface(ShadowMode.BlockGlyph, new Point(1, 0));
        shadowed.Bounds = new Rect(0, 0, 1, 1);
        var outside = new ProbeControl
        {
            Bounds = new Rect(1, 0, 1, 1),
            Content = "X".AsMemory()
        };
        var parent = new ProbeContainer { Bounds = new Rect(0, 0, 1, 1) };
        parent.Children.Add(shadowed);
        parent.Children.Add(outside);
        using Frame frame = new(new Size(2, 1));

        parent.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("▓");
        parent.HitTest(new Point(1, 0)).ShouldBeNull();
    }

    /// <summary>Verifies an unclipped middle owner retains its ancestor aperture for descendant overflow.</summary>
    [Fact]
    public void Render_WhenMiddleOwnerDoesNotClip_PropagatesDescendantShadowInsideAncestorCanvas()
    {
        var shadowed = CreateSurface(ShadowMode.BlockGlyph, new Point(1, 0));
        shadowed.Bounds = new Rect(3, 0, 1, 1);
        var middle = new ProbeContainer
        {
            Bounds = new Rect(0, 0, 1, 1),
            ClipChildren = false,
            Children = { shadowed }
        };
        var root = new ProbeContainer
        {
            Bounds = new Rect(0, 0, 4, 1),
            Children = { middle }
        };
        using Frame frame = new(new Size(5, 1));

        root.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(4, 0)).ShouldBe("▓");
    }

    /// <summary>Verifies a nested shadow still obeys the caller's finite canvas clip.</summary>
    [Fact]
    public void Render_WhenNestedShadowMeetsCallerCanvasClip_DoesNotEscapeClip()
    {
        var (root, _) = CreateNestedSurface(ShadowMode.BlockGlyph, new Point(2, 1));
        new LayoutEngine().Layout(root, new Size(3, 2));
        using Frame frame = new(new Size(5, 3));

        root.Render(frame.Canvas.Clip(new Rect(0, 0, 4, 3)));

        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("▓");
        frame.GetCell(new Point(4, 1)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(4, 2)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>Verifies an unrepresentable extreme shadow cannot hide or fail the arranged body.</summary>
    [Theory]
    [InlineData(ShadowMode.BlockGlyph)]
    [InlineData(ShadowMode.FractionalBlock)]
    public void Render_WhenShadowOffsetIsExtreme_PreservesBodyWithoutThrowing(ShadowMode mode)
    {
        var control = CreateArranged(
            mode,
            new Point(int.MinValue, int.MinValue));
        control.Face = AppearanceTestValues.Face(background: ReferenceColors.Get(1));
        using Frame frame = new(new Size(3, 2));

        control.Render(frame.Canvas);

        frame.GetCell(default).Style.Background.ShouldBe(ReferenceColors.Get(1));
        frame.GetCell(new Point(2, 1)).Style.Background.ShouldBe(ReferenceColors.Get(1));
    }

    #endregion

    private static LayoutProbe CreateArranged(ShadowMode mode, Point offset)
    {
        var control = CreateSurface(mode, offset);
        control.Children.Add(new ProbeControl(new Size(3, 2))
        {
            Face = AppearanceTestValues.Face(background: Color.Transparent)
        });
        new LayoutEngine().Layout(control, new Size(3, 2));
        return control;
    }

    private static (LayoutProbe Root, LayoutProbe Shadowed) CreateNestedSurface(
        ShadowMode mode,
        Point offset)
    {
        var shadowed = CreateSurface(mode, offset);
        shadowed.Children.Add(new ProbeControl(new Size(3, 2))
        {
            Face = AppearanceTestValues.Face(background: Color.Transparent)
        });
        var middle = new LayoutProbe { Children = { shadowed } };
        var root = new LayoutProbe { Children = { middle } };
        return (root, shadowed);
    }

    private static LayoutProbe CreateSurface(ShadowMode mode, Point offset) => new()
    {
        Face = AppearanceTestValues.Face(background: Color.Transparent),
        Shadow = AppearanceTestValues.Shadow(visible: true, mode: mode, offset: offset, glyph: new Rune('▓'), attributes: TerminalAttributes.Dim),
    };
}
