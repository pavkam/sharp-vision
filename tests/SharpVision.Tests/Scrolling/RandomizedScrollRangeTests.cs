// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Scrolling;

/// <summary>Proves fixed-seed scroll thumb containment, monotonicity, and inversion.</summary>
public sealed class RandomizedScrollRangeTests
{
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
