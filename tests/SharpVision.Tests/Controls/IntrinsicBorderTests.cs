// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies intrinsic border layout, glyph validation, styling, and exact cells.</summary>
public sealed class IntrinsicBorderTests
{
    #region Contract

    /// <summary>Verifies every named family survives the intrinsic control property with exact glyphs.</summary>
    [Theory]
    [MemberData(nameof(PresetCases))]
    public void BorderGlyphs_WhenPresetIsSelected_UsesExactRunes(
        BorderGlyphStyle glyphs,
        char corner,
        char horizontal,
        char vertical)
    {
        var control = new LayoutProbe { Border = AppearanceTestValues.Border(BorderSide.All, glyphs) };

        control.Border.GlyphStyle.TopLeft.ShouldBe(new Rune(corner));
        control.Border.GlyphStyle.Top.ShouldBe(new Rune(horizontal));
        control.Border.GlyphStyle.Left.ShouldBe(new Rune(vertical));
    }

    /// <summary>Provides the supported Unicode and portable border families.</summary>
    public static TheoryData<BorderGlyphStyle, char, char, char> PresetCases => new()
    {
        { BorderGlyphStyle.Light, '┌', '─', '│' },
        { BorderGlyphStyle.Heavy, '┏', '━', '┃' },
        { BorderGlyphStyle.Paired, '╔', '═', '║' },
        { BorderGlyphStyle.Rounded, '╭', '─', '│' },
        { BorderGlyphStyle.Ascii, '+', '-', '|' },
        { BorderGlyphStyle.Solid, '█', '█', '█' },
        { BorderGlyphStyle.HalfBlock, '▛', '▀', '▌' },
        { BorderGlyphStyle.LightShade, '░', '░', '░' },
        { BorderGlyphStyle.MediumShade, '▒', '▒', '▒' },
        { BorderGlyphStyle.DarkShade, '▓', '▓', '▓' }
    };

    /// <summary>Verifies complete Border assignment replaces custom glyphs with the documented default.</summary>
    [Fact]
    public void Border_WhenGlyphStyleIsDefault_UsesDocumentedDefault()
    {
        var control = new LayoutProbe
        {
            Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Ascii)
        };

        control.Border = AppearanceTestValues.Border(BorderSide.All);

        control.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Default);
    }

    /// <summary>Verifies the BorderGlyphStyle constructor rejects non-printable or wide Runes.</summary>
    [Theory]
    [InlineData('\n')]
    [InlineData('界')]
    public void Glyphs_WhenGlyphIsNotPrintableNarrow_Throws(char value)
    {
        _ = Should.Throw<ArgumentException>(() => new BorderGlyphStyle(
            new Rune(value),
            new Rune('-'),
            new Rune('+'),
            new Rune('|'),
            new Rune('+'),
            new Rune('-'),
            new Rune('+'),
            new Rune('|')));
    }

    /// <summary>Verifies margin, intrinsic border, and padding form distinct child insets.</summary>
    [Fact]
    public void Layout_WhenChildHasMarginPaddingAndBorder_ComputesExactBounds()
    {
        var child = new ProbeControl(new Size(2, 1)) { Margin = new Thickness(1) };
        var control = new Stack
        {
            Border = AppearanceTestValues.Border(BorderSide.All),
            Padding = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        control.Children.Add(child);

        new LayoutEngine().Layout(control, new Size(8, 7));

        control.DesiredSize.ShouldBe(new Size(8, 7));
        control.Bounds.ShouldBe(new Rect(0, 0, 8, 7));
        child.Bounds.ShouldBe(new Rect(3, 3, 2, 1));
    }

    #endregion

    #region Rendering

    /// <summary>Verifies default glyphs and Unicode child content render exact intrinsic-border cells.</summary>
    [Fact]
    public void Render_WhenBorderIsComplete_WritesCornersEdgesAndChild()
    {
        var control = CreateSurface();
        control.Border = new Border(
            BorderSide.All,
            BorderGlyphStyle.Rounded,
            ThemeColor.ControlBorder,
            Color.Transparent,
            ThemeDecoration.Border);
        control.Children.Add(new ControlText("界"));
        new LayoutEngine().Layout(control, new Size(4, 3));
        using Frame frame = new(new Size(4, 3));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╭");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("─");
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe("╮");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("│");
        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("│");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("╰");
        FrameOracle.Get(frame, new Point(1, 2)).ShouldBe("─");
        FrameOracle.Get(frame, new Point(3, 2)).ShouldBe("╯");
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("界");
        frame.GetCell(new Point(2, 1)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies partial edges, custom glyphs, background, and color remain exact.</summary>
    [Fact]
    public void Render_WhenEdgesArePartial_UsesOnlyActiveCustomGlyphsAndStyles()
    {
        var control = CreateSurface();
        control.Border = AppearanceTestValues.Border(BorderSide.Left | BorderSide.Top);
        control.Border = AppearanceTestValues.Border(
            control.Border.Sides,
            BorderGlyphStyle.Ascii,
            ReferenceColors.Get(3));
        control.Face = AppearanceTestValues.Face(background: ReferenceColors.Get(4));
        new LayoutEngine().Layout(control, new Size(3, 2));
        using Frame frame = new(new Size(3, 2));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("+");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("-");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("|");
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBeEmpty();
        frame.GetCell(default).Style.Foreground.ShouldBe(ReferenceColors.Get(3));
        frame.GetCell(new Point(2, 1)).Style.Background.ShouldBe(ReferenceColors.Get(4));
        frame.GetCell(new Point(1, 1)).Style.Background.ShouldBe(ReferenceColors.Get(4));
    }

    /// <summary>Verifies zero and tiny bounds emit only complete contained edge glyphs.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 3)]
    [InlineData(3, 1)]
    public void Render_WhenBoundsAreTiny_RemainsContained(int width, int height)
    {
        var control = CreateSurface();
        control.Border = new Border(
            BorderSide.All,
            BorderGlyphStyle.Rounded,
            ThemeColor.ControlBorder,
            Color.Transparent,
            ThemeDecoration.Border);
        new LayoutEngine().Layout(control, new Size(width, height));
        using Frame frame = new(new Size(Math.Max(1, width), Math.Max(1, height)));

        control.Render(frame.Canvas);

        switch (width, height)
        {
            case (0, 0):
                FrameOracle.Get(frame, default).ShouldBeEmpty();
                break;
            case (1, 1):
                FrameOracle.Get(frame, default).ShouldBe("╭");
                break;
            case (1, 3):
                FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╭");
                FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("│");
                FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("╰");
                break;
            case (3, 1):
                FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("╭");
                FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("─");
                FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("╮");
                break;
            default:
                throw new InvalidOperationException("The tiny-border case is not defined.");
        }
    }

    #endregion

    private static LayoutProbe CreateSurface() => new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };
}
