// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics;

using SharpVision.Terminal.Graphics;

/// <summary>Proves owned semantic placement geometry construction and validation.</summary>
public sealed class PlacementTests
{
    /// <summary>Verifies the public constructor rejects a null image.</summary>
    [Fact]
    public void Constructor_WhenImageIsNull_ThrowsArgumentNullException()
    {
        _ = Should.Throw<ArgumentNullException>(() => new Placement(
            null!,
            new Rect(0, 0, 1, 1),
            new Rect(0, 0, 1, 1),
            PlacementMode.Contain));
    }

    /// <summary>Verifies placement retains the exact immutable image identity.</summary>
    [Fact]
    public void Constructor_WhenImageIsProvided_OwnsStableIdentity()
    {
        var image = CreateImage(2, 2, 1);

        var placement = new Placement(
            image,
            new Rect(0, 0, 2, 2),
            new Rect(1, 2, 3, 4),
            PlacementMode.Contain);

        placement.Image.ShouldBeSameAs(image);
        placement.ImageIdentity.ShouldBe(image.Identity);
    }

    /// <summary>Verifies empty and out-of-image source rectangles are rejected.</summary>
    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(-1, 0, 1, 1)]
    [InlineData(0, -1, 1, 1)]
    [InlineData(1, 1, 2, 2)]
    public void Constructor_WhenSourceIsInvalid_ThrowsArgumentOutOfRangeException(
        int x,
        int y,
        int width,
        int height)
    {
        var image = CreateImage(2, 2, 1);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Placement(
            image,
            new Rect(x, y, width, height),
            new Rect(0, 0, 1, 1),
            PlacementMode.Stretch));
    }

    /// <summary>Verifies empty, negative-origin, and unknown destination state is rejected.</summary>
    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(-1, 0, 1, 1)]
    [InlineData(0, -1, 1, 1)]
    public void Constructor_WhenDestinationIsInvalid_ThrowsArgumentOutOfRangeException(
        int x,
        int y,
        int width,
        int height)
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Placement(
            CreateImage(1, 1, 1),
            new Rect(0, 0, 1, 1),
            new Rect(x, y, width, height),
            PlacementMode.Stretch));
    }

    /// <summary>Verifies undefined fitting mode values are rejected.</summary>
    [Fact]
    public void Constructor_WhenModeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Placement(
            CreateImage(1, 1, 1),
            new Rect(0, 0, 1, 1),
            new Rect(0, 0, 1, 1),
            (PlacementMode) int.MaxValue));
    }

    private static GraphicsImage CreateImage(int width, int height, byte value)
    {
        var source = new byte[checked(width * height * 4)];
        source.AsSpan().Fill(value);
        return GraphicsImage.FromRgba(new Size(width, height), source);
    }
}
