using System.Buffers;
using System.Text;

using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;

using Shouldly;

using FrameEncoder = SharpVision.Terminal.Rendering.Encoder;

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>
/// Verifies deterministic exact bytes for full and incremental frame encoding.
/// </summary>
public sealed class EncoderTests
{
    /// <summary>
    /// Verifies a complete default-style frame emits exact position/text/cursor bytes.
    /// </summary>
    [Fact]
    public void Encode_WhenFrameIsFull_WritesExactBytes()
    {
        using var back = Create("ab");
        var destination = new ArrayBufferWriter<byte>();

        var result = FrameEncoder.Encode(null, back, destination);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[1;1Hab\u001b[1;1H\u001b[?25l"u8.ToArray());
        result.ShouldBe(new EncodeResult(1, true));
    }

    /// <summary>
    /// Verifies a sparse change emits only its run and restores cursor position.
    /// </summary>
    [Fact]
    public void Encode_WhenOneCellChanges_WritesSparseRun()
    {
        using var front = Create("ab");
        using var back = Create("ac");
        var destination = new ArrayBufferWriter<byte>();

        var result = FrameEncoder.Encode(front, back, destination);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[1;2Hc\u001b[1;1H"u8.ToArray());
        result.Full.ShouldBeFalse();
        result.Spans.ShouldBe(1);
    }

    /// <summary>
    /// Verifies equal content and cursor state emits no bytes.
    /// </summary>
    [Fact]
    public void Encode_WhenFrameIsUnchanged_WritesNothing()
    {
        using var front = Create("ab");
        using var back = Create("ab");
        var destination = new ArrayBufferWriter<byte>();

        var result = FrameEncoder.Encode(front, back, destination);

        destination.WrittenCount.ShouldBe(0);
        result.ShouldBe(new EncodeResult(0, false));
    }

    /// <summary>
    /// Verifies style and hyperlink transitions close and reset to known state.
    /// </summary>
    [Fact]
    public void Encode_WhenCellIsStyled_WritesExactTransitions()
    {
        using var back = new Frame(new Size(1, 1));
        var style = new Style(
            attributes: Attributes.Bold,
            hyperlink: "https://example.test");
        _ = back.Canvas.Draw("x".AsSpan(), new Point(0, 0), style);
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, back, destination);

        destination.WrittenSpan.ToArray().ShouldBe(
            Encoding.UTF8.GetBytes(
             "\u001b[1;1H" +
             "\u001b]8;;https://example.test\u001b\\" +
             "\u001b[1m" +
             "x" +
             "\u001b]8;;\u001b\\" +
             "\u001b[0m" +
             "\u001b[1;1H" +
             "\u001b[?25l"));
    }

    /// <summary>
    /// Verifies the requested visible cursor state is restored after drawing.
    /// </summary>
    [Fact]
    public void Encode_WhenCursorIsVisible_RestoresPositionAndVisibility()
    {
        using var back = Create("ab");
        back.SetCursor(new Point(1, 0), visible: true);
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, back, destination);

        destination.WrittenSpan.EndsWith("\u001b[1;2H\u001b[?25h"u8).ShouldBeTrue();
    }

    private static Frame Create(string value)
    {
        var frame = new Frame(new Size(value.Length, 1));
        _ = frame.Canvas.Draw(value.AsSpan(), new Point(0, 0));
        return frame;
    }
}
