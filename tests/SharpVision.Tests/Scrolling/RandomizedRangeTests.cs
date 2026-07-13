// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Scrolling;

using SharpVision.Scrolling;

using Shouldly;

using ScrollRange = SharpVision.Scrolling.Range;

/// <summary>Proves fixed-seed scroll thumb containment, monotonicity, and inversion.</summary>
public sealed class RandomizedRangeTests
{
    private const int _caseCount = 20_000;
    private const int _seed = 0x005C_7011;

    /// <summary>Verifies valid hostile ranges preserve every thumb invariant.</summary>
    [Fact]
    public void Resolve_WhenInputsAreRandomized_PreservesGeometryInvariants()
    {
        Random random = new Random(_seed);

        for (var sample = 0; sample < _caseCount; sample++)
        {
            var minimum = random.Next(0, 1_000_000);
            var span = random.Next(0, 1_000_000);
            var maximum = minimum + span;
            var value = random.NextInt64(minimum, (long) maximum + 1);
            var viewport = random.Next(0, 1_000_000);
            var track = random.Next(0, 501);
            ScrollRange range = new ScrollRange(minimum, maximum, (int) value, viewport);
            Thumb first = Thumb.Resolve(range, track);
            Thumb second = Thumb.Resolve(range, track);
            var context = $"seed=0x{_seed:X8}, case={sample}, range={range}, track={track}";

            second.ShouldBe(first, context);
            first.Start.ShouldBeGreaterThanOrEqualTo(0, context);
            first.Length.ShouldBeGreaterThanOrEqualTo(0, context);
            first.Start.ShouldBeLessThanOrEqualTo(track - first.Length, context);
            first.Length.ShouldBeLessThanOrEqualTo(track, context);
            var mapped = Thumb.ValueAt(range, track, first.Start);
            mapped.ShouldBeInRange(minimum, maximum, context);
            Thumb low = Thumb.Resolve(new ScrollRange(minimum, maximum, minimum, viewport), track);
            Thumb high = Thumb.Resolve(new ScrollRange(minimum, maximum, maximum, viewport), track);
            first.Start.ShouldBeGreaterThanOrEqualTo(low.Start, context);
            first.Start.ShouldBeLessThanOrEqualTo(high.Start, context);

            if (span > 0 && track - first.Length > 0)
            {
                var travel = track - first.Length;
                var step = ((long) span + travel - 1) / travel;
                Math.Abs((long) mapped - (int) value).ShouldBeLessThanOrEqualTo(step, context);
                Thumb.ValueAt(range, track, 0).ShouldBe(minimum, context);
                Thumb.ValueAt(range, track, travel).ShouldBe(maximum, context);
            }
        }
    }
}
