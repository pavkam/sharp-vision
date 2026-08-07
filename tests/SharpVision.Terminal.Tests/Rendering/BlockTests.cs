// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

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
        using Frame frame = new(new Size(2, 1));

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
        using Frame frame = new(new Size(1, 1));

        frame.Canvas.DrawQuadrants(default, quadrants);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe(expected);
    }

    /// <summary>Verifies separate quadrant draws merge commutatively.</summary>
    [Fact]
    public void DrawQuadrants_WhenSeparateMasksShareCell_MergesBits()
    {
        using Frame frame = new(new Size(1, 1));

        frame.Canvas.DrawQuadrants(default, Quadrants.UpperLeft);
        frame.Canvas.DrawQuadrants(default, Quadrants.LowerRight);

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("▚");
    }

    /// <summary>
    /// Verifies the portable ASCII fallback glyph ('#'), which every non-empty quadrant mask
    /// resolves to under <see cref="Ambiguous.Wide"/>, round-trips through <c>TryDecode</c> as a
    /// conservative superset (<see cref="Quadrants.All"/>) instead of being silently treated as
    /// empty. A frame whose exact quadrant Rune has already been demoted to '#' by the wide
    /// policy can never recover which quadrants were actually filled, so <c>DrawQuadrants</c>'s
    /// documented "merges filled quadrants" contract can only be honored by over-including
    /// (mirroring how <c>LineResolver</c> decodes the ASCII line-cross fallback '+' to all four
    /// connections) rather than under-including.
    /// </summary>
    [Fact]
    public void TryDecode_WhenRuneIsThePortableAsciiFallback_DecodesAsAllQuadrants()
    {
        new Rune('#').TryDecode(out Quadrants quadrants).ShouldBeTrue();
        quadrants.ShouldBe(Quadrants.All);
    }

    /// <summary>
    /// Verifies a second quadrant draw sharing a cell with a first draw that was already demoted
    /// to the ASCII fallback (because the frame uses <see cref="Ambiguous.Wide"/>) still merges
    /// as the union of both requested masks rather than discarding the first draw's bits.
    /// </summary>
    [Fact]
    public void DrawQuadrants_WhenAmbiguousWidthIsWideAndMasksShareCell_MergesBits()
    {
        using Frame frame = new(new Size(1, 1), ambiguousWidth: Ambiguous.Wide);

        frame.Canvas.DrawQuadrants(default, Quadrants.UpperLeft);
        frame.Canvas.DrawQuadrants(default, Quadrants.LowerRight);

        var bytes = frame.GetGrapheme(frame.GetIndex(default));
        Rune.DecodeFromUtf8(bytes, out var stored, out _).ShouldBe(OperationStatus.Done);
        stored.TryDecode(out Quadrants merged).ShouldBeTrue();
        merged.ShouldBe(Quadrants.All, "the union of UpperLeft and LowerRight must be recoverable, not just the last draw's mask");
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
        { Quadrants.All, "█" }
    };

    #endregion
}
