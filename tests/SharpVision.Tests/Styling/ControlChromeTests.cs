// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies shared control chrome rasterization and geometry.</summary>
public sealed class ControlChromeTests
{
    /// <summary>Verifies per-edge colors paint leading and trailing edges independently.</summary>
    [Fact]
    public void DrawPartialBorder_WhenEdgeColorsAreSet_UsesHorizontalColorsForCorners()
    {
        var highlight = Color.Rgb(255, 255, 255);
        var shade = Color.Rgb(0, 0, 0);
        var border = new LayoutProbe
        {
            Bounds = new Rect(0, 0, 4, 3),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Ascii,
                Color.Rgb(170, 170, 170),
                BorderEdgeColors.Sunken(highlight, shade),
                Color.Transparent,
                TerminalAttributes.None),
        };
        using Frame frame = new(new Size(4, 3));

        border.Render(frame.Canvas);

        frame.GetCell(new Point(0, 0)).Style.Foreground.ShouldBe(shade);
        frame.GetCell(new Point(3, 0)).Style.Foreground.ShouldBe(shade);
        frame.GetCell(new Point(0, 1)).Style.Foreground.ShouldBe(shade);
        frame.GetCell(new Point(3, 1)).Style.Foreground.ShouldBe(highlight);
        frame.GetCell(new Point(0, 2)).Style.Foreground.ShouldBe(highlight);
        frame.GetCell(new Point(3, 2)).Style.Foreground.ShouldBe(highlight);
    }

    /// <summary>Verifies partial border edges draw only enabled sides on tiny bounds.</summary>
    [Fact]
    public void DrawPartialBorder_WhenOnlyTopEdgeIsEnabled_DrawsSingleRow()
    {
        var border = new LayoutProbe
        {
            Bounds = new Rect(0, 0, 3, 2),
            Border = AppearanceTestValues.Border(BorderSide.Top, BorderGlyphStyle.Ascii),
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
        var shadow = new LayoutProbe
        {
            Bounds = new Rect(0, 0, 2, 2),
            Face = AppearanceTestValues.Face(background: Color.Transparent),
            Shadow = AppearanceTestValues.Shadow(visible: true, mode: ShadowMode.Composite, offset: new Point(1, 1), glyph: new Rune('▓'), attributes: TerminalAttributes.Dim),
        };
        using Frame frame = new(new Size(4, 4));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune('x'));

        shadow.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("x");
        frame.GetCell(new Point(0, 0)).Style.Attributes.ShouldNotBe(TerminalAttributes.Dim);
        frame.GetCell(new Point(2, 1)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
    }

    /// <summary>Verifies border thickness reduces the arranged content box.</summary>
    [Fact]
    public void ContentBounds_WhenBorderAndPaddingAreSet_DeflatesBeforePadding()
    {
        var control = new ChromeProbe
        {
            Bounds = new Rect(0, 0, 6, 4),
            Border = AppearanceTestValues.Border(BorderSide.All),
            Padding = new Thickness(1)
        };

        control.ExposedContentBounds.ShouldBe(new Rect(2, 2, 2, 0));
    }

    /// <summary>Verifies border thickness increases measured size around content.</summary>
    [Fact]
    public void Measure_WhenBorderIsSet_ReservesActiveEdges()
    {
        var border = new LayoutProbe { Border = AppearanceTestValues.Border(BorderSide.Left) };
        border.Children.Add(new ControlText("ab"));

        new LayoutEngine().Layout(border, new Size(10, 4));

        border.DesiredSize.ShouldBe(new Size(3, 1));
    }
}
