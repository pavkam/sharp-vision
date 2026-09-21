// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Clipboard;

using Kitty.Clipboard;

/// <summary>
/// Verifies exact Kitty OSC 5522 request and data encoding.
/// </summary>
public sealed class KittyClipboardWriterTests
{
    /// <summary>
    /// Verifies MIME read and list requests.
    /// </summary>
    [Fact]
    public void Read_WhenValuesAreValid_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        KittyClipboardWriter.Read(writer, "text/plain image/png"u8, id: "req-1"u8);
        KittyClipboardWriter.List(writer, Selection.Primary);

        destination.WrittenSpan.ToArray().ShouldBe(
            Encoding.ASCII.GetBytes(
                "\u001b]5522;type=read:id=req-1;dGV4dC9wbGFpbiBpbWFnZS9wbmc=\u001b\\" +
                "\u001b]5522;type=read:loc=primary;Lg==\u001b\\"));
    }

    /// <summary>
    /// Verifies credential, selection, and correlation metadata on write start.
    /// </summary>
    [Fact]
    public void WriteStart_WhenValuesAreValid_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();

        KittyClipboardWriter.WriteStart(
            new ProtocolWriter(destination),
            Selection.Primary,
            "req-1"u8,
            "password"u8,
            "friendly"u8);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b]5522;type=write:loc=primary:id=req-1:pw=cGFzc3dvcmQ=:name=ZnJpZW5kbHk=\u001b\\"u8
                .ToArray());
    }

    /// <summary>
    /// Verifies alias and end-of-write packets.
    /// </summary>
    [Fact]
    public void WriteAliasAndEnd_WhenValuesAreValid_WriteExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        KittyClipboardWriter.WriteAlias(writer, "text/plain"u8, "text/plain text/utf8"u8);
        KittyClipboardWriter.WriteEnd(writer);

        destination.WrittenSpan.ToArray().ShouldBe(
            Encoding.ASCII.GetBytes(
                "\u001b]5522;type=walias:mime=dGV4dC9wbGFpbg==;" +
                "dGV4dC9wbGFpbiB0ZXh0L3V0Zjg=\u001b\\" +
                "\u001b]5522;type=wdata\u001b\\"));
    }

    /// <summary>
    /// Verifies support query and paste-event private mode encoding.
    /// </summary>
    [Fact]
    public void Modes_WhenRequested_WriteExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        KittyClipboardWriter.QuerySupport(writer);
        KittyClipboardWriter.PasteEvents(writer, enabled: true);
        KittyClipboardWriter.PasteEvents(writer, enabled: false);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[?5522$p\u001b[?5522h\u001b[?5522l"u8.ToArray());
    }

    /// <summary>
    /// Verifies a single-chunk small payload produces the exact literal packet.
    /// </summary>
    [Fact]
    public void WriteData_WhenPayloadIsSmall_WritesExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();

        KittyClipboardWriter.WriteData(new ProtocolWriter(destination), "text/plain"u8, "A"u8);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b]5522;type=wdata:mime=dGV4dC9wbGFpbg==;QQ==\u001b\\"u8.ToArray());
    }

    /// <summary>
    /// Verifies every raw chunk is independently padded and no larger than 4096 bytes.
    /// </summary>
    /// <param name="size">The binary MIME data size.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(8192)]
    public void WriteData_WhenSizeVaries_ChunksAndPadsIndependently(int size)
    {
        byte[] data = [.. Enumerable.Range(0, size).Select(static value => (byte) value)];
        var destination = new ArrayBufferWriter<byte>();

        KittyClipboardWriter.WriteData(new ProtocolWriter(destination), "application/octet-stream"u8, data);

        using ProtocolParser parser = new();
        var sink = new RecordingSink();
        parser.Parse(destination.WrittenSpan, ref sink);
        KittyClipboardPacket[] packets =
            [.. sink.Observations.Select(static observation => KittyClipboardPacket.Parse(observation.First))];

        packets.Length.ShouldBe(Math.Max(1, (size + 4095) / 4096));
        packets.ShouldAllBe(static packet => packet.Valid);
        packets.ShouldAllBe(static packet => packet.Operation == KittyClipboardOperation.WriteData);
        packets.ShouldAllBe(static packet => packet.Data.Length <= 4096);
        packets.SelectMany(static packet => packet.Data.ToArray()).ToArray().ShouldBe(data);

        foreach (var observation in sink.Observations)
        {
            var separator = observation.First.LastIndexOf((byte) ';');
            var encoded = observation.First[(separator + 1)..];
            (encoded.Length % 4).ShouldBe(0);

            var decoded = Convert.FromBase64String(Encoding.ASCII.GetString(encoded));

            if (decoded.Length % 3 != 0)
            {
                encoded[^1].ShouldBe((byte) '=');
            }
        }

        var bytes = destination.WrittenSpan;
        var header = "\u001b]5522;type=wdata:mime=YXBwbGljYXRpb24vb2N0ZXQtc3RyZWFt;"u8;
        bytes[..header.Length].ToArray().ShouldBe(header.ToArray());

        var firstChunkRawLength = Math.Min(4096, size);
        var firstChunkEncodedLength = (firstChunkRawLength + 2) / 3 * 4;
        var firstPayloadEnd = header.Length + firstChunkEncodedLength;
        var terminator = "\u001b\\"u8;
        bytes.Slice(firstPayloadEnd, terminator.Length).ToArray().ShouldBe(terminator.ToArray());

        if (size > 4_096)
        {
            var secondPacketStart = firstPayloadEnd + terminator.Length;
            bytes.Slice(secondPacketStart, header.Length).ToArray().ShouldBe(header.ToArray());
        }
    }

    /// <summary>
    /// Verifies invalid arguments fail before any output packet is written.
    /// </summary>
    [Fact]
    public void Write_WhenArgumentIsInvalid_ThrowsBeforeWriting()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        _ = Should.Throw<ArgumentException>(() => KittyClipboardWriter.Read(writer, "text/plain"u8, id: "bad!"u8));
        _ = Should.Throw<ArgumentException>(() => KittyClipboardWriter.WriteData(writer, "bad mime"u8, []));
        _ = Should.Throw<ArgumentException>(() =>
            KittyClipboardWriter.WriteData(writer, "text/plain"u8, "large"u8, maxBytes: 2));
        _ = Should.Throw<ArgumentException>(() => KittyClipboardWriter.WriteStart(writer, name: [0xff]));

        destination.WrittenCount.ShouldBe(0);
    }
}
