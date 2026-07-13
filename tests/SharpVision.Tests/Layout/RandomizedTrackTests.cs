// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;



/// <summary>Proves seeded track allocation invariants and the span hot path.</summary>
public sealed class RandomizedTrackTests
{
    /// <summary>Verifies 20,000 valid bounded sets are stable, exact, and clamped.</summary>
    [Fact]
    public void Resolve_WhenInputsAreRandomized_PreservesBoundedInvariants()
    {
        const int caseCount = 20_000;
        Random random = new(0x4A70);

        for (int sample = 0; sample < caseCount; sample++)
        {
            int count = random.Next(1, 17);
            int available = random.Next(0, 501);
            Length[] lengths = new Length[count];
            int[] automatic = new int[count];
            int[] minimum = new int[count];
            int[] maximum = new int[count];
            int[] first = new int[count];
            int[] second = new int[count];
            int minimumBudget = available;

            for (int index = 0; index < count; index++)
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

            for (int index = 0; index < count; index++)
            {
                first[index].ShouldBeGreaterThanOrEqualTo(minimum[index]);
                first[index].ShouldBeLessThanOrEqualTo(maximum[index]);
            }
        }
    }

    /// <summary>Verifies the caller-owned span overload allocates no managed memory.</summary>
    [Fact]
    public void Resolve_WhenCallerOwnsStorage_AllocatesNoManagedMemoryAfterWarmup()
    {
        ReadOnlySpan<Length> lengths =
            [Length.Cells(3), Length.Percent(25), Length.Star(1), Length.Star(2)];
        ReadOnlySpan<int> automatic = [0, 0, 0, 0];
        ReadOnlySpan<int> minimum = [0, 0, 0, 0];
        ReadOnlySpan<int> maximum = [10, 20, 100, 100];
        Span<int> destination = stackalloc int[4];
        Tracks.Resolve(80, lengths, automatic, minimum, maximum, destination);
        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int iteration = 0; iteration < 1_000; iteration++)
        {
            Tracks.Resolve(80, lengths, automatic, minimum, maximum, destination);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.ShouldBe(0);
    }

    private static Length NextLength(Random random) => random.Next(0, 4) switch
    {
        0 => Length.Auto,
        1 => Length.Cells(random.Next(0, 101)),
        2 => Length.Percent(random.NextDouble() * 100),
        _ => Length.Star((random.NextDouble() * 4) + 0.01),
    };
}
