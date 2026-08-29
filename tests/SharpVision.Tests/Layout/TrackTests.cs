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
        track.Minimum.ShouldBe(Length.Cells(0));
        track.Maximum.ShouldBeNull();
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

        defaultTrack.Minimum.ShouldBe(Length.Auto);
        defaultTrack.Maximum.ShouldBeNull();
        arraySlot.Minimum.ShouldBe(Length.Auto);
        arraySlot.Maximum.ShouldBeNull();

        var result = new int[1];
        _ = Should.Throw<ArgumentException>(() =>
            Tracks.Resolve(10, [defaultTrack.Length], [0], [defaultTrack.Minimum], [defaultTrack.Maximum], result));

        result.ShouldBe([0]);
    }

    /// <summary>Verifies factories preserve exact length and limit values.</summary>
    [Fact]
    public void Factory_WhenValuesAreValid_CreatesExactTrack()
    {
        Track.Auto(minimum: Length.Percent(10), maximum: Length.Percent(80))
            .ShouldBe(new Track(Length.Auto, Length.Percent(10), Length.Percent(80)));
        Track.Cells(3).Length.ShouldBe(Length.Cells(3));
        Track.Percent(25).Length.ShouldBe(Length.Percent(25));
        Track.Star(2).Length.ShouldBe(Length.Star(2));
        Track.Percent(40, Length.Percent(25), Length.Cells(16)).ToString()
            .ShouldBe("40% [25%..16cells]");
        Track.Star(1, Length.Cells(3)).ToString().ShouldBe("1* [3cells..∞]");
    }

    /// <summary>Verifies invalid limits fail before constructing a usable definition.</summary>
    [Fact]
    public void Constructor_WhenLimitsAreInvalid_ThrowsDocumentedException()
    {
        _ = Should.Throw<ArgumentException>(() => new Track(Length.Auto, Length.Auto));
        _ = Should.Throw<ArgumentException>(() => new Track(Length.Auto, Length.Star(1)));
        _ = Should.Throw<ArgumentException>(() => new Track(Length.Auto, Length.Cells(3), Length.Cells(2)));
        _ = Should.Throw<ArgumentException>(() => new Track(Length.Auto, Length.Percent(30), Length.Percent(20)));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Track.Cells(-1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Track.Percent(101));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Track.Star(0));
    }

    /// <summary>Verifies relative limits resolve against the same explicit base as percentage requests.</summary>
    [Fact]
    public void Resolve_WhenLimitsAreRelative_UsesExplicitPercentageBase()
    {
        var result = new int[3];

        Tracks.Resolve(
            available: 30,
            [Length.Auto, Length.Percent(80), Length.Star(1)],
            [2, 0, 0],
            [Length.Percent(20), Length.Cells(0), Length.Percent(10)],
            [Length.Percent(40), Length.Percent(50), null],
            result,
            percentBase: 40);

        result.ShouldBe([8, 18, 4]);
    }

    /// <summary>Verifies relative limits use safe fallbacks when no bounded percentage base exists.</summary>
    [Fact]
    public void Resolve_WhenRelativeLimitsAreUnbounded_UsesZeroAndInfiniteFallbacks()
    {
        var result = new int[2];

        Tracks.Resolve(
            available: null,
            [Length.Auto, Length.Auto],
            [7, 9],
            [Length.Percent(80), Length.Cells(3)],
            [null, Length.Percent(10)],
            result);

        result.ShouldBe([7, 9]);
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

    /// <summary>Verifies 5,000 typed-limit sets repeat deterministically and remain contained.</summary>
    [Fact]
    public void Resolve_WhenRelativeLimitsAreRandomized_PreservesAllocationInvariants()
    {
        const int caseCount = 5_000;
        var random = new Random(0x840);

        for (var sample = 0; sample < caseCount; sample++)
        {
            var count = random.Next(1, 17);
            var available = random.Next(0, 501);
            var percentBase = random.Next(0, 501);
            var lengths = new Length[count];
            var automatic = new int[count];
            var minimum = new Length[count];
            var maximum = new Length?[count];
            var first = new int[count];
            var second = new int[count];

            for (var index = 0; index < count; index++)
            {
                lengths[index] = index == count - 1 ? Length.Star(1) : NextLength(random);
                automatic[index] = random.Next(0, 61);
                minimum[index] = random.Next(0, 2) == 0
                    ? Length.Cells(random.Next(0, 9))
                    : Length.Percent(random.NextDouble() * 10);
                maximum[index] = index == count - 1 || random.Next(0, 3) == 0
                    ? null
                    : NextMaximum(random, minimum[index]);
            }

            Tracks.Resolve(available, lengths, automatic, minimum, maximum, first, percentBase);
            Tracks.Resolve(available, lengths, automatic, minimum, maximum, second, percentBase);

            second.ShouldBe(first);
            first.Sum().ShouldBe(available);
            first.ShouldAllBe(extent => extent >= 0);

            for (var index = 0; index < count; index++)
            {
                var resolvedMinimum = ResolveLimit(minimum[index], percentBase);
                var resolvedMaximum = maximum[index] is { } authoredMaximum
                    ? ResolveLimit(authoredMaximum, percentBase)
                    : int.MaxValue;
                first[index].ShouldBeLessThanOrEqualTo(Math.Max(resolvedMinimum, resolvedMaximum));
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

    private static Length NextMaximum(Random random, Length minimum) => minimum.Kind switch
    {
        LengthKind.Auto => throw new UnreachableException(),
        LengthKind.Cells => random.Next(0, 2) == 0
            ? Length.Cells(random.Next((int) minimum.Value, (int) minimum.Value + 81))
            : Length.Percent(10 + (random.NextDouble() * 90)),
        LengthKind.Percent => random.Next(0, 2) == 0
            ? Length.Percent(minimum.Value + (random.NextDouble() * (100 - minimum.Value)))
            : Length.Cells(random.Next(1, 81)),
        LengthKind.Star => throw new UnreachableException(),
        _ => throw new UnreachableException()
    };

    private static int ResolveLimit(Length limit, int percentBase) => limit.Kind switch
    {
        LengthKind.Auto => throw new UnreachableException(),
        LengthKind.Cells => (int) limit.Value,
        LengthKind.Percent => (int) Math.Round(percentBase * limit.Value / 100, MidpointRounding.AwayFromZero),
        LengthKind.Star => throw new UnreachableException(),
        _ => throw new UnreachableException()
    };
}
