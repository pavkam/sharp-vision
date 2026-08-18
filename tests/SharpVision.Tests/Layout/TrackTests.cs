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
        var result = Tracks.Resolve(
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
        var result = Tracks.Resolve(
            available: 20,
            [Length.Cells(3), Length.Auto, Length.Percent(25), Length.Star(1)],
            [0, 4, 0, 0]);

        result.ShouldBe([3, 4, 5, 8]);
    }

    /// <summary>Verifies unbounded deferred tracks use intrinsic requests.</summary>
    [Fact]
    public void Resolve_WhenAxisIsUnbounded_UsesIntrinsicForPercentAndStar()
    {
        var result = Tracks.Resolve(
            available: null,
            [Length.Percent(50), Length.Star(1), Length.Cells(5), Length.Auto],
            [4, 3, 0, 2]);

        result.ShouldBe([4, 3, 5, 2]);
    }

    /// <summary>Verifies limits are applied and capped star space is redistributed.</summary>
    [Fact]
    public void Resolve_WhenTracksHaveLimits_ClampsAndRedistributesRemainder()
    {
        var lengths = new[] { Length.Auto, Length.Star(1), Length.Star(1) };
        var automatic = new[] { 10, 0, 0 };
        var minimum = new[] { 0, 4, 0 };
        var maximum = new[] { 6, 5, int.MaxValue };
        var result = new int[3];

        Tracks.Resolve(20, lengths, automatic, minimum, maximum, result);

        result.ShouldBe([6, 5, 9]);
    }

    /// <summary>Verifies deficits discard flexible requests before fixed reservation.</summary>
    [Fact]
    public void Resolve_WhenRequestsExceedAvailable_ShrinksByDeterministicPriority()
    {
        var result = Tracks.Resolve(
            available: 8,
            [Length.Cells(10), Length.Auto, Length.Percent(50), Length.Star(1)],
            [0, 5, 0, 0]);

        result.ShouldBe([8, 0, 0, 0]);
    }

    /// <summary>Verifies zero available space yields only zero extents.</summary>
    [Fact]
    public void Resolve_WhenAvailableIsZero_ReturnsZeroTracks()
    {
        var result = Tracks.Resolve(
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

    /// <summary>Verifies new Track() reaches the declared constructor and matches
    /// Track.Auto()'s defaults, unlike the CLR's implicit zero-initialized state.</summary>
    [Fact]
    public void Constructor_WhenParameterless_MatchesAutoDefaults()
    {
        var track = new Track();

        track.ShouldBe(Track.Auto());
        track.Length.ShouldBe(Length.Auto);
        track.Minimum.ShouldBe(0);
        track.Maximum.ShouldBe(int.MaxValue);
    }

    /// <summary>
    /// Documents the remaining struct hazard this issue cannot fix at the library level:
    /// default(Track) and an unassigned Track[] slot bypass every declared constructor,
    /// including the explicit parameterless one, and zero-initialize every field. A
    /// (Minimum: 0, Maximum: 0) track then resolves to exactly 0 cells — a permanently
    /// invisible track. Track's XML remarks document this; callers managing their own
    /// Track[] must always explicitly initialize every element.
    /// </summary>
    [Fact]
    public void Constructor_WhenDefaultOrArraySlot_RemainsZeroInitializedNotAuto()
    {
        var defaultTrack = default(Track);
        var arraySlot = (new Track[1])[0];

        defaultTrack.Minimum.ShouldBe(0);
        defaultTrack.Maximum.ShouldBe(0);
        arraySlot.Minimum.ShouldBe(0);
        arraySlot.Maximum.ShouldBe(0);

        var result = new int[1];
        Tracks.Resolve(10, [defaultTrack.Length], [0], [defaultTrack.Minimum], [defaultTrack.Maximum], result);

        result.ShouldBe([0]);
    }

    /// <summary>Verifies factories preserve exact length and limit values.</summary>
    [Fact]
    public void Factory_WhenValuesAreValid_CreatesExactTrack()
    {
        Track.Auto(minimum: 1, maximum: 8).ShouldBe(new Track(Length.Auto, 1, 8));
        Track.Cells(3).Length.ShouldBe(Length.Cells(3));
        Track.Percent(25).Length.ShouldBe(Length.Percent(25));
        Track.Star(2).Length.ShouldBe(Length.Star(2));
    }

    /// <summary>Verifies invalid limits fail before constructing a usable definition.</summary>
    [Fact]
    public void Constructor_WhenLimitsAreInvalid_ThrowsDocumentedException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Track(Length.Auto, -1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new Track(Length.Auto, 0, -1));
        _ = Should.Throw<ArgumentException>(() => new Track(Length.Auto, 3, 2));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Track.Cells(-1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Track.Percent(101));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Track.Star(0));
    }

    /// <summary>Verifies 20,000 valid bounded sets are stable, exact, and clamped.</summary>
    [Fact]
    public void Resolve_WhenInputsAreRandomized_PreservesBoundedInvariants()
    {
        const int caseCount = 20_000;
        var random = new Random(0x4A70);

        for (var sample = 0; sample < caseCount; sample++)
        {
            var count = random.Next(1, 17);
            var available = random.Next(0, 501);
            var lengths = new Length[count];
            var automatic = new int[count];
            var minimum = new int[count];
            var maximum = new int[count];
            var first = new int[count];
            var second = new int[count];
            var minimumBudget = available;

            for (var index = 0; index < count; index++)
            {
                lengths[index] = index == count - 1
                    ? Length.Star(random.NextDouble() + 0.01)
                    : NextLength(random);
                automatic[index] = random.Next(0, 61);
                minimum[index] = random.Next(0, Math.Min(8, minimumBudget) + 1);
                minimumBudget -= minimum[index];
                maximum[index] = index == count - 1
                    ? int.MaxValue
                    : random.Next(minimum[index], minimum[index] + 81);
            }

            Tracks.Resolve(available, lengths, automatic, minimum, maximum, first);
            Tracks.Resolve(available, lengths, automatic, minimum, maximum, second);

            second.ShouldBe(first);
            first.Sum().ShouldBe(available);

            for (var index = 0; index < count; index++)
            {
                first[index].ShouldBeGreaterThanOrEqualTo(minimum[index]);
                first[index].ShouldBeLessThanOrEqualTo(maximum[index]);
            }
        }
    }

    private static Length NextLength(Random random) => random.Next(0, 4) switch
    {
        0 => Length.Auto,
        1 => Length.Cells(random.Next(0, 101)),
        2 => Length.Percent(random.NextDouble() * 100),
        _ => Length.Star((random.NextDouble() * 4) + 0.01)
    };
}
