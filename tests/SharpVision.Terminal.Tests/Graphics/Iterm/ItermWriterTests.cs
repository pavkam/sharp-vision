// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Iterm;

using System.Buffers.Binary;

using SharpVision.Terminal.Graphics;
using SharpVision.Terminal.Iterm;

/// <summary>Proves canonical bounded iTerm2 3.5 multipart inline-image encoding.</summary>
public sealed class ItermWriterTests
{
    /// <summary>Verifies metadata, PNG Base64, inline policy, omitted name, and canonical ST exactly.</summary>
    [Fact]
    public void Write_WhenPngFitsOnePart_EmitsExactMultipartBytes()
    {
        var image = GraphicsImage.FromPng(CreatePngPayload(dataBytes: 0));
        var output = new ArrayBufferWriter<byte>();

        ItermWriter.Write(image, new Size(2, 3), PlacementMode.Contain, output);

        output.WrittenSpan.ToArray().ShouldBe(Encoding.ASCII.GetBytes(
            "\u001b]1337;MultipartFile=size=57;width=2;height=3;preserveAspectRatio=1;inline=1\u001b\\" +
            "\u001b]1337;FilePart=iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAAAAAAAAAAAElEQVQAAAAAAAAAAElFTkQAAAAA\u001b\\" +
            "\u001b]1337;FileEnd\u001b\\"));
        output.WrittenSpan.IndexOf(";name="u8).ShouldBe(-1);
        output.WrittenSpan.IndexOf(";File="u8).ShouldBe(-1);
        output.WrittenSpan.IndexOf((byte) 0x07).ShouldBe(-1);
    }

    /// <summary>Verifies stretch is the only mode that disables aspect preservation.</summary>
    [Fact]
    public void Write_WhenModeIsStretch_EmitsPreserveAspectRatioZero()
    {
        var image = GraphicsImage.FromPng(CreatePngPayload(dataBytes: 0));
        var output = new ArrayBufferWriter<byte>();

        ItermWriter.Write(image, new Size(1, 1), PlacementMode.Stretch, output);

        output.WrittenSpan.IndexOf(";preserveAspectRatio=0;inline=1\u001b\\"u8)
            .ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>Verifies every part obeys the complete-sequence limit and reconstructs exact PNG bytes.</summary>
    [Fact]
    public void Write_WhenPngNeedsParts_BoundsEveryOscAndReconstructsSource()
    {
        var source = CreatePngPayload(dataBytes: 100);
        var image = GraphicsImage.FromPng(source);
        var output = new ArrayBufferWriter<byte>();

        ItermWriter.Write(
            image,
            new Size(2, 3),
            PlacementMode.Contain,
            output,
            maxSequenceBytes: 96,
            maxOutputBytes: 1_024);

        var frames = SplitFrames(output.WrittenSpan);
        frames.Count.ShouldBe(5);
        frames.ShouldAllBe(frame => frame.Length <= 96);
        frames[0].AsSpan().StartsWith("\u001b]1337;MultipartFile="u8).ShouldBeTrue();
        frames[^1].ShouldBe("\u001b]1337;FileEnd\u001b\\"u8.ToArray());
        var reconstructed = frames
            .Skip(1)
            .SkipLast(1)
            .SelectMany(DecodePart)
            .ToArray();
        reconstructed.ShouldBe(source);
    }

    /// <summary>Verifies the official complete-sequence ceiling derives a finite canonical raw chunk.</summary>
    [Fact]
    public void GetMaximumRawPartBytes_WhenLimitIsOfficialMaximum_AccountsForFramingAndBase64()
    {
        ItermWriter.GetMaximumRawPartBytes(1_048_576).ShouldBe(786_417);
        ItermWriter.GetMaximumRawPartBytes(96).ShouldBe(57);
        ItermWriter.TryCalculateTransactionBytes(
            sourceBytes: 57,
            new Size(2, 3),
            maxSequenceBytes: 1_048_576,
            out var total).ShouldBeTrue();
        total.ShouldBe(188L);
    }

    /// <summary>Verifies maximum source policy and the smallest metadata-capable chunk use O(1) exact math.</summary>
    [Fact]
    public void TryCalculateTransactionBytes_WhenSourceIsMaximumAndChunkIsTiny_ReturnsExactBound()
    {
        ItermWriter.TryCalculateTransactionBytes(
            256 * 1024 * 1024,
            new Size(1, 1),
            maxSequenceBytes: 85,
            out var total).ShouldBeTrue();
        ItermWriter.TryCalculateTransactionBytes(
            256 * 1024 * 1024,
            new Size(1, 1),
            maxSequenceBytes: 84,
            out var rejected).ShouldBeFalse();

        total.ShouldBe(458_577_353L);
        rejected.ShouldBe(0L);
    }

    /// <summary>Verifies unsupported image semantics fail without publishing partial output.</summary>
    [Fact]
    public void Write_WhenFormatOrModeIsUnsupported_RejectsAtomically()
    {
        var rgba = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        var png = GraphicsImage.FromPng(CreatePngPayload(dataBytes: 0));
        var output = new ArrayBufferWriter<byte>();

        _ = Should.Throw<NotSupportedException>(() => ItermWriter.Write(
            rgba,
            new Size(1, 1),
            PlacementMode.Contain,
            output));
        _ = Should.Throw<NotSupportedException>(() => ItermWriter.Write(
            png,
            new Size(1, 1),
            PlacementMode.Cover,
            output));

        output.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies undefined placement modes are invalid arguments and preserve existing output.</summary>
    [Fact]
    public void Write_WhenPlacementModeIsUndefined_RejectsArgumentAtomically()
    {
        var image = GraphicsImage.FromPng(CreatePngPayload(dataBytes: 0));
        var output = new ArrayBufferWriter<byte>();
        output.Write("existing"u8);
        var before = output.WrittenSpan.ToArray();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ItermWriter.Write(
            image,
            new Size(1, 1),
            (PlacementMode) int.MaxValue,
            output));

        exception.ParamName.ShouldBe("mode");
        output.WrittenSpan.ToArray().ShouldBe(before);
    }

    /// <summary>Verifies sequence and transaction limits reject before destination mutation.</summary>
    [Fact]
    public void Write_WhenFinitePolicyIsExceeded_RejectsAtomically()
    {
        var image = GraphicsImage.FromPng(CreatePngPayload(dataBytes: 0));
        var output = new ArrayBufferWriter<byte>();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => ItermWriter.Write(
            image,
            new Size(2, 3),
            PlacementMode.Contain,
            output,
            maxSequenceBytes: 77));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => ItermWriter.Write(
            image,
            new Size(2, 3),
            PlacementMode.Contain,
            output,
            maxOutputBytes: 8));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => ItermWriter.Write(
            image,
            new Size(2, 3),
            PlacementMode.Contain,
            output,
            maxSequenceBytes: 1_048_577));

        output.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies destination failures are not mislabeled as protocol policy failures.</summary>
    [Fact]
    public void Write_WhenDestinationFails_PreservesOriginalException()
    {
        var image = GraphicsImage.FromPng(CreatePngPayload(dataBytes: 0));
        var output = new ItermFailingBufferWriter();

        var exception = Should.Throw<InvalidOperationException>(() => ItermWriter.Write(
            image,
            new Size(1, 1),
            PlacementMode.Contain,
            output));

        exception.Message.ShouldBe("destination failure");
    }

    private static List<byte[]> SplitFrames(ReadOnlySpan<byte> value)
    {
        var frames = new List<byte[]>();

        while (!value.IsEmpty)
        {
            var end = value.IndexOf("\u001b\\"u8);
            end.ShouldBeGreaterThanOrEqualTo(0);
            var length = end + 2;
            frames.Add(value[..length].ToArray());
            value = value[length..];
        }

        return frames;
    }

    private static byte[] DecodePart(byte[] frame)
    {
        var prefix = "\u001b]1337;FilePart="u8;
        frame.AsSpan().StartsWith(prefix).ShouldBeTrue();
        return Convert.FromBase64String(
            Encoding.ASCII.GetString(frame.AsSpan(prefix.Length, frame.Length - prefix.Length - 2)));
    }

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
}
