// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;




/// <summary>Verifies intrinsic shadow layout, overflow, clipping, and compositing.</summary>
public sealed class IntrinsicShadowTests
{
    #region Contract

    /// <summary>Verifies unknown modes and non-cell glyphs fail before mutation.</summary>
    [Fact]
    public void Properties_WhenValueIsInvalid_ThrowBeforeMutation()
    {
        LayoutProbe control = new() { HasShadow = true };

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            control.ShadowMode = (ShadowMode) 99);
        _ = Should.Throw<ArgumentException>(() => control.ShadowGlyph = new Rune('界'));

        control.ShadowMode.ShouldBe(ShadowMode.Composite);
        control.ShadowGlyph.ShouldBe(new Rune('▓'));
    }

    /// <summary>Verifies intrinsic shadows do not reserve their visual overflow during layout.</summary>
    [Fact]
    public void Layout_WhenChildIsPresent_DoesNotReserveShadowOffset()
    {
        ProbeControl child = new(new Size(3, 2));
        LayoutProbe control = new()
        {
            HasShadow = true,
            ShadowMode = ShadowMode.Composite,
            ShadowOffset = new Point(2, 1),
            ShadowAttributes = Attributes.Dim,
        };
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
        LayoutProbe control = CreateArranged(ShadowMode.BlockGlyph, new Point(2, 1));
        using Frame frame = new(new Size(5, 3));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(4, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(3, 2)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(4, 2)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe(string.Empty);
    }

    /// <summary>Verifies composite mode preserves glyphs and changes only their shadow style.</summary>
    [Fact]
    public void Render_WhenModeIsComposite_PreservesUnderlyingGlyphs()
    {
        LayoutProbe control = CreateArranged(ShadowMode.Composite, new Point(2, 1));
        control.ShadowBackground = Color.Indexed(4);
        using Frame frame = new(new Size(5, 3));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune('x'));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("x");
        frame.GetCell(new Point(3, 1)).Style.Background.ShouldBe(Color.Indexed(4));
        frame.GetCell(new Point(2, 1)).Style.ShouldBe(CellStyle.Default);
    }

    /// <summary>Verifies a generic background supplies an opaque composite shadow fallback.</summary>
    [Fact]
    public void Render_WhenShadowBackgroundIsNull_UsesGenericBackground()
    {
        LayoutProbe control = CreateArranged(ShadowMode.Composite, new Point(2, 1));
        control.Background = Color.Indexed(4);
        using Frame frame = new(new Size(5, 3));
        frame.Canvas.Fill(
            frame.Canvas.Bounds,
            new Rune('x'),
            new CellStyle(Color.Default, Color.Indexed(238)));

        control.Render(frame.Canvas);

        control.ShadowBackground.ShouldBeNull();
        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("x");
        frame.GetCell(new Point(3, 1)).Style.Background.Kind.ShouldBe(ColorKind.Indexed);
        frame.GetCell(new Point(3, 1)).Style.Background.Red.ShouldBe((byte) 4);
        frame.GetCell(new Point(3, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
    }

    /// <summary>Verifies composite mode restyles a complete wide owner.</summary>
    [Fact]
    public void Render_WhenShadowTouchesWideGlyph_StylesCompleteOwner()
    {
        LayoutProbe control = CreateArranged(ShadowMode.Composite, new Point(2, 1));
        control.ShadowBackground = Color.Indexed(4);
        using Frame frame = new(new Size(5, 3));
        _ = frame.Canvas.Draw("界", new Point(3, 1));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("界");
        frame.GetCell(new Point(3, 1)).Style.Background.ShouldBe(Color.Indexed(4));
        frame.GetCell(new Point(4, 1)).Style.Background.ShouldBe(Color.Indexed(4));
    }

    /// <summary>Verifies negative offsets draw above and left without changing hit testing.</summary>
    [Fact]
    public void Render_WhenOffsetIsNegative_DrawsVisualOverflowWithoutHitTarget()
    {
        LayoutProbe control = new()
        {
            HasShadow = true,
            ShadowMode = ShadowMode.BlockGlyph,
            ShadowOffset = new Point(-1, -1),
            ShadowAttributes = Attributes.Dim,
        };
        control.Children.Add(new ProbeControl(new Size(3, 2)));
        control.Measure(new Constraint(3, 2));
        control.Arrange(new Rect(1, 1, 3, 2));
        using Frame frame = new(new Size(4, 3));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("▓");
        control.HitTest(new Point(0, 0)).ShouldBeNull();
    }

    /// <summary>Verifies ancestor clipping contains visual overflow.</summary>
    [Fact]
    public void Render_WhenAncestorCanvasClipsShadow_DoesNotEscapeClip()
    {
        LayoutProbe control = CreateArranged(ShadowMode.BlockGlyph, new Point(2, 1));
        using Frame frame = new(new Size(5, 3));

        control.Render(frame.Canvas.Clip(new Rect(0, 0, 4, 3)));

        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("▓");
        frame.GetCell(new Point(4, 1)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(4, 2)).ShouldBe(CellInfo.Blank);
    }

    #endregion

    private static LayoutProbe CreateArranged(ShadowMode mode, Point offset)
    {
        LayoutProbe control = new()
        {
            HasShadow = true,
            ShadowMode = mode,
            ShadowOffset = offset,
            ShadowAttributes = Attributes.Dim,
        };
        control.Children.Add(new ProbeControl(new Size(3, 2)));
        new Engine().Layout(control, new Size(3, 2));
        return control;
    }
}
