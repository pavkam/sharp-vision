// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Scrolling;



using ScrollRange = SharpVision.Scrolling.Range;

/// <summary>Verifies immutable scroll ranges, clamping, and thumb geometry.</summary>
public sealed class RangeTests
{
    /// <summary>Verifies constructor validation rejects every invalid relationship.</summary>
    [Fact]
    public void Constructor_WhenValuesAreInvalid_Throws()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ScrollRange(-1, 1, 0, 0));
        _ = Should.Throw<ArgumentException>(() => new ScrollRange(2, 1, 1, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ScrollRange(0, 2, 3, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ScrollRange(0, 2, 0, -1));
    }

    /// <summary>Verifies command values clamp with saturating deltas.</summary>
    [Fact]
    public void Move_WhenDeltaExceedsRange_ClampsToEndpoints()
    {
        var range = new ScrollRange(10, 20, 15, viewport: 4);

        range.Move(int.MinValue).ShouldBe(10);
        range.Move(int.MaxValue).ShouldBe(20);
        range.Clamp(9).ShouldBe(10);
        range.Clamp(21).ShouldBe(20);
    }

    /// <summary>Verifies zero scroll range fills the complete track.</summary>
    [Fact]
    public void Resolve_WhenRangeIsStationary_FillsTrack()
    {
        Thumb.Resolve(new ScrollRange(0, 0, 0, viewport: 0), trackLength: 7)
            .ShouldBe(new Thumb(0, 7));
    }

    /// <summary>Verifies viewport ratio and value position use deterministic rounding.</summary>
    [Fact]
    public void Resolve_WhenRangeScrolls_ComputesExactThumb()
    {
        var range = new ScrollRange(0, 80, 40, viewport: 20);

        var thumb = Thumb.Resolve(range, trackLength: 10);

        thumb.ShouldBe(new Thumb(4, 2));
        Thumb.ValueAt(range, trackLength: 10, thumbStart: 4).ShouldBe(40);
    }

    /// <summary>Verifies a scrolling zero-viewport range still has a one-cell thumb.</summary>
    [Fact]
    public void Resolve_WhenViewportIsZero_UsesOneCellMinimum()
    {
        Thumb.Resolve(new ScrollRange(0, 100, 50, viewport: 0), trackLength: 5)
            .Length.ShouldBe(1);
    }

    /// <summary>Verifies all proportional products remain correct at integer boundaries.</summary>
    [Fact]
    public void Resolve_WhenRangeUsesIntegerBoundary_AvoidsOverflow()
    {
        var range = new ScrollRange(0, int.MaxValue, int.MaxValue, int.MaxValue);

        var thumb = Thumb.Resolve(range, trackLength: 500);

        thumb.ShouldBe(new Thumb(250, 250));
        Thumb.ValueAt(range, trackLength: 500, thumb.Start).ShouldBe(int.MaxValue);
    }

    /// <summary>Verifies a viewport larger than its range yields a stable proportional thumb.</summary>
    [Fact]
    public void Resolve_WhenViewportExceedsRange_UsesCumulativeRoundedEdges()
    {
        var range = new ScrollRange(0, 10, 5, viewport: 100);

        Thumb.Resolve(range, trackLength: 11).ShouldBe(new Thumb(1, 10));
    }

    /// <summary>Verifies resizing recomputes contained geometry without changing range endpoints.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(17)]
    [InlineData(500)]
    public void Resolve_WhenTrackResizes_PreservesContainedEndpointGeometry(int trackLength)
    {
        var range = new ScrollRange(3, 103, 103, viewport: 25);

        var thumb = Thumb.Resolve(range, trackLength);

        (thumb.Start + thumb.Length).ShouldBe(trackLength);
        Thumb.ValueAt(range, trackLength, thumb.Start).ShouldBe(103);
    }

    /// <summary>Verifies zero and one-cell tracks remain contained and invert endpoints.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Resolve_WhenTrackIsTiny_RemainsContained(int trackLength)
    {
        var range = new ScrollRange(3, 9, 9, viewport: 2);
        var thumb = Thumb.Resolve(range, trackLength);

        thumb.Start.ShouldBeGreaterThanOrEqualTo(0);
        thumb.Start.ShouldBeLessThanOrEqualTo(trackLength);
        thumb.Length.ShouldBeLessThanOrEqualTo(trackLength);
        Thumb.ValueAt(range, trackLength, thumb.Start).ShouldBeInRange(3, 9);
    }
}
