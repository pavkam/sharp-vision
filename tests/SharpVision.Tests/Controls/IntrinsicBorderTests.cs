// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies intrinsic border layout, validation, styling, and cells on ordinary controls.</summary>
public sealed class IntrinsicBorderTests
{
    #region Presets and validation

    /// <summary>Verifies every named family exposes exact corner and edge glyphs.</summary>
    [Theory]
    [MemberData(nameof(PresetCases))]
    public void BorderGlyphs_WhenPresetIsSelected_UsesExactRunes(
        Glyphs glyphs,
        char corner,
        char horizontal,
        char vertical)
    {
        glyphs.TopLeft.ShouldBe(new Rune(corner));
        glyphs.Top.ShouldBe(new Rune(horizontal));
        glyphs.Left.ShouldBe(new Rune(vertical));
    }

    /// <summary>Provides the supported Unicode and portable border families.</summary>
    public static TheoryData<Glyphs, char, char, char> PresetCases => new()
    {
        { Glyphs.Light, '┌', '─', '│' },
        { Glyphs.Heavy, '┏', '━', '┃' },
        { Glyphs.Paired, '╔', '═', '║' },
        { Glyphs.Rounded, '╭', '─', '│' },
        { Glyphs.Ascii, '+', '-', '|' },
        { Glyphs.Solid, '█', '█', '█' },
        { Glyphs.LightShade, '░', '░', '░' },
        { Glyphs.MediumShade, '▒', '▒', '▒' },
        { Glyphs.DarkShade, '▓', '▓', '▓' },
    };

    /// <summary>Verifies thickness accepts only zero or one before changing the control.</summary>
    [Fact]
    public void BorderThickness_WhenAnEdgeExceedsOne_ThrowsBeforeMutation()
    {
        LayoutProbe control = new() { BorderThickness = new Thickness(1) };

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            control.BorderThickness = new Thickness(2, 0, 0, 0));

        control.BorderThickness.ShouldBe(new Thickness(1));
    }

    /// <summary>Verifies every custom border glyph must be a printable narrow Rune.</summary>
    [Theory]
    [InlineData('\n')]
    [InlineData('界')]
    public void BorderGlyphs_WhenGlyphIsNotPrintableNarrow_ThrowsBeforeMutation(char value)
    {
        LayoutProbe control = new();

        _ = Should.Throw<ArgumentException>(() => control.BorderGlyphs = new Glyphs(
            new Rune(value),
            new Rune('-'),
            new Rune('+'),
            new Rune('|'),
            new Rune('+'),
            new Rune('-'),
            new Rune('+'),
            new Rune('|')));

        control.BorderGlyphs.ShouldBe(Glyphs.Default);
    }

    #endregion

    #region Layout

    /// <summary>Verifies margin, border, and padding compose around ordinary container content.</summary>
    [Fact]
    public void Layout_WhenChildHasMarginPaddingAndBorder_ComputesExactBounds()
    {
        ProbeControl child = new(new Size(2, 1)) { Margin = new Thickness(1) };
        Stack control = new()
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(1),
            Children = { child },
        };

        new Engine().Layout(control, new Size(8, 7));

        control.DesiredSize.ShouldBe(new Size(8, 7));
        child.Bounds.ShouldBe(new Rect(3, 3, 2, 1));
    }

    #endregion

    #region Rendering

    /// <summary>Verifies intrinsic default border glyphs and Unicode child content render exact cells.</summary>
    [Fact]
    public void Render_WhenBorderIsComplete_WritesCornersEdgesAndChild()
    {
        LayoutProbe control = new()
        {
            BorderThickness = new Thickness(1),
            Children = { new ControlText("界") },
        };
        new Engine().Layout(control, new Size(4, 3));
        using Frame frame = new(new Size(4, 3));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("┌");
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe("┐");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("└");
        FrameOracle.Get(frame, new Point(3, 2)).ShouldBe("┘");
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("界");
        frame.GetCell(new Point(2, 1)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies partial intrinsic edges, custom glyphs, background, and color remain exact.</summary>
    [Fact]
    public void Render_WhenEdgesArePartial_UsesOnlyActiveCustomGlyphsAndStyles()
    {
        LayoutProbe control = new()
        {
            BorderThickness = new Thickness(1, 1, 0, 0),
            BorderGlyphs = Glyphs.Ascii,
            BorderColor = Color.Indexed(3),
            Background = Color.Indexed(4),
            Width = Length.Cells(3),
            Height = Length.Cells(2),
        };
        new Engine().Layout(control, new Size(3, 2));
        using Frame frame = new(new Size(3, 2));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("+");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("-");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("|");
        frame.GetCell(default).Style.Foreground.ShouldBe(Color.Indexed(3));
        frame.GetCell(new Point(1, 1)).Style.Background.ShouldBe(Color.Indexed(4));
    }

    /// <summary>Verifies zero and tiny bounds never emit incomplete corners outside clipping.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 3)]
    public void Render_WhenBoundsAreTiny_RemainsContained(int width, int height)
    {
        LayoutProbe control = new() { BorderThickness = new Thickness(1) };
        new Engine().Layout(control, new Size(width, height));
        using Frame frame = new(new Size(Math.Max(1, width), Math.Max(1, height)));

        Should.NotThrow(() => control.Render(frame.Canvas));
    }

    #endregion
}
