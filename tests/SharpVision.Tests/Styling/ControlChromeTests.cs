// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;




using ControlText = SharpVision.Controls.Text;

/// <summary>Verifies shared control chrome rasterization and geometry.</summary>
public sealed class ControlChromeTests
{
    /// <summary>Verifies partial border edges draw only enabled sides on tiny bounds.</summary>
    [Fact]
    public void DrawPartialBorder_WhenOnlyTopEdgeIsEnabled_DrawsSingleRow()
    {
        Border border = new()
        {
            Bounds = new Rect(0, 0, 3, 2),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderGlyphs = Glyphs.Ascii,
        };
        using Frame frame = new(new Size(3, 2));

        border.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("-");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("-");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("-");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe(string.Empty);
    }

    /// <summary>Verifies composite shadow overflow stays outside the body rectangle.</summary>
    [Fact]
    public void DrawShadow_WhenCompositeModeIsUsed_LeavesBodyCellsUntouched()
    {
        Shadow shadow = new()
        {
            Bounds = new Rect(0, 0, 2, 2),
            Mode = ShadowMode.Composite,
            Offset = new Point(1, 1),
        };
        using Frame frame = new(new Size(4, 4));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune('x'));

        shadow.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("x");
        frame.GetCell(new Point(0, 0)).Style.Attributes.ShouldNotBe(Attributes.Dim);
        frame.GetCell(new Point(2, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
    }

    /// <summary>Verifies border thickness reduces the arranged content box.</summary>
    [Fact]
    public void ContentBounds_WhenBorderAndPaddingAreSet_DeflatesBeforePadding()
    {
        ChromeProbe control = new()
        {
            Bounds = new Rect(0, 0, 6, 4),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(1),
        };

        control.ExposedContentBounds.ShouldBe(new Rect(2, 2, 2, 0));
    }

    /// <summary>Verifies border thickness increases measured size around content.</summary>
    [Fact]
    public void Measure_WhenBorderThicknessIsSet_ReservesActiveEdges()
    {
        Border border = new()
        {
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = new ControlText("ab"),
        };

        new Engine().Layout(border, new Size(10, 4));

        border.DesiredSize.ShouldBe(new Size(3, 1));
    }
}
