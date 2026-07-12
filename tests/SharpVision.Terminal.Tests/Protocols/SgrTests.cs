using System.Buffers;

using SharpVision.Terminal.Protocols;

using Shouldly;

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>
/// Verifies typed Select Graphic Rendition encoding.
/// </summary>
public sealed class SgrTests
{
    /// <summary>Provides every xterm underline variant and exact subparameter form.</summary>
    public static TheoryData<Underline, string> UnderlineCases => new()
    {
        { Underline.None, "\u001b[4:0m" },
        { Underline.Straight, "\u001b[4:1m" },
        { Underline.Paired, "\u001b[4:2m" },
        { Underline.Curly, "\u001b[4:3m" },
        { Underline.Dotted, "\u001b[4:4m" },
        { Underline.Dashed, "\u001b[4:5m" },
    };

    /// <summary>Provides all typed basic-color foreground and background sequences.</summary>
    public static TheoryData<BasicColor, string, string> BasicColorCases => new()
    {
        { BasicColor.Black, "\u001b[30m", "\u001b[40m" },
        { BasicColor.Red, "\u001b[31m", "\u001b[41m" },
        { BasicColor.Green, "\u001b[32m", "\u001b[42m" },
        { BasicColor.Yellow, "\u001b[33m", "\u001b[43m" },
        { BasicColor.Blue, "\u001b[34m", "\u001b[44m" },
        { BasicColor.Magenta, "\u001b[35m", "\u001b[45m" },
        { BasicColor.Cyan, "\u001b[36m", "\u001b[46m" },
        { BasicColor.White, "\u001b[37m", "\u001b[47m" },
        { BasicColor.BrightBlack, "\u001b[90m", "\u001b[100m" },
        { BasicColor.BrightRed, "\u001b[91m", "\u001b[101m" },
        { BasicColor.BrightGreen, "\u001b[92m", "\u001b[102m" },
        { BasicColor.BrightYellow, "\u001b[93m", "\u001b[103m" },
        { BasicColor.BrightBlue, "\u001b[94m", "\u001b[104m" },
        { BasicColor.BrightMagenta, "\u001b[95m", "\u001b[105m" },
        { BasicColor.BrightCyan, "\u001b[96m", "\u001b[106m" },
        { BasicColor.BrightWhite, "\u001b[97m", "\u001b[107m" },
    };

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
    [InlineData(Rendition.Overline, "\u001b[53m")]
    [InlineData(Rendition.NotOverline, "\u001b[55m")]
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

    /// <summary>Verifies every basic color uses classic ANSI/aixterm SGR forms.</summary>
    /// <param name="color">The typed basic color.</param>
    /// <param name="foreground">The exact foreground sequence.</param>
    /// <param name="background">The exact background sequence.</param>
    [Theory]
    [MemberData(nameof(BasicColorCases))]
    public void BasicColor_WhenValueIsKnown_WritesExactForegroundAndBackground(
        BasicColor color,
        string foreground,
        string background)
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new Writer(destination);

        Sgr.Foreground(writer, color);
        Sgr.Background(writer, color);

        destination.WrittenSpan.ToArray().ShouldBe(
            System.Text.Encoding.ASCII.GetBytes(foreground + background));
    }

    /// <summary>Verifies every underline variant uses xterm's typed 4:x form.</summary>
    /// <param name="underline">The typed underline variant.</param>
    /// <param name="expected">The exact expected sequence.</param>
    [Theory]
    [MemberData(nameof(UnderlineCases))]
    public void Underline_WhenVariantIsKnown_WritesExactSubparameter(
        Underline underline,
        string expected)
    {
        var destination = new ArrayBufferWriter<byte>();

        Sgr.Apply(new Writer(destination), underline);

        destination.WrittenSpan.ToArray().ShouldBe(
            System.Text.Encoding.ASCII.GetBytes(expected));
    }

    /// <summary>Verifies default, indexed, and RGB underline-color output.</summary>
    [Fact]
    public void UnderlineColor_WhenColorIsValid_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new Writer(destination);

        Sgr.UnderlineColor(writer, Color.Default);
        Sgr.UnderlineColor(writer, Color.Indexed(123));
        Sgr.UnderlineColor(writer, Color.Rgb(1, 2, 3));

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[59m\u001b[58;5;123m\u001b[58;2;1;2;3m"u8.ToArray());
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
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Sgr.Foreground(writer, (BasicColor) 999));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Sgr.Background(writer, (BasicColor) 999));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Sgr.Apply(writer, (Underline) 999));

        destination.WrittenCount.ShouldBe(0);
    }
}
