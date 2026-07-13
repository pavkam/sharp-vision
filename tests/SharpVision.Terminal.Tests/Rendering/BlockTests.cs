// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;

using Shouldly;

/// <summary>Verifies shade and quadrant block drawing.</summary>
public sealed class BlockTests
{
    #region Shades

    /// <summary>Verifies every shade resolves to its exact Block Elements Rune.</summary>
    [Theory]
    [InlineData(Shade.Light, "░")]
    [InlineData(Shade.Medium, "▒")]
    [InlineData(Shade.Dark, "▓")]
    [InlineData(Shade.Solid, "█")]
    public void FillShade_WhenShadeIsSelected_WritesExactGlyph(Shade shade, string expected)
    {
        using Frame frame = new Frame(new Size(2, 1));

        frame.Canvas.FillShade(new Rect(0, 0, 2, 1), shade);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe(expected);
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe(expected);
    }

    #endregion

    #region Quadrants

    /// <summary>Verifies every quadrant mask resolves to the exact Unicode Rune.</summary>
    [Theory]
    [MemberData(nameof(QuadrantCases))]
    public void DrawQuadrants_WhenMaskIsSelected_WritesExactGlyph(
        Quadrants quadrants,
        string expected)
    {
        using Frame frame = new Frame(new Size(1, 1));

        frame.Canvas.DrawQuadrants(default, quadrants);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe(expected);
    }

    /// <summary>Verifies separate quadrant draws merge commutatively.</summary>
    [Fact]
    public void DrawQuadrants_WhenSeparateMasksShareCell_MergesBits()
    {
        using Frame frame = new Frame(new Size(1, 1));

        frame.Canvas.DrawQuadrants(default, Quadrants.UpperLeft);
        frame.Canvas.DrawQuadrants(default, Quadrants.LowerRight);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("▚");
    }

    /// <summary>Provides all non-empty quadrant masks and Unicode forms.</summary>
    public static TheoryData<Quadrants, string> QuadrantCases => new()
    {
        { Quadrants.UpperLeft, "▘" },
        { Quadrants.UpperRight, "▝" },
        { Quadrants.Upper, "▀" },
        { Quadrants.LowerLeft, "▖" },
        { Quadrants.UpperLeft | Quadrants.LowerLeft, "▌" },
        { Quadrants.UpperRight | Quadrants.LowerLeft, "▞" },
        { Quadrants.Upper | Quadrants.LowerLeft, "▛" },
        { Quadrants.LowerRight, "▗" },
        { Quadrants.UpperLeft | Quadrants.LowerRight, "▚" },
        { Quadrants.UpperRight | Quadrants.LowerRight, "▐" },
        { Quadrants.Upper | Quadrants.LowerRight, "▜" },
        { Quadrants.Lower, "▄" },
        { Quadrants.UpperLeft | Quadrants.Lower, "▙" },
        { Quadrants.UpperRight | Quadrants.Lower, "▟" },
        { Quadrants.All, "█" },
    };

    #endregion
}
