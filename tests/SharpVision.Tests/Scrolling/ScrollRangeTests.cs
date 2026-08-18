// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Scrolling;

/// <summary>Verifies immutable scroll ranges, clamping, and thumb geometry.</summary>
public sealed class ScrollRangeTests
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
        ScrollThumb.Resolve(new ScrollRange(0, 0, 0, viewport: 0), trackLength: 7)
            .ShouldBe(new ScrollThumb(0, 7));
    }

    /// <summary>Verifies viewport ratio and value position use deterministic rounding.</summary>
    [Fact]
    public void Resolve_WhenRangeScrolls_ComputesExactThumb()
    {
        var range = new ScrollRange(0, 80, 40, viewport: 20);

        var thumb = ScrollThumb.Resolve(range, trackLength: 10);

        thumb.ShouldBe(new ScrollThumb(4, 2));
        ScrollThumb.ValueAt(range, trackLength: 10, thumbStart: 4).ShouldBe(40);
    }

    /// <summary>Verifies a scrolling zero-viewport range still has a one-cell thumb.</summary>
    [Fact]
    public void Resolve_WhenViewportIsZero_UsesOneCellMinimum()
    {
        ScrollThumb.Resolve(new ScrollRange(0, 100, 50, viewport: 0), trackLength: 5)
            .Length.ShouldBe(1);
    }

    /// <summary>Verifies all proportional products remain correct at integer boundaries.</summary>
    [Fact]
    public void Resolve_WhenRangeUsesIntegerBoundary_AvoidsOverflow()
    {
        var range = new ScrollRange(0, int.MaxValue, int.MaxValue, int.MaxValue);

        var thumb = ScrollThumb.Resolve(range, trackLength: 500);

        thumb.ShouldBe(new ScrollThumb(250, 250));
        ScrollThumb.ValueAt(range, trackLength: 500, thumb.Start).ShouldBe(int.MaxValue);
    }

    /// <summary>Verifies a viewport larger than its range yields a stable proportional thumb.</summary>
    [Fact]
    public void Resolve_WhenViewportExceedsRange_UsesCumulativeRoundedEdges()
    {
        var range = new ScrollRange(0, 10, 5, viewport: 100);

        ScrollThumb.Resolve(range, trackLength: 11).ShouldBe(new ScrollThumb(1, 10));
    }

    /// <summary>Verifies resizing recomputes contained geometry without changing range endpoints.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(17)]
    [InlineData(500)]
    public void Resolve_WhenTrackResizes_PreservesContainedEndpointGeometry(int trackLength)
    {
        var range = new ScrollRange(3, 103, 103, viewport: 25);

        var thumb = ScrollThumb.Resolve(range, trackLength);

        (thumb.Start + thumb.Length).ShouldBe(trackLength);
        ScrollThumb.ValueAt(range, trackLength, thumb.Start).ShouldBe(103);
    }

    /// <summary>Verifies zero and one-cell tracks remain contained and invert endpoints.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Resolve_WhenTrackIsTiny_RemainsContained(int trackLength)
    {
        var range = new ScrollRange(3, 9, 9, viewport: 2);
        var thumb = ScrollThumb.Resolve(range, trackLength);

        thumb.Start.ShouldBeGreaterThanOrEqualTo(0);
        thumb.Start.ShouldBeLessThanOrEqualTo(trackLength);
        thumb.Length.ShouldBeLessThanOrEqualTo(trackLength);
        ScrollThumb.ValueAt(range, trackLength, thumb.Start).ShouldBeInRange(3, 9);
    }

    private const int _caseCount = 20_000;
    private const int _seed = 0x005C_7011;

    /// <summary>Verifies valid hostile ranges preserve every thumb invariant.</summary>
    [Fact]
    public void Resolve_WhenInputsAreRandomized_PreservesGeometryInvariants()
    {
        var random = new Random(_seed);

        for (var sample = 0; sample < _caseCount; sample++)
        {
            var minimum = random.Next(0, 1_000_000);
            var span = random.Next(0, 1_000_000);
            var maximum = minimum + span;
            var value = random.NextInt64(minimum, (long) maximum + 1);
            var viewport = random.Next(0, 1_000_000);
            var track = random.Next(0, 501);
            var range = new ScrollRange(minimum, maximum, (int) value, viewport);
            var first = ScrollThumb.Resolve(range, track);
            var second = ScrollThumb.Resolve(range, track);
            var context = $"seed=0x{_seed:X8}, case={sample}, range={range}, track={track}";

            second.ShouldBe(first, context);
            first.Start.ShouldBeGreaterThanOrEqualTo(0, context);
            first.Length.ShouldBeGreaterThanOrEqualTo(0, context);
            first.Start.ShouldBeLessThanOrEqualTo(track - first.Length, context);
            first.Length.ShouldBeLessThanOrEqualTo(track, context);
            var mapped = ScrollThumb.ValueAt(range, track, first.Start);
            mapped.ShouldBeInRange(minimum, maximum, context);
            var low = ScrollThumb.Resolve(new ScrollRange(minimum, maximum, minimum, viewport), track);
            var high = ScrollThumb.Resolve(new ScrollRange(minimum, maximum, maximum, viewport), track);
            first.Start.ShouldBeGreaterThanOrEqualTo(low.Start, context);
            first.Start.ShouldBeLessThanOrEqualTo(high.Start, context);

            if (span > 0 && track - first.Length > 0)
            {
                var travel = track - first.Length;
                var step = ((long) span + travel - 1) / travel;
                Math.Abs((long) mapped - (int) value).ShouldBeLessThanOrEqualTo(step, context);
                ScrollThumb.ValueAt(range, track, 0).ShouldBe(minimum, context);
                ScrollThumb.ValueAt(range, track, travel).ShouldBe(maximum, context);
            }
        }
    }
}
