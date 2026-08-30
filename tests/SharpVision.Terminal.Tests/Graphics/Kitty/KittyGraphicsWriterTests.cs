// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Kitty;

using System.IO.Compression;

using SharpVision.Terminal.Kitty.Graphics;
using SharpVision.Terminal.Tests.Support;

/// <summary>Proves canonical bounded Kitty graphics command encoding.</summary>
public sealed class KittyGraphicsWriterTests
{
    #region Encoding and chunking

    /// <summary>Verifies the official direct RGB capability query bytes exactly.</summary>
    [Fact]
    public void Write_WhenQueryIsDirectRgb_EmitsOfficialExample()
    {
        var output = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.Write(KittyGraphicsCommand.Query(31), [0, 0, 0], output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies direct RGBA transmission carries dimensions, identity, and quiet mode.</summary>
    [Fact]
    public void WriteTransmission_WhenRgbaFitsOneChunk_EmitsCanonicalBytes()
    {
        var output = new ArrayBufferWriter<byte>();
        var command = KittyGraphicsCommand.Transmit(
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
        var command = KittyGraphicsCommand.Transmit(
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
        var command = KittyGraphicsCommand.Transmit(9, new Size(1, 1), KittyGraphicsFormat.Png);

        KittyGraphicsWriter.WriteTransmission(command, payload, output);

        var bytes = output.WrittenSpan;
        bytes.IndexOf("\u001b_Gm=0,q=2;"u8).ShouldBeGreaterThan(0);
        bytes[..(bytes.IndexOf((byte) ';') + 1)].ToArray().ShouldBe(
            "\u001b_Ga=t,f=100,t=d,i=9,m=1,q=2;"u8.ToArray());
    }

    /// <summary>Verifies direct RGB transmission carries the three-byte-per-pixel shape and f=24,
    /// proving RGB payload validation and framing work now that transmission accepts it.</summary>
    [Fact]
    public void WriteTransmission_WhenRgbFitsOneChunk_EmitsCanonicalBytes()
    {
        var output = new ArrayBufferWriter<byte>();
        var command = KittyGraphicsCommand.Transmit(
            imageId: 10,
            new Size(1, 1),
            KittyGraphicsFormat.Rgb,
            quiet: 2);

        KittyGraphicsWriter.WriteTransmission(command, [1, 2, 3], output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=t,f=24,t=d,s=1,v=1,i=10,q=2;AQID\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies zlib-compressed transmission emits the <c>o=z</c> field and bytes that are
    /// actually smaller than the raw payload, decompressing back to the exact original pixels.</summary>
    [Fact]
    public void WriteTransmission_WhenCompressed_EmitsCompressionFieldAndActuallyCompressedBytes()
    {
        var output = new ArrayBufferWriter<byte>();
        var size = new Size(16, 16);
        var raw = new byte[16 * 16 * 4];
        raw.AsSpan().Fill(7);
        var command = KittyGraphicsCommand.Transmit(
            imageId: 11,
            size,
            KittyGraphicsFormat.Rgba,
            quiet: 2,
            compression: KittyGraphicsCompression.Zlib);

        KittyGraphicsWriter.WriteTransmission(command, raw, output);

        var bytes = output.WrittenSpan;
        var semicolon = bytes.IndexOf((byte) ';');
        bytes[..semicolon].ToArray().ShouldBe(
            "\u001b_Ga=t,f=32,t=d,o=z,s=16,v=16,i=11,q=2"u8.ToArray());

        var decoded = Convert.FromBase64String(Encoding.ASCII.GetString(bytes[(semicolon + 1)..^2]));
        decoded.Length.ShouldBeLessThan(raw.Length);

        using var decompressed = new MemoryStream();

        using (var zlib = new ZLibStream(new MemoryStream(decoded), CompressionMode.Decompress))
        {
            zlib.CopyTo(decompressed);
        }

        decompressed.ToArray().ShouldBe(raw);
    }

    /// <summary>Verifies compressing and chunking the same payload twice produces byte-identical
    /// output, proving the fixed compression level makes <c>WriteTransmission</c> deterministic just
    /// like <c>Graphics.Png.Encode</c>.</summary>
    [Fact]
    public void WriteTransmission_WhenCompressedAndCalledTwice_ProducesByteIdenticalOutput()
    {
        var size = new Size(16, 16);
        var raw = new byte[16 * 16 * 4];
        Random.Shared.NextBytes(raw);
        var command = KittyGraphicsCommand.Transmit(
            imageId: 12,
            size,
            KittyGraphicsFormat.Rgba,
            compression: KittyGraphicsCompression.Zlib);
        var first = new ArrayBufferWriter<byte>();
        var second = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.WriteTransmission(command, raw, first);
        KittyGraphicsWriter.WriteTransmission(command, raw, second);

        first.WrittenSpan.ToArray().ShouldBe(second.WrittenSpan.ToArray());
    }

    /// <summary>Verifies the two lower-level entry points reject a zlib-compressed command, since
    /// neither can compress a raw payload or validate an already-compressed one: only
    /// <c>WriteTransmission</c> owns the compression step.</summary>
    [Fact]
    public void Write_WhenCommandIsCompressed_ThrowsArgumentException()
    {
        var size = new Size(1, 1);
        var raw = new byte[4];
        var command = KittyGraphicsCommand.Transmit(
            imageId: 13,
            size,
            KittyGraphicsFormat.Rgba,
            compression: KittyGraphicsCompression.Zlib);

        _ = Should.Throw<ArgumentException>(() => KittyGraphicsWriter.Write(command, raw, new ArrayBufferWriter<byte>()));
    }

    /// <summary>Verifies <c>WriteEncoded</c> also rejects a zlib-compressed command for the same
    /// reason as <c>Write</c>.</summary>
    [Fact]
    public void WriteEncoded_WhenCommandIsCompressed_ThrowsArgumentException()
    {
        var size = new Size(1, 1);
        var encoded = Convert.ToBase64String(new byte[4]);
        var command = KittyGraphicsCommand.Transmit(
            imageId: 14,
            size,
            KittyGraphicsFormat.Rgba,
            compression: KittyGraphicsCompression.Zlib);

        _ = Should.Throw<ArgumentException>(() => KittyGraphicsWriter.WriteEncoded(
            command,
            Encoding.ASCII.GetBytes(encoded),
            new ArrayBufferWriter<byte>()));
    }

    #endregion

    #region Commands and policy

    /// <summary>Verifies placement encodes source, destination, z-index, quiet, and C=1.</summary>
    [Fact]
    public void Write_WhenPlacementIsComplete_EmitsExactBytes()
    {
        var output = new ArrayBufferWriter<byte>();
        var command = KittyGraphicsCommand.Place(
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
            KittyGraphicsCommand.Place(7, 9, new Rect(0, 0, 1, 1), new Size(2, 3)),
            [],
            output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=p,i=7,p=9,x=0,y=0,w=1,h=1,c=2,r=3,q=2,C=1\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies a virtual placement emits U=1 without cursor-movement policy.</summary>
    [Fact]
    public void Write_WhenPlacementUsesUnicodePlaceholders_EmitsExactVirtualPlacementBytes()
    {
        var output = new ArrayBufferWriter<byte>();
        var command = KittyGraphicsCommand.Place(
            imageId: 7,
            placementId: 9,
            new Rect(1, 2, 3, 4),
            new Size(5, 6),
            unicodePlaceholder: true);

        KittyGraphicsWriter.Write(command, [], output);

        command.IsUnicodePlaceholder.ShouldBeTrue();
        command.DoNotMoveCursor.ShouldBeFalse();
        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=p,i=7,p=9,x=1,y=2,w=3,h=4,c=5,r=6,U=1,q=2\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies hard image deletion frees data for the exact identifier.</summary>
    [Fact]
    public void Write_WhenImageIsDeleted_EmitsExactHardDelete()
    {
        var output = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.Write(KittyGraphicsCommand.DeleteImage(7), [], output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=d,d=I,i=7,q=2\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies a soft exact-placement delete preserves shared image data.</summary>
    [Fact]
    public void Write_WhenPlacementIsDeleted_EmitsExactSoftDelete()
    {
        var output = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.Write(KittyGraphicsCommand.DeletePlacement(7, 9), [], output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=d,d=i,i=7,p=9,q=2\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies a number-addressed hard image deletion emits the spec-mandated 'N' value
    /// (paired with the 'I' number key), not the id-addressed 'I' value.</summary>
    [Fact]
    public void Write_WhenNumberAddressedImageIsDeleted_EmitsNumberAddressedHardDelete()
    {
        var output = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.Write(KittyGraphicsCommand.DeleteImage(7).WithImageNumber(), [], output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=d,d=N,I=7,q=2\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies a number-addressed soft placement deletion emits the spec-mandated 'n' value
    /// (paired with the 'I' number key), not the id-addressed 'i' value.</summary>
    [Fact]
    public void Write_WhenNumberAddressedPlacementIsDeleted_EmitsNumberAddressedSoftDelete()
    {
        var output = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.Write(KittyGraphicsCommand.DeletePlacement(7, 9).WithImageNumber(), [], output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=d,d=n,I=7,p=9,q=2\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies a number-addressed transmission can be written with quiet=0 so the terminal's
    /// OK response - the only way the caller learns the terminal-assigned id for that number - is not
    /// suppressed.</summary>
    [Fact]
    public void WriteTransmission_WhenNumberAddressedWithQuietZero_EmitsUnsuppressedResponse()
    {
        var output = new ArrayBufferWriter<byte>();
        var command = KittyGraphicsCommand.Transmit(
            7,
            new Size(1, 1),
            KittyGraphicsFormat.Rgba,
            quiet: 0).WithImageNumber();

        KittyGraphicsWriter.WriteTransmission(command, [1, 2, 3, 255], output, maxPayloadBytes: 64);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=t,f=32,t=d,s=1,v=1,I=7;AQID/w==\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies direct-only policy rejects file, temporary-file, and shared-memory media.</summary>
    [Theory]
    [InlineData(KittyGraphicsMedium.File)]
    [InlineData(KittyGraphicsMedium.TemporaryFile)]
    [InlineData(KittyGraphicsMedium.SharedMemory)]
    public void Transmit_WhenMediumIsNotDirect_ThrowsNotSupportedException(KittyGraphicsMedium medium)
    {
        _ = Should.Throw<NotSupportedException>(() => KittyGraphicsCommand.Transmit(
            1,
            new Size(1, 1),
            KittyGraphicsFormat.Rgba,
            medium: medium));
    }

    /// <summary>Verifies an unrecognized format or compression enum value cannot enter the writer,
    /// now that RGB format and zlib compression are themselves both accepted.</summary>
    [Fact]
    public void Transmit_WhenFormatOrCompressionIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => KittyGraphicsCommand.Transmit(
            1,
            new Size(1, 1),
            (KittyGraphicsFormat) (-1)));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => KittyGraphicsCommand.Transmit(
            1,
            new Size(1, 1),
            KittyGraphicsFormat.Rgba,
            compression: (KittyGraphicsCompression) (-1)));
    }

    #endregion

    #region Frame transmission and animation control

    /// <summary>Verifies minimal frame transmission bytes carry the frame action, dimensions, and identity.</summary>
    [Fact]
    public void WriteTransmission_WhenFrameIsMinimal_EmitsCanonicalBytes()
    {
        var output = new ArrayBufferWriter<byte>();
        var command = KittyGraphicsCommand.TransmitFrame(
            imageId: 7,
            new Size(1, 1),
            KittyGraphicsFormat.Rgba,
            quiet: 2);

        KittyGraphicsWriter.WriteTransmission(command, [1, 2, 3, 4], output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=f,f=32,t=d,s=1,v=1,i=7,q=2;AQIDBA==\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies frame composition fields encode base frame, offset, and gap exactly.</summary>
    [Fact]
    public void WriteTransmission_WhenFrameComposesOnBaseFrame_EmitsExactCompositionFields()
    {
        var output = new ArrayBufferWriter<byte>();
        var command = KittyGraphicsCommand.TransmitFrame(
            imageId: 7,
            new Size(1, 1),
            KittyGraphicsFormat.Rgba,
            baseFrameId: 3,
            offset: new Point(2, 4),
            frameGap: 40,
            quiet: 2);

        KittyGraphicsWriter.WriteTransmission(command, [1, 2, 3, 4], output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=f,f=32,t=d,s=1,v=1,c=3,x=2,y=4,z=40,i=7,q=2;AQIDBA==\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies a negative frame gap is preserved verbatim as a gapless frame marker.</summary>
    [Fact]
    public void WriteTransmission_WhenFrameGapIsNegative_EmitsGaplessMarker()
    {
        var output = new ArrayBufferWriter<byte>();
        var command = KittyGraphicsCommand.TransmitFrame(
            imageId: 7,
            new Size(1, 1),
            KittyGraphicsFormat.Rgba,
            frameGap: -1,
            quiet: 2);

        KittyGraphicsWriter.WriteTransmission(command, [1, 2, 3, 4], output);

        output.WrittenSpan.ToArray().ShouldBe(
            "\u001b_Ga=f,f=32,t=d,s=1,v=1,z=-1,i=7,q=2;AQIDBA==\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies large raw frame data chunks exactly like a normal transmission, with minimal continuation.</summary>
    [Fact]
    public void WriteTransmission_WhenFramePayloadNeedsChunks_UsesFiniteOfficialChunkGrammar()
    {
        var output = new ArrayBufferWriter<byte>();
        var payload = new byte[4_096];
        payload.AsSpan().Fill(1);
        var command = KittyGraphicsCommand.TransmitFrame(
            imageId: 8,
            new Size(1_024, 1),
            KittyGraphicsFormat.Rgba,
            quiet: 1);

        KittyGraphicsWriter.WriteTransmission(command, payload, output);

        var bytes = output.WrittenSpan;
        var second = bytes.IndexOf("\u001b_Ga=f,m=0,q=1;"u8);
        second.ShouldBeGreaterThan(0);
        var firstPayloadStart = bytes.IndexOf((byte) ';') + 1;
        var firstPayloadEnd = bytes[..second].LastIndexOf("\u001b\\"u8);
        (firstPayloadEnd - firstPayloadStart).ShouldBe(4_096);
        ((firstPayloadEnd - firstPayloadStart) & 3).ShouldBe(0);
        bytes[..firstPayloadStart].ToArray().ShouldBe(
            "\u001b_Ga=f,f=32,t=d,s=1024,v=1,i=8,m=1,q=1;"u8.ToArray());
    }

    /// <summary>Verifies a structurally valid large PNG frame is chunked without bypassing validation.</summary>
    [Fact]
    public void WriteTransmission_WhenValidPngFrameNeedsChunks_ValidatesAndChunksPayload()
    {
        var output = new ArrayBufferWriter<byte>();
        var payload = CreatePngPayload(3_073);
        var command = KittyGraphicsCommand.TransmitFrame(9, new Size(1, 1), KittyGraphicsFormat.Png);

        KittyGraphicsWriter.WriteTransmission(command, payload, output);

        var bytes = output.WrittenSpan;
        bytes.IndexOf("\u001b_Ga=f,m=0,q=2;"u8).ShouldBeGreaterThan(0);
        bytes[..(bytes.IndexOf((byte) ';') + 1)].ToArray().ShouldBe(
            "\u001b_Ga=f,f=100,t=d,i=9,m=1,q=2;"u8.ToArray());
    }

    /// <summary>Verifies direct-only policy rejects file, temporary-file, and shared-memory media for frames.</summary>
    [Theory]
    [InlineData(KittyGraphicsMedium.File)]
    [InlineData(KittyGraphicsMedium.TemporaryFile)]
    [InlineData(KittyGraphicsMedium.SharedMemory)]
    public void TransmitFrame_WhenMediumIsNotDirect_ThrowsNotSupportedException(KittyGraphicsMedium medium)
    {
        _ = Should.Throw<NotSupportedException>(() => KittyGraphicsCommand.TransmitFrame(
            1,
            new Size(1, 1),
            KittyGraphicsFormat.Rgba,
            medium: medium));
    }

    /// <summary>Verifies unsupported RGB frame data and zlib metadata cannot enter the writer.</summary>
    [Fact]
    public void TransmitFrame_WhenFormatOrCompressionIsUnsupported_ThrowsNotSupportedException()
    {
        _ = Should.Throw<NotSupportedException>(() => KittyGraphicsCommand.TransmitFrame(
            1,
            new Size(1, 1),
            KittyGraphicsFormat.Rgb));
        _ = Should.Throw<NotSupportedException>(() => KittyGraphicsCommand.TransmitFrame(
            1,
            new Size(1, 1),
            KittyGraphicsFormat.Rgba,
            compression: KittyGraphicsCompression.Zlib));
    }

    /// <summary>Verifies a negative frame offset is rejected before output.</summary>
    [Fact]
    public void TransmitFrame_WhenOffsetIsNegative_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => KittyGraphicsCommand.TransmitFrame(
            1,
            new Size(1, 1),
            KittyGraphicsFormat.Rgba,
            offset: new Point(-1, 0)));
    }

    /// <summary>Verifies animation control encodes the sub-action, image identity, and quiet mode exactly.</summary>
    [Fact]
    public void Write_WhenAnimationRunsWithoutLoopOverride_EmitsExactBytes()
    {
        var output = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.Write(
            KittyGraphicsCommand.Animate(7, KittyGraphicsAnimationControl.Run),
            [],
            output);

        output.WrittenSpan.ToArray().ShouldBe("\u001b_Ga=a,i=7,s=3,q=2\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies a number-addressed animation command emits the 'I' number key, not the
    /// id-addressed 'i' key, matching every other action.</summary>
    [Fact]
    public void Write_WhenNumberAddressedAnimationRuns_EmitsNumberAddressedBytes()
    {
        var output = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.Write(
            KittyGraphicsCommand.Animate(7, KittyGraphicsAnimationControl.Run).WithImageNumber(),
            [],
            output);

        output.WrittenSpan.ToArray().ShouldBe("\u001b_Ga=a,I=7,s=3,q=2\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies a nonzero loop count and suppressed quiet mode both encode exactly.</summary>
    [Fact]
    public void Write_WhenAnimationStopsWithLoopCountAndByteQuiet_EmitsExactBytes()
    {
        var output = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.Write(
            KittyGraphicsCommand.Animate(7, KittyGraphicsAnimationControl.Stop, loopCount: 5, quiet: 0),
            [],
            output);

        output.WrittenSpan.ToArray().ShouldBe("\u001b_Ga=a,i=7,s=1,v=5\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies the wait-for-new-frames sub-action encodes its distinct value.</summary>
    [Fact]
    public void Write_WhenAnimationWaitsForNewFrames_EmitsExactBytes()
    {
        var output = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.Write(
            KittyGraphicsCommand.Animate(7, KittyGraphicsAnimationControl.WaitForNewFrames),
            [],
            output);

        output.WrittenSpan.ToArray().ShouldBe("\u001b_Ga=a,i=7,s=2,q=2\u001b\\"u8.ToArray());
    }

    /// <summary>Verifies animation control cannot carry payload data.</summary>
    [Fact]
    public void Write_WhenAnimationCarriesPayload_ThrowsArgumentException()
    {
        var output = new ArrayBufferWriter<byte>();

        _ = Should.Throw<ArgumentException>(() => KittyGraphicsWriter.Write(
            KittyGraphicsCommand.Animate(7, KittyGraphicsAnimationControl.Run),
            [1],
            output));
    }

    /// <summary>Verifies an undefined control sub-action and a negative loop count are rejected before output.</summary>
    [Fact]
    public void Animate_WhenControlOrLoopCountIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => KittyGraphicsCommand.Animate(1, (KittyGraphicsAnimationControl) 99));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => KittyGraphicsCommand.Animate(1, KittyGraphicsAnimationControl.Run, loopCount: -1));
    }

    /// <summary>Verifies animation control rejects encoded-payload writes, matching placement and delete.</summary>
    [Fact]
    public void WriteEncoded_WhenActionIsAnimate_WritesNothing()
    {
        var output = new ArrayBufferWriter<byte>();

        _ = Should.Throw<ArgumentException>(() => KittyGraphicsWriter.WriteEncoded(
            KittyGraphicsCommand.Animate(1, KittyGraphicsAnimationControl.Run),
            "AAAA"u8,
            output));

        output.WrittenCount.ShouldBe(0);
    }

    #endregion

    #region Checked encoded input

    /// <summary>Verifies malformed pre-encoded Base64 never mutates the destination.</summary>
    [Fact]
    public void WriteEncoded_WhenPayloadIsInvalidBase64_WritesNothing()
    {
        var output = new ArrayBufferWriter<byte>();
        var command = KittyGraphicsCommand.Transmit(1, new Size(1, 1), KittyGraphicsFormat.Rgba);

        _ = Should.Throw<ArgumentException>(() => KittyGraphicsWriter.WriteEncoded(command, "***"u8, output));

        output.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies canonical encoded query and RGBA payloads retain exact bytes.</summary>
    [Fact]
    public void WriteEncoded_WhenPayloadIsCanonicalAndMatchesCommand_EmitsExactBytes()
    {
        var output = new ArrayBufferWriter<byte>();

        KittyGraphicsWriter.WriteEncoded(KittyGraphicsCommand.Query(31), "AAAA"u8, output);
        KittyGraphicsWriter.WriteEncoded(
            KittyGraphicsCommand.Transmit(1, new Size(1, 1), KittyGraphicsFormat.Rgba),
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
    [InlineData("rgb-shape")]
    [InlineData("png-shape")]
    public void WriteEncoded_WhenPayloadOrActionIsUnsafe_WritesNothing(string scenario)
    {
        var output = new ArrayBufferWriter<byte>();
        var command = scenario switch
        {
            "place" => KittyGraphicsCommand.Place(1, 1, new Rect(0, 0, 1, 1), new Size(1, 1)),
            "delete" => KittyGraphicsCommand.DeleteImage(1),
            "query-shape" => KittyGraphicsCommand.Query(31),
            "rgb-shape" => KittyGraphicsCommand.Transmit(1, new Size(1, 1), KittyGraphicsFormat.Rgb),
            "png-shape" => KittyGraphicsCommand.Transmit(1, new Size(1, 1), KittyGraphicsFormat.Png),
            _ => KittyGraphicsCommand.Transmit(1, new Size(1, 1), KittyGraphicsFormat.Rgba)
        };
        var payload = scenario switch
        {
            "malformed" => "***"u8.ToArray(),
            "noncanonical" => "AQIDBB=="u8.ToArray(),
            "oversized" => [.. Enumerable.Repeat((byte) 'A', 4_097)],
            "query-shape" => "AA=="u8.ToArray(),
            "rgba-shape" => "AQID"u8.ToArray(),
            "rgb-shape" => "AQIDBA=="u8.ToArray(),
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

        _ = Should.Throw<ArgumentOutOfRangeException>(() => KittyGraphicsCommand.Transmit(
            1,
            new Size(16_385, 1),
            KittyGraphicsFormat.Rgba));
        _ = Should.Throw<ArgumentException>(() => KittyGraphicsWriter.WriteTransmission(
            KittyGraphicsCommand.Transmit(1, new Size(1, 1), KittyGraphicsFormat.Rgba),
            [1, 2, 3],
            output));
        _ = Should.Throw<ArgumentException>(() => KittyGraphicsWriter.WriteTransmission(
            KittyGraphicsCommand.Transmit(1, new Size(1, 1), KittyGraphicsFormat.Png),
            new byte[17],
            output,
            maxPayloadBytes: 16));

        output.WrittenCount.ShouldBe(0);
    }

    #endregion

    #region Helpers

    private static byte[] CreatePngPayload(int dataBytes) => PngTestData.CreateContainer(dataBytes);

    #endregion
}
