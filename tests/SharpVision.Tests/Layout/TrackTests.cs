// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;



/// <summary>Verifies exact fixed, intrinsic, percentage, star, and span allocation.</summary>
public sealed class TrackTests
{
    /// <summary>Verifies cumulative rounding assigns the exact remainder.</summary>
    [Fact]
    public void Resolve_WhenPercentAndStarsShareOddSpace_UsesCumulativeEdges()
    {
        int[] result = Tracks.Resolve(
            available: 11,
            [Length.Percent(50), Length.Star(1), Length.Star(1)],
            [0, 0, 0]);

        result.ShouldBe([6, 3, 2]);
        result.Sum().ShouldBe(11);
    }

    /// <summary>Verifies fixed and auto tracks reserve before percentages and stars.</summary>
    [Fact]
    public void Resolve_WhenKindsAreMixed_UsesFinalAxisAndRemainingSpace()
    {
        int[] result = Tracks.Resolve(
            available: 20,
            [Length.Cells(3), Length.Auto, Length.Percent(25), Length.Star(1)],
            [0, 4, 0, 0]);

        result.ShouldBe([3, 4, 5, 8]);
    }

    /// <summary>Verifies unbounded deferred tracks use intrinsic requests.</summary>
    [Fact]
    public void Resolve_WhenAxisIsUnbounded_UsesIntrinsicForPercentAndStar()
    {
        int[] result = Tracks.Resolve(
            available: null,
            [Length.Percent(50), Length.Star(1), Length.Cells(5), Length.Auto],
            [4, 3, 0, 2]);

        result.ShouldBe([4, 3, 5, 2]);
    }

    /// <summary>Verifies limits are applied and capped star space is redistributed.</summary>
    [Fact]
    public void Resolve_WhenTracksHaveLimits_ClampsAndRedistributesRemainder()
    {
        Length[] lengths = [Length.Auto, Length.Star(1), Length.Star(1)];
        int[] automatic = [10, 0, 0];
        int[] minimum = [0, 4, 0];
        int[] maximum = [6, 5, int.MaxValue];
        int[] result = new int[3];

        Tracks.Resolve(20, lengths, automatic, minimum, maximum, result);

        result.ShouldBe([6, 5, 9]);
    }

    /// <summary>Verifies deficits discard flexible requests before fixed reservation.</summary>
    [Fact]
    public void Resolve_WhenRequestsExceedAvailable_ShrinksByDeterministicPriority()
    {
        int[] result = Tracks.Resolve(
            available: 8,
            [Length.Cells(10), Length.Auto, Length.Percent(50), Length.Star(1)],
            [0, 5, 0, 0]);

        result.ShouldBe([8, 0, 0, 0]);
    }

    /// <summary>Verifies zero available space yields only zero extents.</summary>
    [Fact]
    public void Resolve_WhenAvailableIsZero_ReturnsZeroTracks()
    {
        int[] result = Tracks.Resolve(
            available: 0,
            [Length.Cells(4), Length.Auto, Length.Percent(50), Length.Star(1)],
            [0, 3, 0, 0]);

        result.ShouldBe([0, 0, 0, 0]);
    }

    /// <summary>Verifies all arguments are validated before destination mutation.</summary>
    [Fact]
    public void Resolve_WhenInputIsInvalid_ThrowsBeforeWritingDestination()
    {
        int[] destination = [7, 7];

        _ = Should.Throw<ArgumentException>(() => Tracks.Resolve(
            10,
            [Length.Auto, Length.Star(1)],
            [2],
            [0, 0],
            [10, 10],
            destination));

        destination.ShouldBe([7, 7]);
    }

    /// <summary>Verifies a spanning intrinsic deficit is distributed exactly.</summary>
    [Fact]
    public void Satisfy_WhenSpanRequiresMoreSpace_DistributesCumulativeDeficit()
    {
        int[] tracks = [2, 1, 2, 9];

        Tracks.Satisfy(tracks, start: 0, count: 3, required: 11);

        tracks.ShouldBe([4, 3, 4, 9]);
    }

    /// <summary>Verifies span validation happens before caller storage changes.</summary>
    [Fact]
    public void Satisfy_WhenRangeIsInvalid_ThrowsBeforeWritingTracks()
    {
        int[] tracks = [2, 1, 2];

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            Tracks.Satisfy(tracks, start: 2, count: 2, required: 8));

        tracks.ShouldBe([2, 1, 2]);
    }
}
