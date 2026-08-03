// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Kitty;

using System.Buffers.Binary;

using SharpVision.Terminal.Kitty.Graphics;

/// <summary>Proves canonical bounded Kitty graphics command encoding.</summary>
public sealed class WriterTests
{
    #region Encoding and chunking

    /// <summary>Verifies the official direct RGB capability query bytes exactly.</summary>
    [Fact]
    public void Write_WhenQueryIsDirectRgb_EmitsOfficialExample()
    {
        var output = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.Write(Command.Query(31), [0, 0, 0], output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies direct RGBA transmission carries dimensions, identity, and quiet mode.</summary>
    [Fact]
    public void WriteTransmission_WhenRgbaFitsOneChunk_EmitsCanonicalBytes()
    {
        var output = new ArrayBufferWriter<byte>();
        var command = Command.Transmit(
            imageId: 7,
            new Size(1, 1),
            KittyGraphicsFormat.Rgba,
            quiet: 2);

        KittyGraphicsWriter.WriteTransmission(command, [1, 2, 3, 4], output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=t,f=32,t=d,s=1,v=1,i=7,q=2;AQIDBA==\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies encoded chunks never exceed 4096 bytes and continuation metadata is minimal.</summary>
    [Fact]
    public void WriteTransmission_WhenPayloadNeedsChunks_UsesFiniteOfficialChunkGrammar()
    {
        var output = new ArrayBufferWriter<byte>();
        var payload = new byte[4_096];
        payload.AsSpan().Fill(1);
        var command = Command.Transmit(
            imageId: 8,
            new Size(1_024, 1),
            KittyGraphicsFormat.Rgba,
            quiet: 1);

        KittyGraphicsWriter.WriteTransmission(command, payload, output);

        var bytes = output.WrittenSpan;
        var second = bytes.IndexOf("\u001b_Gm=0,q=1;"u8);
        second.ShouldBeGreaterThan(0);
        var firstPayloadStart = bytes.IndexOf((byte) ';') + 1;
        var firstPayloadEnd = bytes[..second].LastIndexOf("\u001b\\"u8);
        (firstPayloadEnd - firstPayloadStart).ShouldBe(4_096);
        ((firstPayloadEnd - firstPayloadStart) & 3).ShouldBe(0);
        bytes[..firstPayloadStart].ToArray().ShouldBe(
            "\u001b_Ga=t,f=32,t=d,s=1024,v=1,i=8,m=1,q=1;"u8.ToArray());
    }

    /// <summary>Verifies a structurally valid large PNG is chunked without bypassing validation.</summary>
    [Fact]
    public void WriteTransmission_WhenValidPngNeedsChunks_ValidatesAndChunksPayload()
    {
        var output = new ArrayBufferWriter<byte>();
        var payload = CreatePngPayload(3_073);
        var command = Command.Transmit(9, new Size(1, 1), KittyGraphicsFormat.Png);

        KittyGraphicsWriter.WriteTransmission(command, payload, output);

        var bytes = output.WrittenSpan;
        bytes.IndexOf("\u001b_Gm=0,q=2;"u8).ShouldBeGreaterThan(0);
        bytes[..(bytes.IndexOf((byte) ';') + 1)].ToArray().ShouldBe(
            "\u001b_Ga=t,f=100,t=d,i=9,m=1,q=2;"u8.ToArray());
    }

    #endregion

    #region Commands and policy

    /// <summary>Verifies placement encodes source, destination, z-index, quiet, and C=1.</summary>
    [Fact]
    public void Write_WhenPlacementIsComplete_EmitsExactBytes()
    {
        var output = new ArrayBufferWriter<byte>();
        var command = Command.Place(
            imageId: 7,
            placementId: 9,
            new Rect(1, 2, 3, 4),
            new Size(5, 6),
            zIndex: -1,
            quiet: 2,
            doNotMoveCursor: true);

        KittyGraphicsWriter.Write(command, [], output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=p,i=7,p=9,x=1,y=2,w=3,h=4,c=5,r=6,z=-1,q=2,C=1\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies placement replacement uses the same image and placement IDs.</summary>
    [Fact]
    public void Write_WhenPlacementUpdates_ReusesExactIdentityPair()
    {
        var output = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.Write(
            Command.Place(7, 9, new Rect(0, 0, 1, 1), new Size(2, 3)),
            [],
            output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=p,i=7,p=9,x=0,y=0,w=1,h=1,c=2,r=3,q=2,C=1\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies hard image deletion frees data for the exact identifier.</summary>
    [Fact]
    public void Write_WhenImageIsDeleted_EmitsExactHardDelete()
    {
        var output = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.Write(Command.DeleteImage(7), [], output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=d,d=I,i=7,q=2\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies a soft exact-placement delete preserves shared image data.</summary>
    [Fact]
    public void Write_WhenPlacementIsDeleted_EmitsExactSoftDelete()
    {
        var output = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.Write(Command.DeletePlacement(7, 9), [], output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=d,d=i,i=7,p=9,q=2\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies direct-only policy rejects file, temporary-file, and shared-memory media.</summary>
    [Theory]
    [InlineData(KittyGraphicsMedium.File)]
    [InlineData(KittyGraphicsMedium.TemporaryFile)]
    [InlineData(KittyGraphicsMedium.SharedMemory)]
    public void Transmit_WhenMediumIsNotDirect_ThrowsNotSupportedException(KittyGraphicsMedium medium)
    {
        _ = Should.Throw<NotSupportedException>(() => Command.Transmit(
            1,
            new Size(1, 1),
            KittyGraphicsFormat.Rgba,
            medium: medium));
    }

    /// <summary>Verifies unsupported RGB transmission and zlib metadata cannot enter the writer.</summary>
    [Fact]
    public void Transmit_WhenFormatOrCompressionIsUnsupported_ThrowsNotSupportedException()
    {
        _ = Should.Throw<NotSupportedException>(() => Command.Transmit(
            1,
            new Size(1, 1),
            KittyGraphicsFormat.Rgb));
        _ = Should.Throw<NotSupportedException>(() => Command.Transmit(
            1,
            new Size(1, 1),
            KittyGraphicsFormat.Rgba,
            compression: Compression.Zlib));
    }

    #endregion

    #region Checked encoded input

    /// <summary>Verifies malformed pre-encoded Base64 never mutates the destination.</summary>
    [Fact]
    public void WriteEncoded_WhenPayloadIsInvalidBase64_WritesNothing()
    {
        var output = new ArrayBufferWriter<byte>();
        var command = Command.Transmit(1, new Size(1, 1), KittyGraphicsFormat.Rgba);

        _ = Should.Throw<ArgumentException>(() => KittyGraphicsWriter.WriteEncoded(command, "***"u8, output));

        output.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies canonical encoded query and RGBA payloads retain exact bytes.</summary>
    [Fact]
    public void WriteEncoded_WhenPayloadIsCanonicalAndMatchesCommand_EmitsExactBytes()
    {
        var output = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.WriteEncoded(Command.Query(31), "AAAA"u8, output);
        KittyGraphicsWriter.WriteEncoded(
            Command.Transmit(1, new Size(1, 1), KittyGraphicsFormat.Rgba),
            "AQIDBA=="u8,
            output);

        output.WrittenSpan.ToArray().ShouldBe(
            [
                .. "\u001b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\u001b\\"u8,
                .. "\u001b_Ga=t,f=32,t=d,s=1,v=1,i=1,q=2;AQIDBA==\u001b\\"u8
            ]);
    }

    /// <summary>Verifies encoded data is decoded, canonicalized, and shape-checked atomically.</summary>
    [Theory]
    [InlineData("malformed")]
    [InlineData("noncanonical")]
    [InlineData("oversized")]
    [InlineData("place")]
    [InlineData("delete")]
    [InlineData("query-shape")]
    [InlineData("rgba-shape")]
    [InlineData("png-shape")]
    public void WriteEncoded_WhenPayloadOrActionIsUnsafe_WritesNothing(string scenario)
    {
        var output = new ArrayBufferWriter<byte>();
        var command = scenario switch
        {
            "place" => Command.Place(1, 1, new Rect(0, 0, 1, 1), new Size(1, 1)),
            "delete" => Command.DeleteImage(1),
            "query-shape" => Command.Query(31),
            "png-shape" => Command.Transmit(1, new Size(1, 1), KittyGraphicsFormat.Png),
            _ => Command.Transmit(1, new Size(1, 1), KittyGraphicsFormat.Rgba)
        };
        var payload = scenario switch
        {
            "malformed" => "***"u8.ToArray(),
            "noncanonical" => "AQIDBB=="u8.ToArray(),
            "oversized" => [.. Enumerable.Repeat((byte) 'A', 4_097)],
            "query-shape" => "AA=="u8.ToArray(),
            "rgba-shape" => "AQID"u8.ToArray(),
            "png-shape" => "AAAA"u8.ToArray(),
            _ => "AAAA"u8.ToArray()
        };

        _ = Should.Throw<ArgumentException>(() => KittyGraphicsWriter.WriteEncoded(command, payload, output));

        output.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies raw geometry and payload bounds fail before output.</summary>
    [Fact]
    public void WriteTransmission_WhenGeometryOrPayloadIsInvalid_WritesNothing()
    {
        var output = new ArrayBufferWriter<byte>();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => Command.Transmit(
            1,
            new Size(16_385, 1),
            KittyGraphicsFormat.Rgba));
        _ = Should.Throw<ArgumentException>(() => KittyGraphicsWriter.WriteTransmission(
            Command.Transmit(1, new Size(1, 1), KittyGraphicsFormat.Rgba),
            [1, 2, 3],
            output));
        _ = Should.Throw<ArgumentException>(() => KittyGraphicsWriter.WriteTransmission(
            Command.Transmit(1, new Size(1, 1), KittyGraphicsFormat.Png),
            new byte[17],
            output,
            maxPayloadBytes: 16));

        output.WrittenCount.ShouldBe(0);
    }

    #endregion

    #region Helpers

    private static byte[] CreatePngPayload(int dataBytes)
    {
        var payload = new byte[57 + dataBytes];
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        signature.CopyTo(payload);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8), 13);
        "IHDR"u8.CopyTo(payload.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(20), 1);
        payload[24] = 8;
        payload[25] = 6;
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(33), (uint) dataBytes);
        "IDAT"u8.CopyTo(payload.AsSpan(37));
        var iend = 45 + dataBytes;
        "IEND"u8.CopyTo(payload.AsSpan(iend + 4));
        return payload;
    }

    #endregion
}
