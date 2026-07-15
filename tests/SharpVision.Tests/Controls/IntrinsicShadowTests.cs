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
            control.ShadowMode = (ShadowMode) 99);
        _ = Should.Throw<ArgumentException>(() =>
            control.ShadowGlyph = new Rune('界'));

        control.ShadowMode.ShouldBe(ShadowMode.Composite);
        control.ShadowGlyph.ShouldBe(new Rune('▓'));
    }

    /// <summary>Verifies intrinsic shadow overflow does not change desired size or the child slot.</summary>
    [Fact]
    public void Layout_WhenChildIsPresent_DoesNotReserveShadowOffset()
    {
        var child = new ProbeControl(new Size(3, 2));
        var control = CreateSurface(ShadowMode.Composite, new Point(2, 1));
        control.Children.Add(child);

        new Engine().Layout(control, new Size(3, 2));

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
        frame.GetCell(new Point(3, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
    }

    /// <summary>Verifies transparent composite mode preserves glyphs and backgrounds while applying shadow attributes.</summary>
    [Fact]
    public void Render_WhenModeIsComposite_PreservesUnderlyingGlyphsAndBackgrounds()
    {
        var control = CreateArranged(ShadowMode.Composite, new Point(2, 1));
        var destinationBackground = Color.Indexed(7);
        using Frame frame = new(new Size(5, 3));
        frame.Canvas.Fill(
            frame.Canvas.Bounds,
            new Rune('x'),
            new TerminalStyle(Color.Default, destinationBackground));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("x");
        frame.GetCell(new Point(3, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
        frame.GetCell(new Point(3, 1)).Style.Background.ShouldBe(destinationBackground);
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("x");
        frame.GetCell(new Point(2, 1)).Style.Attributes.ShouldBe(Attributes.None);
        frame.GetCell(new Point(2, 1)).Style.Background.ShouldBe(destinationBackground);
    }

    /// <summary>Verifies ordinary chrome fills the body separately and applies the explicit shadow background outside it.</summary>
    [Fact]
    public void Render_WhenShadowBackgroundIsSet_SeparatesBodyAndShadowStyles()
    {
        var control = CreateArranged(ShadowMode.Composite, new Point(2, 1));
        control.Background = Color.Indexed(1);
        control.ShadowBackground = Color.Indexed(4);
        using Frame frame = new(new Size(5, 3));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune('x'));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBeEmpty();
        frame.GetCell(new Point(0, 0)).Style.Background.ShouldBe(Color.Indexed(1));
        frame.GetCell(new Point(0, 0)).Style.Attributes.ShouldBe(Attributes.None);
        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("x");
        frame.GetCell(new Point(3, 1)).Style.Background.ShouldBe(Color.Indexed(4));
        frame.GetCell(new Point(3, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
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
        frame.GetCell(new Point(4, 1)).IsContinuation.ShouldBeTrue();
        frame.GetCell(new Point(3, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
        frame.GetCell(new Point(4, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
        frame.GetCell(new Point(3, 1)).Style.Background.ShouldBe(Color.Default);
        frame.GetCell(new Point(4, 1)).Style.Background.ShouldBe(Color.Default);
    }

    /// <summary>Verifies negative offsets draw above and left without expanding hit targets.</summary>
    [Fact]
    public void Render_WhenOffsetIsNegative_DrawsVisualOverflowWithoutHitTarget()
    {
        var control = CreateSurface(ShadowMode.BlockGlyph, new Point(-1, -1));
        control.Children.Add(new ProbeControl(new Size(3, 2)));
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

    #endregion

    private static LayoutProbe CreateArranged(ShadowMode mode, Point offset)
    {
        var control = CreateSurface(mode, offset);
        control.Children.Add(new ProbeControl(new Size(3, 2)));
        new Engine().Layout(control, new Size(3, 2));
        return control;
    }

    private static LayoutProbe CreateSurface(ShadowMode mode, Point offset) => new()
    {
        HasShadow = true,
        ShadowMode = mode,
        ShadowOffset = offset,
        ShadowGlyph = new Rune('▓'),
        ShadowAttributes = Attributes.Dim,
    };
}
