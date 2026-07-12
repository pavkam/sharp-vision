using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;
using SharpVision.Terminal.Unicode;

using Shouldly;

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>
/// Verifies grapheme-safe canvas drawing, clipping, wrapping, and repair.
/// </summary>
public sealed class CanvasTests
{
    /// <summary>Verifies a base-less cluster cannot alter its preceding cell.</summary>
    /// <param name="value">The base-less source cluster.</param>
    [Theory]
    [InlineData("\u0301")]
    [InlineData("\u0903")]
    [InlineData("\u0600")]
    [InlineData("\u200d")]
    [InlineData("\ufe0f")]
    [InlineData("🏽")]
    [InlineData("\U000e0067")]
    public void Draw_WhenClusterHasNoBase_StoresIndependentReplacement(
        string value)
    {
        // Arrange
        using var frame = new Frame(new Size(2, 1));
        _ = frame.Canvas.Draw("a".AsSpan(), new Point(0, 0));

        // Act
        var result = frame.Canvas.Draw(value.AsSpan(), new Point(1, 0));

        // Assert
        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("a");
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("�");
        result.Final.ShouldBe(new Point(2, 0));
        result.Cells.ShouldBe(1);
        result.Replaced.ShouldBe(1);
    }

    /// <summary>
    /// Verifies the frame's explicit ambiguous-width policy reaches drawing.
    /// </summary>
    [Fact]
    public void Draw_WhenAmbiguousPolicyIsWide_OwnsTwoCells()
    {
        using var frame = new Frame(new Size(2, 1), ambiguousWidth: Ambiguous.Wide);

        _ = frame.Canvas.Draw("·".AsSpan(), new Point(0, 0));

        frame.GetCell(new Point(0, 0)).Width.ShouldBe(2);
        frame.GetCell(new Point(1, 0)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies narrow and wide graphemes create semantic lead/continuation cells.
    /// </summary>
    [Fact]
    public void Draw_WhenTextContainsNarrowAndWideClusters_AssignsCellOwnership()
    {
        using var frame = new Frame(new Size(4, 1));

        var result = frame.Canvas.Draw("A界".AsSpan(), new Point(0, 0));

        result.Final.ShouldBe(new Point(3, 0));
        result.Graphemes.ShouldBe(2);
        result.Cells.ShouldBe(3);
        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("A");
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("界");
        frame.GetCell(new Point(2, 0)).IsContinuation.ShouldBeTrue();
        frame.GetCell(new Point(2, 0)).Lead.ShouldBe(new Point(1, 0));
    }

    /// <summary>
    /// Verifies overwriting a continuation repairs the complete previous glyph.
    /// </summary>
    [Fact]
    public void Draw_WhenContinuationIsOverwritten_RepairsWholeWideCluster()
    {
        using var frame = new Frame(new Size(2, 1));
        _ = frame.Canvas.Draw("界".AsSpan(), new Point(0, 0));

        _ = frame.Canvas.Draw("x".AsSpan(), new Point(1, 0));

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(1, 0)).IsContinuation.ShouldBeFalse();
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("x");
    }

    /// <summary>
    /// Verifies overwriting a wide lead clears its stale continuation.
    /// </summary>
    [Fact]
    public void Draw_WhenWideLeadIsOverwritten_RepairsContinuation()
    {
        using var frame = new Frame(new Size(2, 1));
        _ = frame.Canvas.Draw("界".AsSpan(), new Point(0, 0));

        _ = frame.Canvas.Draw("x".AsSpan(), new Point(0, 0));

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("x");
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>
    /// Verifies clearing either occupied cell expands to the full glyph.
    /// </summary>
    [Fact]
    public void Clear_WhenRegionTouchesContinuation_RepairsWholeWideCluster()
    {
        using var frame = new Frame(new Size(3, 1));
        _ = frame.Canvas.Draw("界z".AsSpan(), new Point(0, 0));

        frame.Canvas.Clear(new Rect(1, 0, 1, 1));

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("z");
    }

    /// <summary>
    /// Verifies a wide edge cluster can be clipped without half output.
    /// </summary>
    [Fact]
    public void Draw_WhenWideClusterHitsEdgeAndPolicyIsClip_SkipsWholeCluster()
    {
        using var frame = new Frame(new Size(2, 1));

        var result = frame.Canvas.Draw("a界".AsSpan(), new Point(0, 0), edge: Edge.Clip);

        result.Clipped.ShouldBe(1);
        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("a");
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>
    /// Verifies a wide edge cluster wraps as one glyph.
    /// </summary>
    [Fact]
    public void Draw_WhenWideClusterHitsEdgeAndPolicyIsWrap_MovesWholeCluster()
    {
        using var frame = new Frame(new Size(2, 2));

        var result = frame.Canvas.Draw("a界".AsSpan(), new Point(0, 0), edge: Edge.Wrap);

        result.Final.ShouldBe(new Point(2, 1));
        FrameTests.GetText(frame, new Point(0, 1)).ShouldBe("界");
        frame.GetCell(new Point(1, 1)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies a wide edge cluster can be replaced with one visible cell.
    /// </summary>
    [Fact]
    public void Draw_WhenWideClusterHitsEdgeAndPolicyIsReplace_WritesReplacement()
    {
        using var frame = new Frame(new Size(2, 1));

        var result = frame.Canvas.Draw("a界".AsSpan(), new Point(0, 0), edge: Edge.Replace);

        result.Replaced.ShouldBe(1);
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("�");
        frame.GetCell(new Point(1, 0)).Width.ShouldBe(1);
    }

    /// <summary>
    /// Verifies combining and ZWJ text is stored once as complete UTF-8.
    /// </summary>
    [Fact]
    public void Draw_WhenClusterHasMultipleRunes_StoresCompleteGrapheme()
    {
        using var frame = new Frame(new Size(3, 1));

        _ = frame.Canvas.Draw("e\u0301👩‍💻".AsSpan(), new Point(0, 0));

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("e\u0301");
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("👩‍💻");
        frame.GetCell(new Point(2, 0)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies invalid UTF-16 is stored as one replacement-cell payload.
    /// </summary>
    [Fact]
    public void Draw_WhenUtf16IsInvalid_StoresReplacementRune()
    {
        using var frame = new Frame(new Size(1, 1));

        _ = frame.Canvas.Draw("\ud800".AsSpan(), new Point(0, 0));

        FrameTests.GetText(frame, new Point(0, 0)).ShouldBe("�");
        frame.GetCell(new Point(0, 0)).Width.ShouldBe(1);
    }

    /// <summary>
    /// Verifies arena-limit validation happens before any cell mutation.
    /// </summary>
    [Fact]
    public void Draw_WhenTextExceedsArenaLimit_ThrowsBeforeMutation()
    {
        using var frame = new Frame(new Size(2, 1), maxTextBytes: 1);

        _ = Should.Throw<InvalidOperationException>(
            () => frame.Canvas.Draw("ab".AsSpan(), new Point(0, 0)));

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>
    /// Verifies child clips never draw outside their intersection.
    /// </summary>
    [Fact]
    public void Clip_WhenDrawingCrossesIntersection_PreservesOutsideCells()
    {
        using var frame = new Frame(new Size(4, 1));
        var canvas = frame.Canvas.Clip(new Rect(1, 0, 2, 1));

        _ = canvas.Draw("abcd".AsSpan(), new Point(0, 0));

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        FrameTests.GetText(frame, new Point(1, 0)).ShouldBe("b");
        FrameTests.GetText(frame, new Point(2, 0)).ShouldBe("c");
        frame.GetCell(new Point(3, 0)).ShouldBe(CellInfo.Blank);
    }
}
