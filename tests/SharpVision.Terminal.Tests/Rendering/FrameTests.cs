// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>
/// Verifies semantic frame geometry, ownership, validation, and disposal.
/// </summary>
public sealed class FrameTests
{
    /// <summary>Verifies cursor shape is immutable frame state and rejects unknown values.</summary>
    [Fact]
    public void SetCursor_WhenShapeIsSpecified_CommitsValidatedSemanticShape()
    {
        using Frame frame = new(new Size(2, 1));

        frame.SetCursor(new Point(1, 0), visible: true, CursorShape.Bar);

        frame.Cursor.ShouldBe(new Cursor(new Point(1, 0), true, CursorShape.Bar));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            frame.SetCursor(default, visible: true, (CursorShape) 99));
        frame.Cursor.ShouldBe(new Cursor(new Point(1, 0), true, CursorShape.Bar));
    }

    /// <summary>Verifies cloning grows its text arena before copying a large semantic frame.</summary>
    [Fact]
    public void Clone_WhenTextExceedsInitialArena_PreservesEveryGrapheme()
    {
        const int length = 300;
        using Frame frame = new(new Size(length, 1));
        _ = frame.Canvas.Draw(new string('x', length), default, CellStyle.Default);

        using var clone = frame.Clone();

        clone.Size.ShouldBe(frame.Size);
        clone.GetGraphemeByteCount(new Point(0, 0)).ShouldBe(1);
        clone.GetGraphemeByteCount(new Point(length - 1, 0)).ShouldBe(1);
        GetText(clone, new Point(length - 1, 0)).ShouldBe("x");
    }

    /// <summary>Verifies explicit rendering-value constructors reject impossible metrics.</summary>
    [Fact]
    public void Constructor_WhenRenderingValueIsInvalid_ThrowsDocumentedException()
    {
        _ = Should.Throw<ArgumentException>(() =>
            new CellInfo(CellStyle.Default, width: 1, isContinuation: true, lead: default));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new DrawResult(default, graphemes: -1, cells: 0, clipped: 0, replaced: 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new EncodeResult(-1, full: false));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new RenderMetrics(0, 0, 0, full: false, elapsed: TimeSpan.FromTicks(-1)));
    }

    /// <summary>
    /// Verifies negative extents are rejected by geometry value constructors.
    /// </summary>
    [Fact]
    public void Constructor_WhenGeometryExtentIsNegative_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Size(-1, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Rect(0, 0, -1, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new CellMetrics(0, 1));
    }

    /// <summary>
    /// Verifies zero-sized suspended screens are valid and address no cell.
    /// </summary>
    [Fact]
    public void Constructor_WhenSizeIsZero_CreatesSuspendedFrame()
    {
        using Frame frame = new(new Size(0, 0));

        frame.Size.ShouldBe(new Size(0, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => frame.GetCell(new Point(0, 0)));
    }

    /// <summary>
    /// Verifies public coordinates are checked before frame state is read.
    /// </summary>
    [Fact]
    public void GetCell_WhenPointIsOutsideFrame_ThrowsArgumentOutOfRangeException()
    {
        using Frame frame = new(new Size(2, 1));

        _ = Should.Throw<ArgumentOutOfRangeException>(() => frame.GetCell(new Point(-1, 0)));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => frame.GetCell(new Point(2, 0)));
    }

    /// <summary>
    /// Verifies borrowed frame text is copied only into a sufficient destination.
    /// </summary>
    [Fact]
    public void CopyGrapheme_WhenDestinationIsTooSmall_ThrowsArgumentException()
    {
        using Frame frame = new(new Size(2, 1));
        _ = frame.Canvas.Draw("界".AsSpan(), new Point(0, 0));

        frame.GetGraphemeByteCount(new Point(0, 0)).ShouldBe(3);
        _ = Should.Throw<ArgumentException>(() => frame.CopyGrapheme(new Point(0, 0), new byte[2]));

        Span<byte> destination = stackalloc byte[3];
        frame.CopyGrapheme(new Point(1, 0), destination).ShouldBe(3);
        destination.SequenceEqual("界"u8).ShouldBeTrue();
    }

    /// <summary>
    /// Verifies clearing resets semantic cells, styles, and grapheme arena usage.
    /// </summary>
    [Fact]
    public void Clear_WhenFrameContainsText_ResetsEveryCell()
    {
        using Frame frame = new(new Size(2, 1));
        var style = new CellStyle(attributes: TerminalAttributes.Bold);
        _ = frame.Canvas.Draw("ab".AsSpan(), new Point(0, 0), style);

        frame.Clear();

        frame.GetCell(new Point(0, 0)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
        frame.GetGraphemeByteCount(new Point(0, 0)).ShouldBe(0);
    }

    /// <summary>
    /// Verifies disposed pooled state cannot be observed or mutated.
    /// </summary>
    [Fact]
    public void Dispose_WhenCalled_RejectsFurtherAccess()
    {
        var frame = new Frame(new Size(1, 1));

        frame.Dispose();
        frame.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => frame.Canvas);
        _ = Should.Throw<ObjectDisposedException>(() => frame.GetCell(new Point(0, 0)));
    }

    internal static string GetText(Frame frame, Point point)
    {
        var count = frame.GetGraphemeByteCount(point);
        var bytes = new byte[count];
        frame.CopyGrapheme(point, bytes).ShouldBe(count);

        return Encoding.UTF8.GetString(bytes);
    }
}
