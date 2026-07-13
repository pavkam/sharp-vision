// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;




/// <summary>Verifies Shadow ownership, layout, overflow, clipping, and compositing.</summary>
public sealed class ShadowTests
{
    #region Contract

    /// <summary>Verifies documented Turbo Vision-compatible defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        Shadow shadow = new();

        shadow.Child.ShouldBeNull();
        shadow.Mode.ShouldBe(ShadowMode.Composite);
        shadow.Offset.ShouldBe(new Point(2, 1));
        shadow.Glyph.ShouldBe(new Rune('▓'));
        shadow.Children.Count.ShouldBe(0);
    }

    /// <summary>Verifies unknown modes and non-cell glyphs fail before mutation.</summary>
    [Fact]
    public void Properties_WhenValueIsInvalid_ThrowBeforeMutation()
    {
        Shadow shadow = new();

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            shadow.Mode = (ShadowMode) 99);
        _ = Should.Throw<ArgumentException>(() => shadow.Glyph = new Rune('界'));

        shadow.Mode.ShouldBe(ShadowMode.Composite);
        shadow.Glyph.ShouldBe(new Rune('▓'));
    }

    /// <summary>Verifies the decorator owns exactly one normally arranged child.</summary>
    [Fact]
    public void Layout_WhenChildIsPresent_DoesNotReserveShadowOffset()
    {
        ProbeControl child = new(new Size(3, 2));
        Shadow shadow = new() { Child = child };

        new Engine().Layout(shadow, new Size(3, 2));

        shadow.DesiredSize.ShouldBe(new Size(3, 2));
        shadow.Bounds.ShouldBe(new Rect(0, 0, 3, 2));
        child.Bounds.ShouldBe(shadow.Bounds);
    }

    #endregion

    #region Rendering

    /// <summary>Verifies block mode draws only the shifted non-overlapping footprint.</summary>
    [Fact]
    public void Render_WhenModeIsBlockGlyph_DrawsTurboVisionFootprint()
    {
        Shadow shadow = CreateArranged(ShadowMode.BlockGlyph, new Point(2, 1));
        using Frame frame = new(new Size(5, 3));

        shadow.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(4, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(3, 2)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(4, 2)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe(string.Empty);
    }

    /// <summary>Verifies composite mode preserves glyphs and changes only their style.</summary>
    [Fact]
    public void Render_WhenModeIsComposite_PreservesUnderlyingGlyphs()
    {
        Shadow shadow = CreateArranged(ShadowMode.Composite, new Point(2, 1));
        shadow.Background = Color.Indexed(4);
        using Frame frame = new(new Size(5, 3));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune('x'));

        shadow.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("x");
        frame.GetCell(new Point(3, 1)).Style.Background.ShouldBe(Color.Indexed(4));
        frame.GetCell(new Point(2, 1)).Style.ShouldBe(CellStyle.Default);
    }

    /// <summary>Verifies composite mode restyles a complete wide owner.</summary>
    [Fact]
    public void Render_WhenShadowTouchesWideGlyph_StylesCompleteOwner()
    {
        Shadow shadow = CreateArranged(ShadowMode.Composite, new Point(2, 1));
        shadow.Background = Color.Indexed(4);
        using Frame frame = new(new Size(5, 3));
        _ = frame.Canvas.Draw("界", new Point(3, 1));

        shadow.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("界");
        frame.GetCell(new Point(3, 1)).Style.Background.ShouldBe(Color.Indexed(4));
        frame.GetCell(new Point(4, 1)).Style.Background.ShouldBe(Color.Indexed(4));
    }

    /// <summary>Verifies negative offsets draw above and left without changing hit testing.</summary>
    [Fact]
    public void Render_WhenOffsetIsNegative_DrawsVisualOverflowWithoutHitTarget()
    {
        Shadow shadow = new()
        {
            Child = new ProbeControl(new Size(3, 2)),
            Mode = ShadowMode.BlockGlyph,
            Offset = new Point(-1, -1),
        };
        shadow.Measure(new Constraint(3, 2));
        shadow.Arrange(new Rect(1, 1, 3, 2));
        using Frame frame = new(new Size(4, 3));

        shadow.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("▓");
        shadow.HitTest(new Point(0, 0)).ShouldBeNull();
    }

    /// <summary>Verifies ancestor clipping contains visual overflow.</summary>
    [Fact]
    public void Render_WhenAncestorCanvasClipsShadow_DoesNotEscapeClip()
    {
        Shadow shadow = CreateArranged(ShadowMode.BlockGlyph, new Point(2, 1));
        using Frame frame = new(new Size(5, 3));

        shadow.Render(frame.Canvas.Clip(new Rect(0, 0, 4, 3)));

        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("▓");
        frame.GetCell(new Point(4, 1)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(4, 2)).ShouldBe(CellInfo.Blank);
    }

    #endregion

    private static Shadow CreateArranged(ShadowMode mode, Point offset)
    {
        Shadow shadow = new()
        {
            Child = new ProbeControl(new Size(3, 2)),
            Mode = mode,
            Offset = offset,
        };
        new Engine().Layout(shadow, new Size(3, 2));
        return shadow;
    }
}
