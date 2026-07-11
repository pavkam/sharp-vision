using System.Buffers;

using SharpVision.Terminal.Protocols;

using Shouldly;

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>
/// Verifies typed Select Graphic Rendition encoding.
/// </summary>
public sealed class SgrTests
{
    /// <summary>
    /// Verifies every supported rendition code against its literal sequence.
    /// </summary>
    /// <param name="rendition">The typed rendition.</param>
    /// <param name="expected">The literal expected sequence.</param>
    [Theory]
    [InlineData(Rendition.Reset, "\u001b[0m")]
    [InlineData(Rendition.Bold, "\u001b[1m")]
    [InlineData(Rendition.Dim, "\u001b[2m")]
    [InlineData(Rendition.Italic, "\u001b[3m")]
    [InlineData(Rendition.Underline, "\u001b[4m")]
    [InlineData(Rendition.SlowBlink, "\u001b[5m")]
    [InlineData(Rendition.RapidBlink, "\u001b[6m")]
    [InlineData(Rendition.Reverse, "\u001b[7m")]
    [InlineData(Rendition.Hidden, "\u001b[8m")]
    [InlineData(Rendition.Strike, "\u001b[9m")]
    [InlineData(Rendition.NormalIntensity, "\u001b[22m")]
    [InlineData(Rendition.NotItalic, "\u001b[23m")]
    [InlineData(Rendition.NotUnderline, "\u001b[24m")]
    [InlineData(Rendition.NotBlink, "\u001b[25m")]
    [InlineData(Rendition.NotReverse, "\u001b[27m")]
    [InlineData(Rendition.NotHidden, "\u001b[28m")]
    [InlineData(Rendition.NotStrike, "\u001b[29m")]
    public void Apply_WhenRenditionIsKnown_WritesLiteralSequence(
        Rendition rendition,
        string expected)
    {
        var destination = new ArrayBufferWriter<byte>();

        Sgr.Apply(new Writer(destination), rendition);

        destination.WrittenSpan.ToArray().ShouldBe(
            System.Text.Encoding.ASCII.GetBytes(expected));
    }

    /// <summary>
    /// Verifies individual attributes and reset.
    /// </summary>
    [Fact]
    public void Apply_WhenRenditionIsKnown_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new Writer(destination);

        Sgr.Apply(writer, Rendition.Bold);
        Sgr.Apply(writer, Rendition.Underline);
        Sgr.Apply(writer, Rendition.Reverse);
        Sgr.Reset(writer);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[1m\u001b[4m\u001b[7m\u001b[0m"u8.ToArray());
    }

    /// <summary>
    /// Verifies default, indexed, and RGB foreground encoding.
    /// </summary>
    [Fact]
    public void Foreground_WhenColorIsValid_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new Writer(destination);

        Sgr.Foreground(writer, Color.Default);
        Sgr.Foreground(writer, Color.Indexed(123));
        Sgr.Foreground(writer, Color.Rgb(1, 2, 3));

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[39m\u001b[38;5;123m\u001b[38;2;1;2;3m"u8.ToArray());
    }

    /// <summary>
    /// Verifies default, indexed, and RGB background encoding.
    /// </summary>
    [Fact]
    public void Background_WhenColorIsValid_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new Writer(destination);

        Sgr.Background(writer, Color.Default);
        Sgr.Background(writer, Color.Indexed(255));
        Sgr.Background(writer, Color.Rgb(254, 253, 252));

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[49m\u001b[48;5;255m\u001b[48;2;254;253;252m"u8.ToArray());
    }

    /// <summary>
    /// Verifies color and attribute validation before output.
    /// </summary>
    [Fact]
    public void Command_WhenValueIsInvalid_ThrowsBeforeWriting()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new Writer(destination);

        _ = Should.Throw<ArgumentOutOfRangeException>(
            static () => Color.Indexed(-1));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            static () => Color.Indexed(256));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            static () => Color.Rgb(-1, 0, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Sgr.Apply(writer, (Rendition) 999));

        destination.WrittenCount.ShouldBe(0);
    }
}
