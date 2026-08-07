// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics;

using SharpVision.Terminal.Graphics;

/// <summary>Verifies bounded owned RGBA and PNG image values.</summary>
public sealed class ImageSourceTests
{
    /// <summary>Verifies RGBA construction copies caller memory and exposes stable metadata.</summary>
    [Fact]
    public void FromRgba_WhenSourceIsValid_OwnsIndependentPixels()
    {
        byte[] source = [1, 2, 3, 4, 5, 6, 7, 8];

        var image = GraphicsImage.FromRgba(new Size(2, 1), source);
        source.AsSpan().Clear();
        var copied = new byte[image.ByteCount];
        var written = image.CopyTo(copied);

        image.Size.ShouldBe(new Size(2, 1));
        image.Format.ShouldBe(ImageFormat.Rgba);
        image.Identity.ShouldNotBe(0UL);
        written.ShouldBe(8);
        copied.ShouldBe([1, 2, 3, 4, 5, 6, 7, 8]);
    }

    /// <summary>Verifies every invalid RGBA proposal fails before an image is returned.</summary>
    [Fact]
    public void FromRgba_WhenProposalIsInvalid_ThrowsDocumentedException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            GraphicsImage.FromRgba(default, []));
        _ = Should.Throw<ArgumentException>(() =>
            GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3]));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            GraphicsImage.FromRgba(
                new Size(2, 1),
                new byte[8],
                new ImageLimits(maxDimension: 1, maxPixels: 1, maxSourceBytes: 4)));
    }

    /// <summary>Verifies a short copy destination is rejected without partial mutation.</summary>
    [Fact]
    public void CopyTo_WhenDestinationIsShort_ThrowsBeforeMutation()
    {
        var image = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 4]);
        byte[] destination = [9, 9, 9];

        _ = Should.Throw<ArgumentException>(() => image.CopyTo(destination));

        destination.ShouldBe([9, 9, 9]);
    }

    /// <summary>Verifies limit construction rejects non-positive or contradictory bounds.</summary>
    [Fact]
    public void Constructor_WhenLimitsAreInvalid_ThrowsDocumentedException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ImageLimits(maxDimension: 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ImageLimits(maxPixels: 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ImageLimits(maxSourceBytes: 0));
        _ = Should.Throw<ArgumentException>(() =>
            new ImageLimits(maxDimension: 2, maxPixels: 5, maxSourceBytes: 20));
    }

    /// <summary>Verifies a structurally valid PNG is copied with IHDR dimensions.</summary>
    [Fact]
    public void FromPng_WhenStructureIsValid_OwnsBytesAndDimensions()
    {
        var source = CreatePng(3, 2);

        var image = GraphicsImage.FromPng(source);
        source.AsSpan().Clear();
        var copied = new byte[image.ByteCount];
        _ = image.CopyTo(copied);

        image.Size.ShouldBe(new Size(3, 2));
        image.Format.ShouldBe(ImageFormat.Png);
        copied[0].ShouldBe((byte) 137);
        copied.AsSpan(12, 4).SequenceEqual("IHDR"u8).ShouldBeTrue();
        copied.AsSpan(49, 4).SequenceEqual("IEND"u8).ShouldBeTrue();
    }

    /// <summary>Verifies malformed and truncated PNG structures are rejected.</summary>
    [Fact]
    public void FromPng_WhenStructureIsMalformed_ThrowsDocumentedException()
    {
        var invalidSignature = CreatePng(1, 1);
        invalidSignature[0] = 0;
        var invalidHeader = CreatePng(1, 1);
        invalidHeader[24] = 1;
        var missingEnd = CreatePng(1, 1)[..45];
        var oversizedChunk = CreatePng(1, 1);
        oversizedChunk[33] = 127;

        _ = Should.Throw<ArgumentException>(() => GraphicsImage.FromPng(invalidSignature));
        _ = Should.Throw<ArgumentException>(() => GraphicsImage.FromPng(invalidHeader));
        _ = Should.Throw<ArgumentException>(() => GraphicsImage.FromPng(missingEnd));
        _ = Should.Throw<ArgumentException>(() => GraphicsImage.FromPng(oversizedChunk));
    }

    /// <summary>Verifies PNG dimensions and source bytes obey caller policy.</summary>
    [Fact]
    public void FromPng_WhenPolicyIsExceeded_ThrowsBeforeOwnership()
    {
        var source = CreatePng(2, 2);

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            GraphicsImage.FromPng(
                source,
                new ImageLimits(maxDimension: 1, maxPixels: 1, maxSourceBytes: 57)));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            GraphicsImage.FromPng(
                source,
                new ImageLimits(maxDimension: 2, maxPixels: 4, maxSourceBytes: 56)));
    }

    private static byte[] CreatePng(int width, int height)
    {
        byte[] result =
        [
            137, 80, 78, 71, 13, 10, 26, 10,
            0, 0, 0, 13, 73, 72, 68, 82,
            0, 0, 0, 0, 0, 0, 0, 0,
            8, 6, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0, 73, 68, 65, 84,
            0, 0, 0, 0,
            0, 0, 0, 0, 73, 69, 78, 68,
            0, 0, 0, 0
        ];
        WriteInt32(result.AsSpan(16, 4), width);
        WriteInt32(result.AsSpan(20, 4), height);
        return result;
    }

    private static void WriteInt32(Span<byte> destination, int value)
    {
        destination[0] = (byte) (value >> 24);
        destination[1] = (byte) (value >> 16);
        destination[2] = (byte) (value >> 8);
        destination[3] = (byte) value;
    }
}
