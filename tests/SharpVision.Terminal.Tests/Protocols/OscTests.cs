// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>
/// Verifies typed operating system command encoding.
/// </summary>
public sealed class OscTests
{
    /// <summary>
    /// Verifies icon/title and title-only selectors.
    /// </summary>
    [Fact]
    public void Title_WhenTextIsValid_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        Osc.IconAndTitle(writer, "both"u8);
        Osc.Title(writer, "title"u8);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b]0;both\u001b\\\u001b]2;title\u001b\\"u8.ToArray());
    }

    /// <summary>
    /// Verifies body-only and titled notification payload grammar.
    /// </summary>
    [Fact]
    public void Notify_WhenTextIsValid_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        Osc.Notify(writer, "body only"u8);
        Osc.Notify(writer, "title"u8, "body"u8);

        byte[] expected = [.. WrapOsc("9", "body only"), .. WrapOsc("777", "notify;title;body")];
        destination.WrittenSpan.ToArray().ShouldBe(expected);
    }

    /// <summary>
    /// Verifies a titled notification payload exceeding the stack limit rents from the array pool.
    /// </summary>
    [Fact]
    public void Notify_WhenTitledPayloadExceedsStackLimit_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);
        var title = new string('t', 300);
        var body = new string('b', 300);

        Osc.Notify(writer, Encoding.UTF8.GetBytes(title), Encoding.UTF8.GetBytes(body));

        destination.WrittenSpan.ToArray().ShouldBe(WrapOsc("777", $"notify;{title};{body}"));
    }

    /// <summary>
    /// Verifies both notification overloads reject control bytes and DEL before writing.
    /// </summary>
    [Fact]
    public void Notify_WhenValueIsInvalid_ThrowsBeforeWriting()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        _ = Should.Throw<ArgumentException>(() => Osc.Notify(writer, [(byte) 'a', 0x07]));
        _ = Should.Throw<ArgumentException>(() => Osc.Notify(writer, [(byte) 'a', 0x7f]));
        _ = Should.Throw<ArgumentException>(() => Osc.Notify(writer, [0x07], "body"u8));
        _ = Should.Throw<ArgumentException>(() => Osc.Notify(writer, "title"u8, [0x7f]));

        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>
    /// Verifies a literal <c>;</c> byte in the title is rejected before writing: the urxvt/foot
    /// receiver splits the OSC 777 payload once after the "notify;" prefix, so an embedded ';'
    /// in the title would shift that boundary and truncate the title.
    /// </summary>
    [Fact]
    public void Notify_WhenTitleContainsSemicolon_ThrowsBeforeWriting()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        _ = Should.Throw<ArgumentException>(() => Osc.Notify(writer, "ti;tle"u8, "body"u8));

        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>
    /// Verifies a literal <c>;</c> byte in the body is accepted and written through unchanged:
    /// the receiver treats everything after the first split as the body, so further ';' bytes
    /// there are safe under the urxvt/foot convention.
    /// </summary>
    [Fact]
    public void Notify_WhenBodyContainsSemicolon_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        Osc.Notify(writer, "title"u8, "bo;dy"u8);

        destination.WrittenSpan.ToArray().ShouldBe(WrapOsc("777", "notify;title;bo;dy"));
    }

    /// <summary>
    /// Builds the exact bytes a decimal-selector OSC command with the given payload should
    /// produce: ESC, ']', the selector, ';', the UTF-8 payload, and the ST terminator.
    /// </summary>
    private static byte[] WrapOsc(string selector, string payload)
    {
        List<byte> bytes = [0x1b, (byte) ']'];
        bytes.AddRange(Encoding.ASCII.GetBytes(selector));
        bytes.Add((byte) ';');
        bytes.AddRange(Encoding.UTF8.GetBytes(payload));
        bytes.Add(0x1b);
        bytes.Add(0x5c);
        return [.. bytes];
    }

    /// <summary>
    /// Verifies hyperlink open and close payload grammar.
    /// </summary>
    [Fact]
    public void Hyperlink_WhenValuesAreValid_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        Osc.OpenHyperlink(writer, "https://example.test"u8, "docs"u8);
        Osc.CloseHyperlink(writer);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b]8;id=docs;https://example.test\u001b\\\u001b]8;;\u001b\\"u8.ToArray());
    }

    /// <summary>
    /// Verifies a uri-only hyperlink payload exceeding the stack limit rents from the array pool.
    /// </summary>
    [Fact]
    public void Hyperlink_WhenUriExceedsStackLimit_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);
        var uri = new string('a', 600);

        Osc.OpenHyperlink(writer, Encoding.UTF8.GetBytes(uri));
        Osc.CloseHyperlink(writer);

        destination.WrittenSpan.ToArray().ShouldBe(
            Encoding.UTF8.GetBytes($"\u001b]8;;{uri}\u001b\\\u001b]8;;\u001b\\"));
    }

    /// <summary>
    /// Verifies a combined id and uri hyperlink payload exceeding the stack limit rents from the array pool.
    /// </summary>
    [Fact]
    public void Hyperlink_WhenIdAndUriExceedStackLimit_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);
        var id = new string('i', 300);
        var uri = new string('u', 300);

        Osc.OpenHyperlink(writer, Encoding.UTF8.GetBytes(uri), Encoding.UTF8.GetBytes(id));
        Osc.CloseHyperlink(writer);

        destination.WrittenSpan.ToArray().ShouldBe(
            Encoding.UTF8.GetBytes($"\u001b]8;id={id};{uri}\u001b\\\u001b]8;;\u001b\\"));
    }

    /// <summary>
    /// Verifies palette and default-color queries.
    /// </summary>
    [Fact]
    public void ColorQuery_WhenIndexIsValid_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        Osc.QueryPalette(writer, 15);
        Osc.QueryForeground(writer);
        Osc.QueryBackground(writer);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b]4;15;?\u001b\\\u001b]10;?\u001b\\\u001b]11;?\u001b\\"u8.ToArray());
    }

    /// <summary>
    /// Verifies OSC text and index validation before output.
    /// </summary>
    [Fact]
    public void Command_WhenValueIsInvalid_ThrowsBeforeWriting()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        _ = Should.Throw<ArgumentException>(() => Osc.Title(writer, [(byte) 'a', 0x07]));
        _ = Should.Throw<ArgumentException>(() => Osc.OpenHyperlink(writer, "https://example.test"u8, "bad:id"u8));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Osc.QueryPalette(writer, 256));

        destination.WrittenCount.ShouldBe(0);
    }
}
