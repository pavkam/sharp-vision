namespace SharpVision.Terminal.Tests.Rendering;

using System.Buffers;
using System.Text;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

using Shouldly;

using CapabilitySupport = Terminal.Capabilities.Support;
using FrameEncoder = Terminal.Rendering.Encoder;
using TerminalCapabilities = Terminal.Capabilities.Capabilities;

/// <summary>
/// Verifies deterministic exact bytes for full and incremental frame encoding.
/// </summary>
public sealed class EncoderTests
{
    private static TerminalCapabilities TrueColorCapabilities { get; } =
        TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor };

    /// <summary>Provides exact foreground/background degradation for every color tier.</summary>
    public static TheoryData<ColorDepth, string> ColorDepthCases => new()
    {
        {
            ColorDepth.TrueColor,
            "\u001b[1;1H\u001b[38;2;95;135;175m\u001b[48;2;255;0;0mx\u001b[0m\u001b[1;1H\u001b[?25l"
        },
        {
            ColorDepth.Indexed256,
            "\u001b[1;1H\u001b[38;5;67m\u001b[48;5;9mx\u001b[0m\u001b[1;1H\u001b[?25l"
        },
        {
            ColorDepth.Basic16,
            "\u001b[1;1H\u001b[90m\u001b[101mx\u001b[0m\u001b[1;1H\u001b[?25l"
        },
        {
            ColorDepth.Monochrome,
            "\u001b[1;1Hx\u001b[1;1H\u001b[?25l"
        },
    };

    /// <summary>Verifies semantic colors project to exact bytes at every capability tier.</summary>
    /// <param name="depth">The active color fidelity.</param>
    /// <param name="expected">The complete expected frame output.</param>
    [Theory]
    [MemberData(nameof(ColorDepthCases))]
    public void Encode_WhenColorDepthChanges_WritesHighestSupportedRepresentation(
        ColorDepth depth,
        string expected)
    {
        using var back = new Frame(new Size(1, 1));
        var style = new Style(Color.Rgb(95, 135, 175), Color.Rgb(255, 0, 0));
        _ = back.Canvas.Draw("x", default, style);
        var destination = new ArrayBufferWriter<byte>();
        var capabilities = TerminalCapabilities.Conservative with { ColorDepth = depth };

        _ = FrameEncoder.Encode(null, back, destination, capabilities);

        destination.WrittenSpan.ToArray().ShouldBe(Encoding.ASCII.GetBytes(expected));
    }

    /// <summary>Verifies semantic colors collapsing to one basic color emit one transition.</summary>
    [Fact]
    public void Encode_WhenSemanticColorsProjectEqually_DoesNotEmitRedundantTransition()
    {
        using var back = new Frame(new Size(2, 1));
        _ = back.Canvas.Draw("a", default, new Style(Color.Rgb(255, 0, 0)));
        _ = back.Canvas.Draw("b", new Point(1, 0), new Style(Color.Rgb(250, 5, 5)));
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(
            null,
            back,
            destination,
            TerminalCapabilities.Conservative);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[1;1H\u001b[91mab\u001b[0m\u001b[1;1H\u001b[?25l"u8.ToArray());
    }

    /// <summary>Verifies a missing capability snapshot fails before destination mutation.</summary>
    [Fact]
    public void Encode_WhenCapabilitiesAreNull_ThrowsBeforeWriting()
    {
        using var back = Create("x");
        var destination = new ArrayBufferWriter<byte>();

        _ = Should.Throw<ArgumentNullException>(() =>
            FrameEncoder.Encode(null, back, destination, null!));

        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies unknown optional decoration support uses conservative fallbacks.</summary>
    [Fact]
    public void Encode_WhenModernDecorationsAreUnknown_DegradesWithoutUnsupportedBytes()
    {
        using var back = new Frame(new Size(1, 1));
        var style = new Style(
            attributes: Attributes.RapidBlink | Attributes.Overline,
            underline: Underline.Curly,
            underlineColor: Color.Rgb(1, 2, 3));
        _ = back.Canvas.Draw("x", default, style);
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(
            null,
            back,
            destination,
            TrueColorCapabilities);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[1;1H\u001b[6m\u001b[4mx\u001b[0m\u001b[1;1H\u001b[?25l"u8.ToArray());
    }

    /// <summary>Verifies proven optional decoration support emits exact modern SGR.</summary>
    [Fact]
    public void Encode_WhenModernDecorationsAreSupported_WritesExactBytes()
    {
        using var back = new Frame(new Size(1, 1));
        var style = new Style(
            attributes: Attributes.RapidBlink | Attributes.Overline,
            underline: Underline.Curly,
            underlineColor: Color.Rgb(1, 2, 3));
        _ = back.Canvas.Draw("x", default, style);
        var destination = new ArrayBufferWriter<byte>();
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var capabilities = TrueColorCapabilities with
        {
            StyledUnderlines = supported,
            UnderlineColor = supported,
            Overline = supported,
        };

        _ = FrameEncoder.Encode(null, back, destination, capabilities);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[1;1H\u001b[6m\u001b[53m\u001b[4:3m\u001b[58;2;1;2;3mx"u8.ToArray()
                .Concat("\u001b[0m\u001b[1;1H\u001b[?25l"u8.ToArray())
                .ToArray());
    }

    /// <summary>Verifies an orphan component is emitted as an independent replacement cell.</summary>
    [Fact]
    public void Encode_WhenFrameContainsOrphanMark_WritesReplacementBytes()
    {
        // Arrange
        using var back = new Frame(new Size(2, 1));
        _ = back.Canvas.Draw("a".AsSpan(), new Point(0, 0));
        _ = back.Canvas.Draw("\u0301".AsSpan(), new Point(1, 0));
        var destination = new ArrayBufferWriter<byte>();

        // Act
        _ = FrameEncoder.Encode(null, back, destination, TrueColorCapabilities);

        // Assert
        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[1;1Ha�\u001b[1;1H\u001b[?25l"u8.ToArray());
        destination.WrittenSpan.IndexOf("\u0301"u8).ShouldBe(-1);
    }

    /// <summary>
    /// Verifies a complete default-style frame emits exact position/text/cursor bytes.
    /// </summary>
    [Fact]
    public void Encode_WhenFrameIsFull_WritesExactBytes()
    {
        using var back = Create("ab");
        var destination = new ArrayBufferWriter<byte>();

        var result = FrameEncoder.Encode(null, back, destination, TrueColorCapabilities);

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

        var result = FrameEncoder.Encode(front, back, destination, TrueColorCapabilities);

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

        var result = FrameEncoder.Encode(front, back, destination, TrueColorCapabilities);

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

        _ = FrameEncoder.Encode(null, back, destination, TrueColorCapabilities);

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

        _ = FrameEncoder.Encode(null, back, destination, TrueColorCapabilities);

        destination.WrittenSpan.EndsWith("\u001b[1;2H\u001b[?25h"u8).ShouldBeTrue();
    }

    private static Frame Create(string value)
    {
        var frame = new Frame(new Size(value.Length, 1));
        _ = frame.Canvas.Draw(value.AsSpan(), new Point(0, 0));
        return frame;
    }
}
