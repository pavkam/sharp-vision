using System.Buffers;

using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;
using SharpVision.Terminal.Tests.Support;

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>
/// Verifies incremental output reaches the same semantic terminal state as full output.
/// </summary>
public sealed class EquivalenceTests
{
    /// <summary>
    /// Verifies targeted multi-frame transitions against an independent terminal model.
    /// </summary>
    /// <param name="frontText">The committed front-frame text.</param>
    /// <param name="backText">The target back-frame text.</param>
    [Theory]
    [InlineData("abcd", "abXd")]
    [InlineData("界x", "abx")]
    [InlineData("abx", "界x")]
    [InlineData("e\u0301x", "éx")]
    public void Encode_WhenFrameTransitions_AgreesWithFullRender(
        string frontText,
        string backText)
    {
        using var front = Create(frontText);
        using var back = Create(backText);
        var incremental = new VirtualScreen(back.Size);
        incremental.Apply(Encode(null, front));
        incremental.Apply(Encode(front, back));
        var full = new VirtualScreen(back.Size);
        full.Apply(Encode(null, back));

        incremental.ShouldMatch(back);
        full.ShouldMatch(back);
        incremental.ShouldMatch(full);
    }

    /// <summary>
    /// Verifies style, hyperlink, and cursor transitions are semantically equivalent.
    /// </summary>
    [Fact]
    public void Encode_WhenStyleAndCursorChange_AgreesWithFullRender()
    {
        using var front = new Frame(new Size(2, 1));
        using var back = new Frame(new Size(2, 1));
        _ = front.Canvas.Draw("ab".AsSpan(), new Point(0, 0));
        _ = back.Canvas.Draw(
            "ab".AsSpan(),
            new Point(0, 0),
            new Style(attributes: Attributes.Bold, hyperlink: "https://example.test"));
        back.SetCursor(new Point(1, 0), visible: true);
        var incremental = new VirtualScreen(back.Size);
        incremental.Apply(Encode(null, front));
        incremental.Apply(Encode(front, back));
        var full = new VirtualScreen(back.Size);
        full.Apply(Encode(null, back));

        incremental.ShouldMatch(back);
        incremental.ShouldMatch(full);
    }

    private static byte[] Encode(Frame? front, Frame back)
    {
        var destination = new ArrayBufferWriter<byte>();
        _ = Encoder.Encode(front, back, destination);
        return destination.WrittenSpan.ToArray();
    }

    private static Frame Create(string value)
    {
        var frame = new Frame(new Size(3, 1));
        _ = frame.Canvas.Draw(value.AsSpan(), new Point(0, 0));
        return frame;
    }
}
