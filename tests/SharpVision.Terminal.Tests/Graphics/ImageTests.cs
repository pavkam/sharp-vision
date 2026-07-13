// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics;

using SharpVision.Terminal.Graphics;


using GraphicsImage = Terminal.Graphics.Image;

/// <summary>Verifies bounded owned RGBA image values.</summary>
public sealed class ImageTests
{
    /// <summary>Verifies RGBA construction copies caller memory and exposes stable metadata.</summary>
    [Fact]
    public void FromRgba_WhenSourceIsValid_OwnsIndependentPixels()
    {
        byte[] source = [1, 2, 3, 4, 5, 6, 7, 8];

        GraphicsImage image = GraphicsImage.FromRgba(new Size(2, 1), source);
        source.AsSpan().Clear();
        byte[] copied = new byte[image.ByteCount];
        int written = image.CopyTo(copied);

        image.Size.ShouldBe(new Size(2, 1));
        image.Format.ShouldBe(Format.Rgba);
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
                new Limits(maxDimension: 1, maxPixels: 1, maxSourceBytes: 4)));
    }

    /// <summary>Verifies a short copy destination is rejected without partial mutation.</summary>
    [Fact]
    public void CopyTo_WhenDestinationIsShort_ThrowsBeforeMutation()
    {
        GraphicsImage image = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 4]);
        byte[] destination = [9, 9, 9];

        _ = Should.Throw<ArgumentException>(() => image.CopyTo(destination));

        destination.ShouldBe([9, 9, 9]);
    }

    /// <summary>Verifies limit construction rejects non-positive or contradictory bounds.</summary>
    [Fact]
    public void Constructor_WhenLimitsAreInvalid_ThrowsDocumentedException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Limits(maxDimension: 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Limits(maxPixels: 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Limits(maxSourceBytes: 0));
        _ = Should.Throw<ArgumentException>(() =>
            new Limits(maxDimension: 2, maxPixels: 5, maxSourceBytes: 20));
    }
}
