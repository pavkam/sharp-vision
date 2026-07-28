// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Compatibility;

using SharpVision.Terminal.Graphics;

/// <summary>Freezes the public semantic image-placement compatibility contract.</summary>
public sealed class GraphicsPlacementConsumerTests
{
    /// <summary>Verifies default and Empty are equal valid sentinels with no retained image.</summary>
    [Fact]
    public void Placement_WhenDefaultOrEmpty_ExposesValidEmptySentinel()
    {
        var value = default(Placement);

        value.ShouldBe(Placement.Empty);
        (value == Placement.Empty).ShouldBeTrue();
        (value != Placement.Empty).ShouldBeFalse();
        value.GetHashCode().ShouldBe(Placement.Empty.GetHashCode());
        value.IsEmpty.ShouldBeTrue();
        value.Image.ShouldBeNull();
        value.ImageIdentity.ShouldBe(0UL);
        value.Source.ShouldBe(default);
        value.Destination.ShouldBe(default);
        value.Mode.ShouldBe(PlacementMode.Contain);
    }

    /// <summary>Verifies consumer-facing APIs construct complete nonempty semantic state.</summary>
    [Fact]
    public void Placement_WhenConstructedByConsumer_ExposesValidatedState()
    {
        var image = GraphicsImage.FromRgba(new Size(2, 1), [1, 2, 3, 255, 4, 5, 6, 255]);

        var value = new Placement(
            image,
            new Rect(1, 0, 1, 1),
            new Rect(3, 4, 5, 6),
            PlacementMode.Cover);

        value.IsEmpty.ShouldBeFalse();
        value.Image.ShouldBeSameAs(image);
        value.ImageIdentity.ShouldBe(image.Identity);
        value.Source.ShouldBe(new Rect(1, 0, 1, 1));
        value.Destination.ShouldBe(new Rect(3, 4, 5, 6));
        value.Mode.ShouldBe(PlacementMode.Cover);
        (value != Placement.Empty).ShouldBeTrue();
    }
}
